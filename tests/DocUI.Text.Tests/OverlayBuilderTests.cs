using System;
using DocUI.Text;
using Xunit;

namespace DocUI.Text.Tests;

public class OverlayBuilderTests {
    private static SegmentListBuilder CreateBuilder(string text) {
        var builder = new SegmentListBuilder();
        if (!string.IsNullOrEmpty(text)) {
            builder.Insert(0, text.AsMemory());
        }
        return builder;
    }

    [Fact]
    public void SurroundRangeEmptyRangeKeepsPrefixBeforeSuffix() {
        var segments = CreateBuilder("AB");
        var builder = new OverlayBuilder(segments);
        builder.SurroundRange(1, 1, "<", ">");

        var result = builder.Build();
        Assert.Equal(4, result.Length); // "A<>B" without line endings = 4
    }

    [Fact]
    public void InsertAtStringOverloadThrowsOnNullContent() {
        var segments = CreateBuilder("A");
        var builder = new OverlayBuilder(segments);
        Assert.Throws<ArgumentNullException>(() => builder.InsertAt(0, (string)null!));
    }

    [Fact]
    public void InsertOrderPreservedWhenOffsetAndPriorityMatch() {
        var segments = CreateBuilder("AB");
        var builder = new OverlayBuilder(segments);
        builder.InsertAt(1, "1");
        builder.InsertAt(1, "2");
        builder.InsertAt(1, "3");

        var result = builder.Build();
        Assert.Equal(5, result.Length); // "A123B" = 5
    }

    [Fact]
    public void BuildReturnsSegmentListBuilder() {
        var segments = CreateBuilder("line1\nline2\nline3");
        var builder = new OverlayBuilder(segments);
        builder.InsertAt(0, "<");
        builder.InsertAt(builder.Length, ">");
        builder.SurroundRange(6, 11, "[", "]");

        var result = builder.Build();

        Assert.Same(segments, result);
    }

    [Fact]
    public void LineApiSupportsInsertAndSurround() {
        var segments = CreateBuilder("first line\r\nsecond\nthird");
        var builder = new OverlayBuilder(segments);

        builder.InsertAtLine(1, 0, "[");
        builder.InsertAtLine(1, 6, "]");
        builder.SurroundRangeLines(0, 0, 2, 5, "<<", ">>");

        var result = builder.Build();
        // Original: "first line" (10) + "second" (6) + "third" (5) = 21
        // Added: "[" + "]" + "<<" + ">>" = 6
        Assert.Equal(27, result.Length);
        Assert.Equal(3, builder.LineCount);
    }

    [Fact]
    public void LineApiThrowsWhenColumnOutOfRange() {
        var segments = CreateBuilder("short");
        var builder = new OverlayBuilder(segments);

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.InsertAtLine(0, 10, "x"));
    }
}
