using Biz.Bizadm.S2Protocol.NET.Generator.Layout;

namespace Biz.Bizadm.S2Protocol.NET.Generator.Emit;

[Flags]
internal enum Codecs
{
    None = 0,
    BitPacked = 1,
    Versioned = 2,
}

// 출력할 C# 타입. 이름 있는 decl은 네임스페이스 최상위, 인라인 struct/choice는 소유 타입의 중첩 타입이다.
internal sealed class EmittedType(WireType type, string name, string? @namespace, EmittedType? owner)
{
    public WireType Type { get; } = type;

    public string Name { get; } = name;

    public string? Namespace { get; } = @namespace;

    public EmittedType? Owner { get; } = owner;

    public string FullName => Owner is null ? $"global::{Namespace}.{Name}" : $"{Owner.FullName}.{Name}";

    public Codecs Codecs { get; set; }

    public List<EmittedType> Nested { get; } = [];
}

// 루트에서 출발해 타입마다 쓰이는 코덱(bitpacked/versioned)을 표시하고, 출력할 타입을 모은다.
internal sealed class TypeCatalog(string rootNamespace)
{
    private readonly Dictionary<WireType, EmittedType> _types = new(ReferenceEqualityComparer.Instance);

    public List<EmittedType> TopLevel { get; } = [];

    public EmittedType Get(WireType type) => _types[type];

    public void Mark(WireType type, Codecs codec) => Mark(type, codec, null, "");

    private void Mark(WireType type, Codecs codec, EmittedType? owner, string member)
    {
        switch (type)
        {
            case WArray array:
                Mark(array.Element, codec, owner, member);
                break;
            case WOptional optional:
                Mark(optional.Element, codec, owner, member);
                break;
            case WInt { Members: not null, DeclName: not null } enumType:
                Register(enumType, owner, member, "");
                break;
            case WStruct structType:
            {
                var emitted = Register(structType, owner, member, "Struct");
                if ((emitted.Codecs & codec) != 0)
                    return;
                emitted.Codecs |= codec;
                foreach (var field in structType.Fields)
                    Mark(field.Type, codec, emitted, ClrNames.Member(field.Name));
                break;
            }
            case WChoice choice:
            {
                var emitted = Register(choice, owner, member, "Choice");
                if ((emitted.Codecs & codec) != 0)
                    return;
                emitted.Codecs |= codec;
                foreach (var @case in choice.Cases)
                    Mark(@case.Type, codec, emitted, ClrNames.Member(@case.Name));
                break;
            }
        }
    }

    private EmittedType Register(WireType type, EmittedType? owner, string member, string suffix)
    {
        if (_types.TryGetValue(type, out var existing))
            return existing;

        EmittedType emitted;
        if (type.DeclName is not null)
        {
            emitted = new EmittedType(type, ClrNames.TypeName(type.DeclName), ClrNames.Namespace(rootNamespace, type.DeclName), null);
            TopLevel.Add(emitted);
        }
        else
        {
            if (owner is null)
                throw new NotSupportedException("Anonymous root type.");
            emitted = new EmittedType(type, member + suffix, null, owner);
            owner.Nested.Add(emitted);
        }
        return _types[type] = emitted;
    }
}
