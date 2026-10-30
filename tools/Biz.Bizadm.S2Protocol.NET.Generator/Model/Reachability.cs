using System.Text.Json;

namespace Biz.Bizadm.S2Protocol.NET.Generator.Model;

// 루트 TypeDecl에서 UserType 참조를 따라 도달 가능한 decl을 모은다.
// 지나가며 모든 bounds를 평가기와 대조하고 enum 멤버 값을 평가해, 모르는 노드는 예외로 드러낸다.
internal sealed class Reachability(ProtocolDocument document, ConstantEvaluator evaluator)
{
    public HashSet<string> Reached { get; } = [];

    public int BoundsChecked { get; private set; }

    public int EnumValuesEvaluated { get; private set; }

    public void Visit(string fullName)
    {
        if (Reached.Add(fullName))
            Walk(document.TypeInfoOf(fullName), fullName);
    }

    private void Walk(JsonElement typeInfo, string path)
    {
        if (typeInfo.TryGetProperty("bounds", out var bounds) && bounds.ValueKind != JsonValueKind.Null)
        {
            evaluator.CheckBounds(bounds, path);
            BoundsChecked++;
        }

        switch (typeInfo.GetProperty("type").GetString())
        {
            case "IntType" or "BoolType" or "BlobType" or "StringType" or "AsciiStringType"
                or "FourCCType" or "BitArrayType" or "NullType":
                break;
            case "ArrayType" or "DynArrayType":
                Walk(typeInfo.GetProperty("element_type"), path + "[]");
                break;
            case "OptionalType":
                Walk(typeInfo.GetProperty("type_info"), path + "?");
                break;
            case "StructType":
                foreach (var parent in typeInfo.GetProperty("parents").EnumerateArray())
                    Walk(parent.GetProperty("type_info"), path + ".__parent");
                foreach (var field in typeInfo.GetProperty("fields").EnumerateArray())
                    Walk(field.GetProperty("type_info"), path + "." + field.GetProperty("name").GetString());
                break;
            case "ChoiceType":
                foreach (var field in typeInfo.GetProperty("fields").EnumerateArray())
                    Walk(field.GetProperty("type_info"), path + "." + field.GetProperty("name").GetString());
                break;
            case "EnumType" or "InumType":
                foreach (var member in typeInfo.GetProperty("fields").EnumerateArray())
                {
                    evaluator.Eval(member.GetProperty("value"));
                    EnumValuesEvaluated++;
                }
                break;
            case "UserType":
                Visit(typeInfo.GetProperty("fullname").GetString()!);
                break;
            case var type:
                throw new NotSupportedException($"{document.Build}: {path} has type_info '{type}'.");
        }
    }
}
