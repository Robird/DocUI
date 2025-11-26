using System;
using System.Collections.Generic;

namespace DocUI.Design.Samples {
    public record struct NodeHandle(long Address, int Length, int Version);

    public interface IAppendOnlyLog {
        long Append(ReadOnlySpan<byte> payload);
        ReadOnlyMemory<byte> Read(long address, int length);
    }

    public interface INodeSerializer<TKey, TValue> {
        ReadOnlyMemory<byte> SerializeLeaf(IReadOnlyList<TKey> keys, IReadOnlyList<TValue> values);
        ReadOnlyMemory<byte> SerializeInternal(IReadOnlyList<TKey> keys, IReadOnlyList<NodeHandle> children);
        bool IsLeaf(ReadOnlySpan<byte> payload);
        LeafNodeData<TKey, TValue> DeserializeLeaf(ReadOnlySpan<byte> payload);
        InternalNodeData<TKey> DeserializeInternal(ReadOnlySpan<byte> payload);
    }

    public sealed record LeafNodeData<TKey, TValue>(IReadOnlyList<TKey> Keys, IReadOnlyList<TValue> Values);

    public sealed record InternalNodeData<TKey>(IReadOnlyList<TKey> Keys, IReadOnlyList<NodeHandle> Children);

    public sealed class InMemoryAppendOnlyLog : IAppendOnlyLog {
        private readonly List<byte[]> _pages = new();
        private readonly List<long> _addresses = new();
        private long _nextAddress;

        public long Append(ReadOnlySpan<byte> payload) {
            var buffer = payload.ToArray();
            var address = _nextAddress;
            _pages.Add(buffer);
            _addresses.Add(address);
            _nextAddress += buffer.Length;
            return address;
        }

        public ReadOnlyMemory<byte> Read(long address, int length) {
            for (int i = 0; i < _addresses.Count; i++) {
                if (_addresses[i] == address) {
                    var page = _pages[i];
                    if (length > page.Length) {
                        throw new ArgumentOutOfRangeException(nameof(length), "Requested payload length exceeds stored segment.");
                    }

                    return new ReadOnlyMemory<byte>(page, 0, length);
                }
            }

            throw new KeyNotFoundException($"Log address {address} was not found.");
        }
    }

    /// <summary>
    /// Immutable snapshot of a persistent copy-on-write B+Tree stored inside an append-only log.
    /// Usage example:
    /// <code>
    /// var log = new InMemoryAppendOnlyLog();
    /// var serializer = new CustomNodeSerializer();
    /// var builder = new PersistentBPlusTree&lt;string,int&gt;.Builder(serializer);
    /// builder.Upsert("alpha", 1);
    /// builder.Upsert("beta", 2);
    /// var tree = builder.Seal(log);
    /// var nextBuilder = tree.ToBuilder();
    /// nextBuilder.Remove("alpha");
    /// var nextTree = nextBuilder.Seal(log);
    /// nextTree.TryGetValue("beta", out var persisted);
    /// </code>
    /// </summary>
    public sealed class PersistentBPlusTree<TKey, TValue> {
        private readonly IAppendOnlyLog _log;
        private readonly INodeSerializer<TKey, TValue> _serializer;
        private readonly IComparer<TKey> _comparer;
        private readonly int _fanout;
        private readonly NodeHandle? _rootHandle;

        public int Version { get; }
        public bool IsEmpty => !_rootHandle.HasValue;

        public PersistentBPlusTree(IAppendOnlyLog log, INodeSerializer<TKey, TValue> serializer, NodeHandle? rootHandle, int fanout = 32, IComparer<TKey>? comparer = null, int version = 0) {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _comparer = comparer ?? Comparer<TKey>.Default;
            if (fanout < 3) {
                throw new ArgumentOutOfRangeException(nameof(fanout), "Fanout must be greater than two.");
            }

            _fanout = fanout;
            _rootHandle = rootHandle;
            Version = version;
        }

        public static PersistentBPlusTree<TKey, TValue> Empty(IAppendOnlyLog log, INodeSerializer<TKey, TValue> serializer, int fanout = 32, IComparer<TKey>? comparer = null)
            => new(log, serializer, null, fanout, comparer);

        public Builder ToBuilder() => new(this);

