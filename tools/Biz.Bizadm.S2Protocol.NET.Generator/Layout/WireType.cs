using System.Numerics;

namespace Biz.Bizadm.S2Protocol.NET.Generator.Layout;

// 와이어 레이아웃. s2protocol typeinfos의 종류와 1:1이다(_int, _blob, _struct ...).
// DeclName은 json에서 만든 쪽에만 있으며(코드 출력용) 대조에는 쓰지 않는다.
internal abstract record WireType
{
    public string? DeclName { get; init; }
}

internal sealed record WInt(BigInteger Offset, int Bits) : WireType
{
    // json EnumType/InumType에서 온 경우의 멤버(코드 출력용, 대조에는 쓰지 않음).
    public IReadOnlyList<WEnumMember>? Members { get; init; }
}

internal sealed record WEnumMember(string Name, BigInteger Value);

internal sealed record WBlob(BigInteger Offset, int Bits) : WireType;

internal sealed record WBitArray(BigInteger Offset, int Bits) : WireType;

internal sealed record WArray(BigInteger Offset, int Bits, WireType Element) : WireType;

internal sealed record WOptional(WireType Element) : WireType;

internal sealed record WBool : WireType;

internal sealed record WFourCC : WireType;

internal sealed record WNull : WireType;

internal sealed record WField(string Name, WireType Type, int Tag);

internal sealed record WStruct(IReadOnlyList<WField> Fields) : WireType;

internal sealed record WCase(int Tag, string Name, WireType Type);

internal sealed record WChoice(BigInteger Offset, int Bits, IReadOnlyList<WCase> Cases) : WireType;
