namespace Biz.Bizadm.S2Protocol.NET;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
public sealed class S2FieldAttribute(string name) : Attribute
{
    // 원본 필드명(m_...). choice 경우 클래스에는 원본 경우 이름. 골든 직렬화에 쓴다.
    public string Name { get; } = name;
}
