namespace Biz.Bizadm.S2Protocol.NET;

/// <summary>
/// Marks a generated property or choice case class with its original s2protocol name (for example <c>m_baseBuild</c>).
/// </summary>
/// <param name="name">The original field or choice case name.</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
public sealed class S2FieldAttribute(string name) : Attribute
{
    /// <summary>The original field name (<c>m_...</c>), or the original case name on a choice case class.</summary>
    // 원본 필드명(m_...). choice 경우 클래스에는 원본 경우 이름. 골든 직렬화에 쓴다.
    public string Name { get; } = name;

    /// <summary>The wire tag of a struct field. Not used for choice cases.</summary>
    // struct 필드의 와이어 태그(versioned에서 필드 존재 기록과 대응). choice 경우에는 쓰지 않는다.
    public long Tag { get; set; }
}
