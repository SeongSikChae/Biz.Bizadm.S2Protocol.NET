namespace Biz.Bizadm.S2Protocol.NET;

[AttributeUsage(AttributeTargets.Property)]
public sealed class S2FieldAttribute(string name) : Attribute
{
    // 원본 필드명(m_...). 골든 직렬화에 쓴다.
    public string Name { get; } = name;
}