        public bool TryGetValue(TKey key, out TValue value) {
            if (!_rootHandle.HasValue) {
                value = default!;
                return false;
            }

            var handle = _rootHandle.Value;
            while (true) {
                var payload = _log.Read(handle.Address, handle.Length).Span;
                if (_serializer.IsLeaf(payload)) {
                    var leaf = _serializer.DeserializeLeaf(payload);
                    int idx = BinarySearch(leaf.Keys, key);
                    if (idx >= 0) {
                        value = leaf.Values[idx];
                        return true;
                    }

                    value = default!;
                    return false;
                }

                var internalNode = _serializer.DeserializeInternal(payload);
                var childIndex = LocateChildIndex(internalNode.Keys, key);
                handle = internalNode.Children[childIndex];
            }
        }

        private int LocateChildIndex(IReadOnlyList<TKey> keys, TKey key) {
            int lo = 0;
            int hi = keys.Count;
            while (lo < hi) {
                int mid = (lo + hi) / 2;
                int cmp = _comparer.Compare(key, keys[mid]);
                if (cmp < 0) {
                    hi = mid;
                }
                else {
                    lo = mid + 1;
                }
            }

            return lo;
        }

        private int BinarySearch(IReadOnlyList<TKey> keys, TKey key) {
            int lo = 0;
            int hi = keys.Count - 1;
            while (lo <= hi) {
                int mid = (lo + hi) / 2;
                int cmp = _comparer.Compare(key, keys[mid]);
                if (cmp == 0) {
                    return mid;
                }

                if (cmp < 0) {
                    hi = mid - 1;
                }
                else {
                    lo = mid + 1;
                }
            }

            return ~lo;
        }

        public sealed class Builder {
            private readonly INodeSerializer<TKey, TValue> _serializer;
            private readonly IComparer<TKey> _comparer;
            private readonly int _fanout;
            private readonly int _maxKeys;
            private readonly int _minLeafKeys;
            private readonly int _minInternalKeys;
            private readonly IAppendOnlyLog? _snapshotLog;
            private int _versionSeed;
            private Node? _root;

            public Builder(INodeSerializer<TKey, TValue> serializer, IComparer<TKey>? comparer = null, int fanout = 32) {
                _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
                _comparer = comparer ?? Comparer<TKey>.Default;
                if (fanout < 3) {
                    throw new ArgumentOutOfRangeException(nameof(fanout), "Fanout must be greater than two.");
                }

                _fanout = fanout;
                _maxKeys = _fanout - 1;
                _minLeafKeys = Math.Max(1, _maxKeys / 2);
                _minInternalKeys = Math.Max(1, _maxKeys / 2);
                _snapshotLog = null;
                _versionSeed = 1;
                _root = null;
            }

            internal Builder(PersistentBPlusTree<TKey, TValue> snapshot) {
                ArgumentNullException.ThrowIfNull(snapshot);

                _serializer = snapshot._serializer;
                _comparer = snapshot._comparer;
                _fanout = snapshot._fanout;
                _maxKeys = _fanout - 1;
                _minLeafKeys = Math.Max(1, _maxKeys / 2);
                _minInternalKeys = Math.Max(1, _maxKeys / 2);
                _snapshotLog = snapshot._log;
                _versionSeed = snapshot.Version + 1;
                _root = snapshot._rootHandle.HasValue ? LoadNode(snapshot._rootHandle.Value) : null;
            }

            public void Upsert(TKey key, TValue value) {
                _root ??= new LeafNode();
                var split = Insert(_root, key, value);
                if (split.HasValue) {
                    var newRoot = new InternalNode();
                    newRoot.Keys.Add(split.Value.Separator);
                    newRoot.Children.Add(_root);
                    newRoot.Children.Add(split.Value.Right);
                    newRoot.MarkDirty();
                    _root = newRoot;
                }
            }

            public bool Remove(TKey key) {
                if (_root is null) {
                    return false;
                }

                if (!RemoveInternal(_root, key)) {
                    return false;
                }

                if (_root is InternalNode internalRoot && internalRoot.Keys.Count == 0) {
                    _root = internalRoot.Children.Count > 0 ? internalRoot.Children[0] : null;
                }
                else if (_root is LeafNode leafRoot && leafRoot.Keys.Count == 0) {
                    _root = null;
                }

                return true;
            }

            public PersistentBPlusTree<TKey, TValue> Seal(IAppendOnlyLog log) {
                ArgumentNullException.ThrowIfNull(log);
                var version = _versionSeed++;
                NodeHandle? rootHandle = null;
                if (_root is not null) {
                    rootHandle = PersistNode(_root, log, version);
                }

                return new PersistentBPlusTree<TKey, TValue>(log, _serializer, rootHandle, _fanout, _comparer, version);
            }

