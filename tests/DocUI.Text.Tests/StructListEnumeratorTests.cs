using DocUI.Text;

namespace DocUI.Text.Tests;

public class StructListEnumeratorTests {
    [Fact]
    public void ModifyingListDuringEnumerationThrows() {
        var list = new StructList<int>(4);
        list.Add(1);
        list.Add(2);

        Assert.Throws<InvalidOperationException>(() => {
            foreach (ref readonly var _ in list) {
                list.Add(99);
            }
        });
    }

    [Fact]
    public void EditingCurrentElementByReferenceIsAllowed() {
        var list = new StructList<int>(4);
        list.Add(10);
        list.Add(20);

        var enumerator = list.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        ref var current = ref enumerator.Current;
        current = 42;

        Assert.Equal(42, list[0]);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(20, list[1]);
    }

    [Fact]
    public void ClearingAfterEnumeratorCreationInvalidatesEnumeration() {
        var list = new StructList<int>(4);
        list.Add(1);
        list.Add(2);

        var enumerator = list.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        list.Clear();

        var threw = false;
        try {
            enumerator.MoveNext();
        } catch (InvalidOperationException) {
            threw = true;
        }

        Assert.True(threw);
    }

    [Fact]
    public void CurrentBeforeMoveNextThrows() {
        var list = new StructList<int>(2);
        list.Add(5);

        var enumerator = list.GetEnumerator();

        AssertCurrentThrows(ref enumerator);
    }

    [Fact]
    public void CurrentAfterEnumerationCompletesThrows() {
        var list = new StructList<int>(2);
        list.Add(7);

        var enumerator = list.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());

        AssertCurrentThrows(ref enumerator);
    }

    [Fact]
    public void CurrentThrowsWhenListMutatesAfterMoveNext() {
        var list = new StructList<int>(4);
        list.Add(1);
        list.Add(2);

        var enumerator = list.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        list.RemoveAt(0);

        AssertCurrentThrows(ref enumerator);
    }

    [Fact]
    public void DefaultStructListEnumeratesAsEmpty() {
        StructList<int> list = default;

        var enumerator = list.GetEnumerator();

        Assert.False(enumerator.MoveNext());
    }

    private static void AssertCurrentThrows<T>(ref StructList<T>.Enumerator enumerator) {
        var threw = false;
        try {
            var _ = enumerator.Current;
        } catch (InvalidOperationException) {
            threw = true;
        }

        Assert.True(threw);
    }
}
