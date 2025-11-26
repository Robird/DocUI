// /// <summary>
// /// Core系列按C语言风格展平，不提供行级View。
// /// 排除换行符概念，从流读入数据时分行并移除换行符，最终materialize时Join添加换行符。Length, Offset计算都不包含换行符。
// /// </summary>
// public interface IReadOnlyTextCore {
//     IReadOnlyList<CoreLineInfo> LinesInfo {get;}
//     ReadOnlyMemory<char> GetMemory(int lineIndex, int segmentIndex);

//     // char总个数，包含换行符。
//     int Length{get;}
// }

// public record struct CoreLineInfo(
//     int SegmentCount,
//     int Length,
//     // 以char为单位，行起点在整个Text中的起始偏移。
//     int Offset
// );

// public interface IOverlayBuilder : IReadOnlyTextCore {
//     void Reset(IReadOnlyTextCore background);

//     void LineInsert(int offset, IReadOnlyList<ReadOnlyMemory<char>> seq);
// }
