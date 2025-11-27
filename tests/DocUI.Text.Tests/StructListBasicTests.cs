using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DocUI.Text;
using Xunit;

namespace DocUI.Text.Tests;

[CollectionDefinition("StructList.BasicTests", DisableParallelization = true)]
public sealed class StructListBasicTestsCollection { }

[Collection("StructList.BasicTests")]
public class StructListBasicTests {
    [Fact]
    public void AddAndAddRangeAppendAndTrackCapacity() {
        var list = new StructList<int>(2);

        list.Add(1);
        list.Add(2);

        Assert.Equal(2, list.Count);
        Assert.True(list.Capacity >= 2);

        var more = new[] { 3, 4, 5 };
        list.AddRange(more);

        Assert.Equal(5, list.Count);
        Assert.True(list.Capacity >= 5);
        AssertContents(list, 1, 2, 3, 4, 5);
    }

    [Fact]
    public void InsertAndInsertRangeShiftTailsCorrectly() {
        var list = new StructList<int>(4);
        list.AddRange(new[] { 0, 1, 2, 3 });

        list.Insert(2, 99);
        AssertContents(list, 0, 1, 99, 2, 3);

        list.InsertRange(1, new[] { 8, 9 });
        AssertContents(list, 0, 8, 9, 1, 99, 2, 3);
        Assert.Equal(7, list.Count);
    }

    [Fact]
    public void RemoveOperationsShrinkAndReleaseReferences() {
        AssertLiveTrackerCount(0);

        var list = new StructList<TrackingResource>(8);
        for (int i = 0; i < 5; i++) {
            AppendTracker(ref list, i);
        }

        list.RemoveAt(2);
        Assert.Equal(4, list.Count);
        Assert.Equal(3, list[2].Id);

        list.RemoveRange(1, 2);
        Assert.Equal(2, list.Count);
        Assert.Equal(0, list[0].Id);
        Assert.Equal(4, list[1].Id);

        var backing = list.BackingArray!;
        for (int i = list.Count; i < backing.Length; i++) {
            Assert.Null(backing[i]);
        }

        AssertLiveTrackerCount(list.Count);

        list.Clear();
        AssertLiveTrackerCount(0);
    }

    [Fact]
    public void PopAndTryPopOperateOnTail() {
        var list = new StructList<string>(2);
        list.Add("first");
        list.Add("second");

        var popped = list.Pop();
        Assert.Equal("second", popped);
        Assert.Equal(1, list.Count);

        Assert.True(list.TryPop(out var next));
        Assert.Equal("first", next);
        Assert.True(list.IsEmpty);

        Assert.False(list.TryPop(out var empty));
        Assert.Null(empty);
        Assert.Throws<InvalidOperationException>(() => list.Pop());

        AssertLiveTrackerCount(0);

        void RunReferenceTypeScenario() {
            var trackedList = new StructList<TrackingResource>(2);
            AppendTracker(ref trackedList, 10);
            AppendTracker(ref trackedList, 11);

            var poppedTracker = trackedList.Pop();
            Assert.Equal(11, poppedTracker.Id);

            var backing = trackedList.BackingArray!;
            for (int i = trackedList.Count; i < backing.Length; i++) {
                Assert.Null(backing[i]);
            }

            Assert.True(trackedList.TryPop(out var lastTracker));
            Assert.Equal(10, lastTracker.Id);

            backing = trackedList.BackingArray!;
            for (int i = trackedList.Count; i < backing.Length; i++) {
                Assert.Null(backing[i]);
            }
            Assert.True(trackedList.IsEmpty);
        }

        RunReferenceTypeScenario();
        AssertLiveTrackerCount(0);
    }

    [Fact]
    public void ClearResetsCountAndKeepsCapacityWhileReleasingReferences() {
        AssertLiveTrackerCount(0);

        Assert.True(RuntimeHelpers.IsReferenceOrContainsReferences<TrackingResource>());

        var list = new StructList<TrackingResource>(4);
        for (int i = 0; i < 3; i++) {
            AppendTracker(ref list, i);
        }

        var capacityBefore = list.Capacity;
        list.Clear();

        var clearedBacking = list.BackingArray!;
        for (int i = 0; i < clearedBacking.Length; i++) {
            Assert.Null(clearedBacking[i]);
        }

        Assert.Equal(0, list.Count);
        Assert.Equal(capacityBefore, list.Capacity);
        AssertLiveTrackerCount(0);
    }

