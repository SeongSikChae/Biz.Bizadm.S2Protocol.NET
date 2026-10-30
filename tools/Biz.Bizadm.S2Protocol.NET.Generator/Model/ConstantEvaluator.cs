using System.Numerics;
using System.Text.Json;

namespace Biz.Bizadm.S2Protocol.NET.Generator.Model;

// json 식(enum 멤버 값, bounds의 value)을 정수로 평가한다. bounds는 원본이 계산한 evalue를 쓰고,
// CheckBounds로 이 평가기와 evalue가 같은지 대조한다.
internal sealed class ConstantEvaluator(ProtocolDocument document)
{
    public BigInteger Eval(JsonElement expr) => expr.GetProperty("type").GetString() switch
    {
        "IntLiteral" => BigInteger.Parse(expr.GetProperty("value").GetString()!),
        "NegateExpr" => -Eval(expr.GetProperty("rhs")),
        "PowExpr" => BigInteger.Pow(Eval(expr.GetProperty("lhs")), (int)Eval(expr.GetProperty("rhs"))),
        "LShiftExpr" => Eval(expr.GetProperty("lhs")) << (int)Eval(expr.GetProperty("rhs")),
        "MulExpr" => Eval(expr.GetProperty("lhs")) * Eval(expr.GetProperty("rhs")),
        "OrExpr" => Eval(expr.GetProperty("lhs")) | Eval(expr.GetProperty("rhs")),
        "IdentifierExpr" => EvalIdentifier(expr.GetProperty("fullname").GetString()!),
        var type => throw new NotSupportedException($"{document.Build}: expression '{type}'."),
    };

    // 포함 범위 [Min, Max]. inclusive=false면 한 칸 안쪽으로 당긴다.
    public static (BigInteger Min, BigInteger Max) Bounds(JsonElement bounds)
    {
        var min = bounds.GetProperty("min");
        var max = bounds.GetProperty("max");
        BigInteger lo = BigInteger.Parse(min.GetProperty("evalue").GetString()!);
        BigInteger hi = BigInteger.Parse(max.GetProperty("evalue").GetString()!);
        if (!min.GetProperty("inclusive").GetBoolean()) lo += 1;
        if (!max.GetProperty("inclusive").GetBoolean()) hi -= 1;
        return (lo, hi);
    }

    public void CheckBounds(JsonElement bounds, string path)
    {
        foreach (string side in (string[])["min", "max"])
        {
            var limit = bounds.GetProperty(side);
            BigInteger expected = BigInteger.Parse(limit.GetProperty("evalue").GetString()!);
            BigInteger actual = Eval(limit.GetProperty("value"));
            if (actual != expected)
                throw new InvalidOperationException(
                    $"{document.Build}: {path} bounds.{side} evaluates to {actual} but evalue is {expected}.");
        }
    }

    private BigInteger EvalIdentifier(string fullName)
    {
        if (document.Decls.TryGetValue(fullName, out var decl) && decl.GetProperty("type").GetString() == "ConstDecl")
            return Eval(decl.GetProperty("value"));
        if (document.EnumMembers.TryGetValue(fullName, out var member))
            return Eval(member.GetProperty("value"));
        throw new KeyNotFoundException($"{document.Build}: identifier '{fullName}' not found.");
    }
}