            private SplitResult? Insert(Node node, TKey key, TValue value) {
                if (node is LeafNode leaf) {
                    InsertIntoLeaf(leaf, key, value);
                    if (leaf.Keys.Count > _maxKeys) {
                        return SplitLeaf(leaf);
                    }

                    return null;
                }

                var internalNode = (InternalNode)node;
                int childIndex = LocateChildIndex(internalNode.Keys, key);
                var split = Insert(internalNode.Children[childIndex], key, value);
                if (split.HasValue) {
                    internalNode.Keys.Insert(childIndex, split.Value.Separator);
                    internalNode.Children.Insert(childIndex + 1, split.Value.Right);
                    internalNode.MarkDirty();
                    if (internalNode.Keys.Count > _maxKeys) {
                        return SplitInternal(internalNode);
                    }
                }

                return null;
            }

            private bool RemoveInternal(Node node, TKey key) {
                if (node is LeafNode leaf) {
                    int idx = BinarySearchIndex(leaf.Keys, key);
                    if (idx < 0) {
                        return false;
                    }

                    leaf.Keys.RemoveAt(idx);
                    leaf.Values.RemoveAt(idx);
                    leaf.MarkDirty();
                    return true;
                }

                var internalNode = (InternalNode)node;
                int childIndex = LocateChildIndex(internalNode.Keys, key);
                var child = internalNode.Children[childIndex];
                if (!RemoveInternal(child, key)) {
                    return false;
                }

                if (NeedsRebalance(child)) {
                    if (!TryBorrow(internalNode, childIndex)) {
                        MergeChildren(internalNode, Math.Max(childIndex - 1, 0));
                    }
                }

                internalNode.MarkDirty();
                return true;
            }

            private bool NeedsRebalance(Node node) {
                if (node == _root) {
                    return false;
                }

                int min = node.IsLeaf ? _minLeafKeys : _minInternalKeys;
                return node.KeyCount < min;
            }

            private bool TryBorrow(InternalNode parent, int childIndex) {
                if (childIndex > 0 && CanBorrow(parent.Children[childIndex - 1])) {
                    BorrowFromLeft(parent, childIndex);
                    return true;
                }

                if (childIndex < parent.Children.Count - 1 && CanBorrow(parent.Children[childIndex + 1])) {
                    BorrowFromRight(parent, childIndex);
                    return true;
                }

                return false;
            }

            private bool CanBorrow(Node sibling) {
                int min = sibling.IsLeaf ? _minLeafKeys : _minInternalKeys;
                return sibling.KeyCount > min;
            }

            private void BorrowFromLeft(InternalNode parent, int childIndex) {
                var child = parent.Children[childIndex];
                var left = parent.Children[childIndex - 1];

                if (child is LeafNode leaf && left is LeafNode leftLeaf) {
                    int last = leftLeaf.Keys.Count - 1;
                    var borrowedKey = leftLeaf.Keys[last];
                    var borrowedValue = leftLeaf.Values[last];
                    leftLeaf.Keys.RemoveAt(last);
                    leftLeaf.Values.RemoveAt(last);
                    leaf.Keys.Insert(0, borrowedKey);
                    leaf.Values.Insert(0, borrowedValue);
                    parent.Keys[childIndex - 1] = leaf.Keys[0];
                    leftLeaf.MarkDirty();
                    leaf.MarkDirty();
                    parent.MarkDirty();
                    return;
                }

                if (child is InternalNode childInternal && left is InternalNode leftInternal) {
                    int lastChildIndex = leftInternal.Children.Count - 1;
                    var borrowedChild = leftInternal.Children[lastChildIndex];
                    var borrowedKey = leftInternal.Keys[leftInternal.Keys.Count - 1];
                    leftInternal.Children.RemoveAt(lastChildIndex);
                    leftInternal.Keys.RemoveAt(leftInternal.Keys.Count - 1);

                    var separator = parent.Keys[childIndex - 1];
                    childInternal.Children.Insert(0, borrowedChild);
                    childInternal.Keys.Insert(0, separator);
                    parent.Keys[childIndex - 1] = borrowedKey;

                    leftInternal.MarkDirty();
                    childInternal.MarkDirty();
                    parent.MarkDirty();
                }
            }

