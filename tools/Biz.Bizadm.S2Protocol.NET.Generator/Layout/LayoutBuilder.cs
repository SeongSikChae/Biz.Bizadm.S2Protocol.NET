using System.Numerics;
using System.Text.Json;
using Biz.Bizadm.S2Protocol.NET.Generator.Model;

namespace Biz.Bizadm.S2Protocol.NET.Generator.Layout;

// json type_info → 와이어 레이아웃. 유도 규칙은 py typeinfos 대조(OracleComparer)로 검증한다.
internal sealed class LayoutBuilder(ProtocolDocument document, ConstantEvaluator evaluator)
{
    private readonly Dictionary<string, WireType> _decls = [];
    private readonly HashSet<string> _building = [];

    public WireType ForDecl(string fullName)
    {
        if (_decls.TryGetValue(fullName, out var built))
            return built;
        if (!_building.Add(fullName))
            throw new NotSupportedException($"{document.Build}: recursive type '{fullName}'.");

        var type = Build(document.TypeInfoOf(fullName), fullName);
        _building.Remove(fullName);
        return _decls[fullName] = type;
    }

    private WireType Build(JsonElement typeInfo, string? declName)
    {
        return typeInfo.GetProperty("type").GetString() switch
        {
            "IntType" => Ranged(typeInfo, (o, b) => new WInt(o, b)) with { DeclName = declName },
            "BlobType" or "AsciiStringType" => Ranged(typeInfo, (o, b) => new WBlob(o, b)) with { DeclName = declName },
            "StringType" => Utf8String(typeInfo) with { DeclName = declName },
            "BitArrayType" => Ranged(typeInfo, (o, b) => new WBitArray(o, b)) with { DeclName = declName },
            "ArrayType" or "DynArrayType" => Ranged(typeInfo, (o, b) => new WArray(o, b, Build(typeInfo.GetProperty("element_type"), null))) with { DeclName = declName },
            "OptionalType" => new WOptional(Build(typeInfo.GetProperty("type_info"), null)) { DeclName = declName },
            "BoolType" => new WBool { DeclName = declName },
            "FourCCType" => new WFourCC { DeclName = declName },
            "NullType" => new WNull { DeclName = declName },
            "EnumType" or "InumType" => Enum(typeInfo) with { DeclName = declName },
            "StructType" => Struct(typeInfo) with { DeclName = declName },
            "ChoiceType" => Choice(typeInfo) with { DeclName = declName },
            "UserType" => ForDecl(typeInfo.GetProperty("fullname").GetString()!),
            var type => throw new NotSupportedException($"{document.Build}: type_info '{type}'."),
        };
    }

    private static T Ranged<T>(JsonElement typeInfo, Func<BigInteger, int, T> create)
    {
        var (min, max) = ConstantEvaluator.Bounds(typeInfo.GetProperty("bounds"));
        return create(min, Bits(min, max));
    }

    private static int Bits(BigInteger min, BigInteger max) => (int)(max - min).GetBitLength();

    // StringType bounds는 문자 수다. 와이어에는 UTF-8 바이트 수(문자당 최대 4바이트)로 기록된다.
    private static WBlob Utf8String(JsonElement typeInfo)
    {
        var (min, max) = ConstantEvaluator.Bounds(typeInfo.GetProperty("bounds"));
        return new WBlob(min * 4, Bits(min * 4, max * 4));
    }

    // bounds가 없으면 멤버 값의 최소~최대 범위를 쓴다.
    private WInt Enum(JsonElement typeInfo)
    {
        var members = typeInfo.GetProperty("fields").EnumerateArray()
            .Select(m => new WEnumMember(m.GetProperty("name").GetString()!, evaluator.Eval(m.GetProperty("value"))))
            .ToList();
        var bounds = typeInfo.GetProperty("bounds");
        if (bounds.ValueKind != JsonValueKind.Null)
            return Ranged(typeInfo, (o, b) => new WInt(o, b)) with { Members = members };

        BigInteger min = members.Min(m => m.Value), max = members.Max(m => m.Value);
        return new WInt(min, Bits(min, max)) { Members = members };
    }

    private WStruct Struct(JsonElement typeInfo)
    {
        var fields = new List<WField>();
        foreach (var parent in typeInfo.GetProperty("parents").EnumerateArray())
            fields.Add(new WField("__parent", Build(parent.GetProperty("type_info"), null), -1));
        // 태그 없는 필드의 태그는 k - n이다. k는 실제 필드 사이의 순번, n은 ConstDecl 멤버(이벤트 struct의
        // EEVENTID 등, 와이어에 없음)까지 센 전체 수다. 즉 ConstDecl이 끝에 있는 것처럼 번호가 매겨진다.
        var members = typeInfo.GetProperty("fields").EnumerateArray().ToList();
        int k = 0;
        foreach (var field in members)
        {
            if (field.GetProperty("type").GetString() == "ConstDecl")
                continue;
            int tag = HasTag(field) ? Tag(field) : k - members.Count;
            k++;
            fields.Add(new WField(field.GetProperty("name").GetString()!, Build(field.GetProperty("type_info"), null), tag));
        }
        return new WStruct(fields);
    }

    // bounds가 없으면 경우 태그의 최소~최대 범위를 쓴다.
    private WChoice Choice(JsonElement typeInfo)
    {
        var cases = typeInfo.GetProperty("fields").EnumerateArray()
            .Select(f => new WCase(Tag(f), f.GetProperty("name").GetString()!, Build(f.GetProperty("type_info"), null)))
            .ToList();
        var bounds = typeInfo.GetProperty("bounds");
        var (min, max) = bounds.ValueKind != JsonValueKind.Null
            ? ConstantEvaluator.Bounds(bounds)
            : (cases.Min(c => c.Tag), cases.Max(c => c.Tag));
        return new WChoice(min, Bits(min, max), cases);
    }

    private static bool HasTag(JsonElement field) =>
        field.TryGetProperty("tag", out var tag) && tag.ValueKind != JsonValueKind.Null;

    private int Tag(JsonElement field) => HasTag(field) ? (int)evaluator.Eval(field.GetProperty("tag")) : -1;
}
