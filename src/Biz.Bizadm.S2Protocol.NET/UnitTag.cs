namespace Biz.Bizadm.S2Protocol.NET;

/// <summary>Unit tag helpers, equivalent to s2protocol <c>unit_tag</c>, <c>unit_tag_index</c> and <c>unit_tag_recycle</c>.</summary>
// 태그는 이벤트의 32비트 부호 없는 필드(생성 타입에서 long)와 같은 범위다.
public static class UnitTag
{
    /// <summary>Builds a unit tag from its index and recycle counter.</summary>
    /// <param name="index">The unit index.</param>
    /// <param name="recycle">The recycle counter.</param>
    /// <returns><c>(index &lt;&lt; 18) + recycle</c>.</returns>
    public static long Create(int index, int recycle) => ((long)index << 18) + recycle;

    /// <summary>Gets the unit index of a unit tag.</summary>
    /// <param name="tag">The unit tag.</param>
    /// <returns>The index.</returns>
    public static int Index(long tag) => (int)((tag >> 18) & 0x3fff);

    /// <summary>Gets the recycle counter of a unit tag.</summary>
    /// <param name="tag">The unit tag.</param>
    /// <returns>The recycle counter.</returns>
    public static int Recycle(long tag) => (int)(tag & 0x3ffff);
}
