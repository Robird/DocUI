
public class OverlayBuilder {
    private struct LineMut{
        public ReadOnlyMemory<char>[] Segments;
        // 以char为单位，不含换行符的本行总长度。
        public int Length;
        // 以char为单位，行起点在整个Text中的起始偏移，同样不含换行符长度。
        public int Offset;
    }

    private LineMut[] _lines;

}