            private void BorrowFromRight(InternalNode parent, int childIndex) {
                var child = parent.Children[childIndex];
                var right = parent.Children[childIndex + 1];

                if (child is LeafNode leaf && right is LeafNode rightLeaf) {
                    var borrowedKey = rightLeaf.Keys[0];
                    var borrowedValue = rightLeaf.Values[0];
                    rightLeaf.Keys.RemoveAt(0);
                    rightLeaf.Values.RemoveAt(0);
                    leaf.Keys.Add(borrowedKey);
                    leaf.Values.Add(borrowedValue);
                    parent.Keys[childIndex] = rightLeaf.Keys[0];
                    rightLeaf.MarkDirty();
                    leaf.MarkDirty();
                    parent.MarkDirty();
                    return;
                }

                if (child is InternalNode childInternal && right is InternalNode rightInternal) {
                    var borrowedChild = rightInternal.Children[0];
                    var separator = parent.Keys[childIndex];
                    childInternal.Keys.Add(separator);
                    childInternal.Children.Add(borrowedChild);

                    var replacementKey = rightInternal.Keys[0];
                    parent.Keys[childIndex] = replacementKey;
                    rightInternal.Keys.RemoveAt(0);
                    rightInternal.Children.RemoveAt(0);

                    childInternal.MarkDirty();
                    rightInternal.MarkDirty();
                    parent.MarkDirty();
                }
            }

            private void MergeChildren(InternalNode parent, int leftIndex) {
                if (leftIndex < 0 || leftIndex >= parent.Children.Count - 1) {
                    return;
                }

                var left = parent.Children[leftIndex];
                var right = parent.Children[leftIndex + 1];

                if (left is LeafNode leftLeaf && right is LeafNode rightLeaf) {
                    leftLeaf.Keys.AddRange(rightLeaf.Keys);
                    leftLeaf.Values.AddRange(rightLeaf.Values);
                    leftLeaf.MarkDirty();
                }
                else if (left is InternalNode leftInternal && right is InternalNode rightInternal) {
                    var separator = parent.Keys[leftIndex];
                    leftInternal.Keys.Add(separator);
                    leftInternal.Keys.AddRange(rightInternal.Keys);
                    leftInternal.Children.AddRange(rightInternal.Children);
                    leftInternal.MarkDirty();
                }

                parent.Keys.RemoveAt(leftIndex);
                parent.Children.RemoveAt(leftIndex + 1);
                parent.MarkDirty();
            }

            private void InsertIntoLeaf(LeafNode leaf, TKey key, TValue value) {
                int idx = BinarySearchIndex(leaf.Keys, key);
                if (idx >= 0) {
                    leaf.Values[idx] = value;
                }
                else {
                    int insertIndex = ~idx;
                    leaf.Keys.Insert(insertIndex, key);
                    leaf.Values.Insert(insertIndex, value);
                }

                leaf.MarkDirty();
            }

            private SplitResult SplitLeaf(LeafNode leaf) {
                int splitIndex = leaf.Keys.Count / 2;
                int total = leaf.Keys.Count;
                int rightCount = total - splitIndex;
                var rightKeys = leaf.Keys.GetRange(splitIndex, rightCount);
                var rightValues = leaf.Values.GetRange(splitIndex, rightCount);

                leaf.Keys.RemoveRange(splitIndex, rightCount);
                leaf.Values.RemoveRange(splitIndex, rightCount);

                var right = new LeafNode(rightKeys, rightValues);
                right.MarkDirty();
                leaf.MarkDirty();

                return new SplitResult(right.Keys[0], right);
            }

            private SplitResult SplitInternal(InternalNode node) {
                int totalKeys = node.Keys.Count;
                int totalChildren = node.Children.Count;
                int splitIndex = totalKeys / 2;
                var separator = node.Keys[splitIndex];

                var rightKeys = node.Keys.GetRange(splitIndex + 1, totalKeys - splitIndex - 1);
                var rightChildren = node.Children.GetRange(splitIndex + 1, totalChildren - splitIndex - 1);

                node.Keys.RemoveRange(splitIndex, totalKeys - splitIndex);
                node.Children.RemoveRange(splitIndex + 1, totalChildren - splitIndex - 1);

                var right = new InternalNode(rightKeys, rightChildren);
                right.MarkDirty();
                node.MarkDirty();

                return new SplitResult(separator, right);
            }

            private int BinarySearchIndex(List<TKey> keys, TKey key) {
                int lo = 0;
                int hi = keys.Count - 1;
                while (lo <= hi) {
                    int mid = (lo + hi) / 2;
                    int cmp = _comparer.Compare(key, keys[mid]);
                    if (cmp == 0) {
                        return mid;
                    }

                    if (cmp < 0) {
                        hi = mid - 1;
                    }
                    else {
                        lo = mid + 1;
                    }
                }

                return ~lo;
            }