    [Fact]
    public void ResetSwapsBackingArraysAndReturnsOriginalData() {
        var initialBuffer = new int[4];
        var list = new StructList<int>(initialBuffer);
        var original = new[] { 1, 2, 3 };
        list.AddRange(original);

        var newBuffer = new int[2];
        var returned = list.Reset(newBuffer);

        Assert.Same(initialBuffer, returned);
        Assert.Equal(original, returned.AsSpan(0, original.Length).ToArray());
        Assert.Equal(0, list.Count);
        Assert.Same(newBuffer, list.BackingArray);
        Assert.Equal(newBuffer.Length, list.Capacity);
    }

    [Fact]
    public void ResetClearsReferenceSlotsBeforeReturningBuffer() {
        AssertLiveTrackerCount(0);

        var initialBuffer = new TrackingResource[4];
        var list = new StructList<TrackingResource>(initialBuffer);
        for (int i = 0; i < 3; i++) {
            AppendTracker(ref list, i);
        }

        var replacement = new TrackingResource[2];
        var returned = list.Reset(replacement);

        Assert.Same(initialBuffer, returned);
        var cleared = returned!;
        for (int i = 0; i < cleared.Length; i++) {
            Assert.Null(cleared[i]);
        }

        Assert.Equal(0, list.Count);
        Assert.Same(replacement, list.BackingArray);
        Assert.Equal(replacement.Length, list.Capacity);
        AssertLiveTrackerCount(0);
    }

    [Fact]
    public void DetachReturnsArrayAndResetsState() {
        var list = new StructList<int>(4);
        list.AddRange(new[] { 5, 6, 7 });
        var capacityBefore = list.Capacity;

        var detached = list.Detach(out var count);
        Assert.NotNull(detached);
        var backing = detached!;

        Assert.Equal(3, count);
        Assert.Equal(new[] { 5, 6, 7 }, backing.AsSpan(0, count).ToArray());
        Assert.Equal(capacityBefore, backing.Length);
        Assert.Equal(0, list.Count);
        Assert.Equal(0, list.Capacity);
        Assert.Same(Array.Empty<int>(), list.BackingArray);
    }

    private static void AssertContents<T>(StructList<T> list, params T[] expected) {
        Assert.Equal(expected, list.AsReadOnlySpan().ToArray());
    }

    private static void AssertLiveTrackerCount(int expected) {
        TrackingResource.ForceCollection();
        var liveIds = TrackingResource.GetLiveIds();
        var actual = liveIds.Length;
        Assert.True(actual == expected, $"Expected {expected} live trackers but found {actual}: [{string.Join(", ", liveIds)}]");
    }

    private static void AppendTracker(ref StructList<TrackingResource> list, int id) {
        var tracker = new TrackingResource(id);
        list.Add(tracker);
    }

    private sealed class TrackingResource {
        private static readonly object _sync = new();
        private static readonly List<WeakReference<TrackingResource>> _references = new();

        public int Id { get; }

        public TrackingResource(int id) {
            Id = id;
            lock (_sync) {
                _references.Add(new WeakReference<TrackingResource>(this));
            }
        }

        public static int LiveInstances => GetLiveIds().Length;

        public static void ForceCollection() {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        public static int[] GetLiveIds() {
            lock (_sync) {
                if (_references.Count == 0) {
                    return Array.Empty<int>();
                }

                var live = new List<int>(_references.Count);
                for (int i = _references.Count - 1; i >= 0; i--) {
                    if (_references[i].TryGetTarget(out var target)) {
                        live.Add(target.Id);
                    } else {
                        _references.RemoveAt(i);
                    }
                }
                return live.ToArray();
            }
        }
    }
}
