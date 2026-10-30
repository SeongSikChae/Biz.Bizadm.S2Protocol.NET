namespace Biz.Bizadm.S2Protocol.NET.Generator.Emit;

// json 이름 → C# 이름. 필드·enum 멤버는 m_/e_ 접두를 떼고 PascalCase, 타입은 json name 그대로.
internal static class ClrNames
{
    public static string Member(string wireName)
    {
        if (wireName == "__parent")
            return "Parent";
        string name = wireName.StartsWith("m_") || wireName.StartsWith("e_") ? wireName[2..] : wireName;
        name = char.ToUpperInvariant(name[0]) + name[1..];
        return char.IsAsciiDigit(name[0]) ? "_" + name : name;
    }

    // NNet.Game.SCmdEvent → (root).Game, NNet.SVersion → (root)
    public static string Namespace(string rootNamespace, string declFullName)
    {
        var parts = declFullName.Split('.');
        var middle = parts[0] == "NNet" ? parts[1..^1] : parts[..^1];
        return middle.Length == 0 ? rootNamespace : rootNamespace + "." + string.Join(".", middle);
    }

    public static string TypeName(string declFullName) => declFullName[(declFullName.LastIndexOf('.') + 1)..];
}