            private int LocateChildIndex(List<TKey> keys, TKey key) {
                int lo = 0;
                int hi = keys.Count;
                while (lo < hi) {
                    int mid = (lo + hi) / 2;
                    int cmp = _comparer.Compare(key, keys[mid]);
                    if (cmp < 0) {
                        hi = mid;
                    }
                    else {
                        lo = mid + 1;
                    }
                }

                return lo;
            }

            private NodeHandle PersistNode(Node node, IAppendOnlyLog log, int version) {
                if (!node.Dirty && node.CachedHandle.HasValue) {
                    return node.CachedHandle.Value;
                }

                if (node is LeafNode leaf) {
                    var payload = _serializer.SerializeLeaf(leaf.Keys, leaf.Values);
                    var address = log.Append(payload.Span); // incremental serialization point: freshly built leaf appended once dirtied.
                    var handle = new NodeHandle(address, payload.Length, version);
                    leaf.MarkClean(handle);
                    return handle;
                }

                var internalNode = (InternalNode)node;
                var childHandles = new NodeHandle[internalNode.Children.Count];
                for (int i = 0; i < internalNode.Children.Count; i++) {
                    childHandles[i] = PersistNode(internalNode.Children[i], log, version);
                }

                var internalPayload = _serializer.SerializeInternal(internalNode.Keys, childHandles);
                var internalAddress = log.Append(internalPayload.Span); // incremental serialization point: parent captures latest child handles before sealing.
                var nodeHandle = new NodeHandle(internalAddress, internalPayload.Length, version);
                internalNode.MarkClean(nodeHandle);
                return nodeHandle;
            }

            private Node LoadNode(NodeHandle handle) {
                if (_snapshotLog is null) {
                    throw new InvalidOperationException("A snapshot log is required to materialize existing nodes.");
                }

                var payload = _snapshotLog.Read(handle.Address, handle.Length).Span;
                if (_serializer.IsLeaf(payload)) {
                    var leafData = _serializer.DeserializeLeaf(payload);
                    return new LeafNode(leafData.Keys, leafData.Values, handle, dirty: false);
                }

                var internalData = _serializer.DeserializeInternal(payload);
                var children = new List<Node>(internalData.Children.Count);
                foreach (var childHandle in internalData.Children) {
                    children.Add(LoadNode(childHandle));
                }

                return new InternalNode(internalData.Keys, children, handle, dirty: false);
            }

            private readonly struct SplitResult {
                public SplitResult(TKey separator, Node right) {
                    Separator = separator;
                    Right = right;
                }

                public TKey Separator { get; }
                public Node Right { get; }
            }

            private abstract class Node {
                protected Node(NodeHandle? handle, bool dirty) {
                    CachedHandle = dirty ? null : handle;
                    Dirty = dirty;
                }

                public NodeHandle? CachedHandle { get; private set; }
                public bool Dirty { get; private set; }
                public abstract bool IsLeaf { get; }
                public abstract int KeyCount { get; }

                public void MarkDirty() {
                    Dirty = true;
                    CachedHandle = null;
                }

                public void MarkClean(NodeHandle handle) {
                    CachedHandle = handle;
                    Dirty = false;
                }
            }

            private sealed class LeafNode : Node {
                public LeafNode()
                    : this(null, null) {
                }

                public LeafNode(IEnumerable<TKey>? keys, IEnumerable<TValue>? values, NodeHandle? handle = null, bool dirty = true)
                    : base(handle, dirty) {
                    Keys = keys is null ? new List<TKey>() : new List<TKey>(keys);
                    Values = values is null ? new List<TValue>() : new List<TValue>(values);
                    if (Keys.Count != Values.Count) {
                        throw new InvalidOperationException("A leaf node must contain the same number of keys and values.");
                    }
                }

                public List<TKey> Keys { get; }
                public List<TValue> Values { get; }
                public override bool IsLeaf => true;
                public override int KeyCount => Keys.Count;
            }

            private sealed class InternalNode : Node {
                public InternalNode()
                    : this(null, null) {
                }

                public InternalNode(IEnumerable<TKey>? keys, IEnumerable<Node>? children, NodeHandle? handle = null, bool dirty = true)
                    : base(handle, dirty) {
                    Keys = keys is null ? new List<TKey>() : new List<TKey>(keys);
                    Children = children is null ? new List<Node>() : new List<Node>(children);
                    if (Children.Count != 0 && Children.Count != Keys.Count + 1) {
                        throw new InvalidOperationException("An internal node must maintain fanout children equal to keys + 1.");
                    }
                }

                public List<TKey> Keys { get; }
                public List<Node> Children { get; }
                public override bool IsLeaf => false;
                public override int KeyCount => Keys.Count;
            }
        }
    }
}
