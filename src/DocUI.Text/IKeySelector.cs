namespace DocUI.Text;

/// <summary>
/// 零开销键选择器接口，用于 <see cref="StructList{T}.BinarySearchBy{TKey, TSelector}"/> 等泛型特化场景。
/// </summary>
/// <typeparam name="T">元素类型。</typeparam>
/// <typeparam name="TKey">键类型。</typeparam>
/// <remarks>
/// 使用 static abstract interface members (C# 11+) 实现零开销抽象。
/// 调用方定义实现此接口的 struct，JIT 会为每个具体类型生成特化代码，
/// 静态方法调用可被内联，无虚调用开销。
/// <example>
/// <code>
/// readonly struct OffsetSelector : IKeySelector&lt;LineSegment, int&gt; {
///     public static int GetKey(in LineSegment item) => item.Offset;
/// }
///
/// // 使用
/// int index = segments.BinarySearchBy&lt;int, OffsetSelector&gt;(targetOffset);
/// </code>
/// </example>
/// </remarks>
public interface IKeySelector<T, TKey> {
    /// <summary>
    /// 从元素中提取用于比较的键。
    /// </summary>
    /// <param name="item">元素引用（in 避免大结构体拷贝）。</param>
    /// <returns>用于比较的键值。</returns>
    static abstract TKey GetKey(in T item);
}
