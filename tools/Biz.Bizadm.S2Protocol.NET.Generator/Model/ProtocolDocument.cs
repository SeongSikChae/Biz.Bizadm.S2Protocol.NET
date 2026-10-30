using System.Text.Json;
using System.Text.RegularExpressions;

namespace Biz.Bizadm.S2Protocol.NET.Generator.Model;

// json/protocolNNNNN.json의 decl 사전. TypeDecl·ConstDecl은 fullname으로, enum 멤버는 멤버 fullname으로 찾는다.
internal sealed partial class ProtocolDocument : IDisposable
{
    private readonly JsonDocument _document;

    private ProtocolDocument(int build, JsonDocument document)
    {
        Build = build;
        _document = document;
        foreach (var module in document.RootElement.GetProperty("modules").EnumerateArray())
            CollectDecls(module);
        CollectEnumMembers(document.RootElement);
    }

    public int Build { get; }

    public Dictionary<string, JsonElement> Decls { get; } = [];

    public Dictionary<string, JsonElement> EnumMembers { get; } = [];

    // 원본 일부(구버전 23개)에 JSON 비표준 이스케이프 \x1b가 있어 \u001b로 바꾼 뒤 파싱한다.
    [GeneratedRegex(@"(?<!\\)((?:\\\\)*)\\x([0-9A-Fa-f]{2})")]
    private static partial Regex HexEscape();

    public static ProtocolDocument Load(string path, int build)
    {
        string text = HexEscape().Replace(File.ReadAllText(path), @"$1\u00$2");
        return new ProtocolDocument(build, JsonDocument.Parse(text));
    }

    public JsonElement TypeInfoOf(string fullName)
    {
        if (!Decls.TryGetValue(fullName, out var decl) || decl.GetProperty("type").GetString() != "TypeDecl")
            throw new KeyNotFoundException($"{Build}: TypeDecl '{fullName}' not found.");
        return decl.GetProperty("type_info");
    }

    public void Dispose() => _document.Dispose();

    private void CollectDecls(JsonElement module)
    {
        foreach (var decl in module.GetProperty("decls").EnumerateArray())
        {
            if (decl.GetProperty("type").GetString() == "Module")
                CollectDecls(decl);
            else
                Decls[decl.GetProperty("fullname").GetString()!] = decl;
        }
    }

    // enum은 struct 필드 안에 인라인으로도 정의되므로 트리 전체에서 멤버를 모은다.
    private void CollectEnumMembers(JsonElement node)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                if (node.TryGetProperty("type", out var type)
                    && type.GetString() is "MemberEnumField" or "MemberInumField")
                    EnumMembers[node.GetProperty("fullname").GetString()!] = node;
                foreach (var property in node.EnumerateObject())
                    CollectEnumMembers(property.Value);
                break;
            case JsonValueKind.Array:
                foreach (var item in node.EnumerateArray())
                    CollectEnumMembers(item);
                break;
        }
    }
}
