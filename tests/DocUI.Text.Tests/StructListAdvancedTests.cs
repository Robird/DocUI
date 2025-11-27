using System;
using System.Collections.Generic;
using DocUI.Text;
using Xunit;

namespace DocUI.Text.Tests;

[Collection("StructList.BasicTests")]
public class StructListAdvancedTests {
    [Fact]
    public void EnsureCapacityKeepsCountStableAndTrimExcessShrinksStorage() {
        var list = new StructList<int>(2);
        list.AddRange(new[] { 1, 2, 3, 4 });

        var countBeforeEnsure = list.Count;
        var requestedCapacity = list.Capacity + 8;

        list.EnsureCapacity(requestedCapacity);
        var capacityAfterEnsure = list.Capacity;

        Assert.Equal(countBeforeEnsure, list.Count);
        Assert.True(capacityAfterEnsure >= requestedCapacity);

        list.RemoveRange(0, list.Count - 1); // leave one item so utilization stays low
        var countBeforeTrim = list.Count;
        Assert.True(countBeforeTrim < capacityAfterEnsure);

        list.TrimExcess();

        Assert.Equal(countBeforeTrim, list.Count);
        Assert.True(list.Count <= list.Capacity);
        Assert.Equal(list.Count, list.Capacity);
        Assert.True(list.Capacity < capacityAfterEnsure);
    }

    [Fact]
    public void SetUpdatesInPlaceAndSpanViewsMatchListOrder() {
        var list = new StructList<string>(4);
        list.AddRange(new[] { "alpha", "beta", "gamma" });

        list.Set(1, "BETA");
        list.Set(2, "GAMMA");

        var expected = new[] { "alpha", "BETA", "GAMMA" };

        Assert.Equal(expected, list.AsSpan().ToArray());
        Assert.Equal(expected, list.AsReadOnlySpan().ToArray());
        Assert.True(list.AsSpan().SequenceEqual(list.AsReadOnlySpan()));

        for (int i = 0; i < expected.Length; i++) {
            Assert.Equal(expected[i], list[i]);
        }
    }

    [Fact]
    public void BinarySearchAndBinarySearchByReturnIndicesOrInsertionPoints() {
        var ints = new StructList<int>(4);
        ints.AddRange(new[] { 2, 4, 6, 8, 10 });

        var foundIndex = ints.BinarySearch(0, ints.Count, 6);
        Assert.Equal(2, foundIndex);

        var missingIndex = ints.BinarySearch(5);
        Assert.Equal(~2, missingIndex);

        var offsetHit = ints.BinarySearch(1, 3, 8);
        Assert.Equal(3, offsetHit);

        var constrainedMiss = ints.BinarySearch(2, 2, 2);
        Assert.Equal(~2, constrainedMiss);

        Assert.Throws<ArgumentOutOfRangeException>(() => ints.BinarySearch(0, ints.Count + 1, 6));

        var descending = new StructList<int>(4);
        descending.AddRange(new[] { 10, 8, 6, 4, 2 });
        var descendingComparer = Comparer<int>.Create((a, b) => b.CompareTo(a));

        var descendingIndex = descending.BinarySearch(0, descending.Count, 8, descendingComparer);
        Assert.Equal(1, descendingIndex);

        var descendingMissing = descending.BinarySearch(0, descending.Count, 5, descendingComparer);
        Assert.Equal(~3, descendingMissing);

        var items = new StructList<SearchItem>(4);
        items.AddRange(new[] {
            new SearchItem(10, "ten"),
            new SearchItem(20, "twenty"),
            new SearchItem(30, "thirty"),
        });

        var match = items.BinarySearchBy<int, SearchItemIdSelector>(20);
        Assert.Equal(1, match);

        var insertionPoint = items.BinarySearchBy<int, SearchItemIdSelector>(25);
        Assert.Equal(~2, insertionPoint);

        var beforeFirst = items.BinarySearchBy<int, SearchItemIdSelector>(5);
        Assert.Equal(~0, beforeFirst);

        var afterLast = items.BinarySearchBy<int, SearchItemIdSelector>(35);
        Assert.Equal(~items.Count, afterLast);
    }

    private readonly struct SearchItem {
        public SearchItem(int id, string name) {
            Id = id;
            Name = name;
        }

        public int Id { get; }
        public string Name { get; }
    }

    private readonly struct SearchItemIdSelector : IKeySelector<SearchItem, int> {
        public static int GetKey(in SearchItem item) => item.Id;
    }
}
