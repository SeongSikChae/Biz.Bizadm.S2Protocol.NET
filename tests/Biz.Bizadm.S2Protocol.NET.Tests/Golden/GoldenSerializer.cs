using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Biz.Bizadm.S2Protocol.NET.Attributes;
using Biz.Bizadm.S2Protocol.NET.Events;

namespace Biz.Bizadm.S2Protocol.NET.Tests.Golden;

// .NET 디코딩 결과 → tools/golden/make_golden.py(Python s2protocol)가 쓰는 JSON 모양.
// struct는 [S2Field] 원본 이름, versioned로 읽은 struct는 PresentTags에 있는 필드만(설계 D10),
// choice는 {경우 이름: 값}, enum은 정수, bytes·FourCC는 {"$b64": ...},
// bitarray는 bitpacked [길이, 정수] / versioned [길이, {"$b64"}].
internal static class GoldenSerializer
{
    public static JsonNode? ToNode(object? value, bool versioned) => value switch
    {
        null => null,
        bool b => JsonValue.Create(b),
        int or long or short or byte or uint or ushort or sbyte => JsonValue.Create(Convert.ToInt64(value)),
        ulong u => JsonValue.Create(u),
        Enum e => JsonValue.Create(Convert.ToInt64(e)),
        byte[] bytes => Base64(bytes),
        FourCC f => Base64([(byte)(f.Value >> 24), (byte)(f.Value >> 16), (byte)(f.Value >> 8), (byte)f.Value]),
        S2BitArray bits => new JsonArray(
            JsonValue.Create(bits.Length),
            versioned ? Base64(bits.Data) : Number(new BigInteger(bits.Data, isUnsigned: true, isBigEndian: true))),
        System.Collections.IList list => new JsonArray([.. list.Cast<object?>().Select(item => ToNode(item, versioned))]),
        _ => Composite(value, versioned),
    };

    public static JsonObject Event(ReplayEvent ev, bool versioned)
    {
        var node = (JsonObject)ToNode(ev.Body, versioned)!;
        node["_event"] = ev.EventName;
        node["_eventid"] = ev.EventId;
        node["_gameloop"] = ev.GameLoop;
        node["_bits"] = ev.Bits;
        int? userId = ev switch { GameEvent g => g.UserId, MessageEvent m => m.UserId, _ => null };
        if (userId is not null)
            node["_userid"] = new JsonObject { ["m_userId"] = userId.Value };   // Python은 SGameUserId dict 그대로
        return node;
    }

    public static JsonObject Attributes(ReplayAttributes attributes)
    {
        var node = new JsonObject();
        if (attributes.Source is null)
            return node;   // Python {}
        node["source"] = attributes.Source.Value;
        node["mapNamespace"] = attributes.MapNamespace!.Value;
        var scopes = new JsonObject();
        foreach (var (scope, byId) in attributes.Scopes)
        {
            var ids = new JsonObject();
            foreach (var (attrId, values) in byId)
                ids[attrId.ToString()] = new JsonArray([.. values.Select(v => (JsonNode)new JsonObject
                {
                    ["namespace"] = v.Namespace,
                    ["attrid"] = v.AttrId,
                    ["value"] = Base64(v.Value),
                })]);
            scopes[scope.ToString()] = ids;
        }
        node["scopes"] = scopes;
        return node;
    }

    private static JsonNode? Composite(object value, bool versioned)
    {
        var type = value.GetType();

        // choice 경우 클래스: 클래스에 [S2Field("경우 이름")]
        if (type.GetCustomAttribute<S2FieldAttribute>() is { } caseField)
            return new JsonObject { [caseField.Name] = ToNode(type.GetProperty("Value")?.GetValue(value), versioned) };
        if (type.Name == "UnknownCase")
            return new JsonObject();   // Python은 모르는 choice 태그에 {}

        var present = (HashSet<long>?)type.GetProperty("PresentTags", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(value);
        var declared = type.GetProperties()
            .Select(p => (Property: p, Field: p.GetCustomAttribute<S2FieldAttribute>()))
            .Where(x => x.Field is not null)
            .ToList();
        var fields = declared.Where(x => present is null || present.Contains(x.Field!.Tag)).ToList();

        var node = new JsonObject();
        foreach (var (property, field) in fields)
        {
            var child = ToNode(property.GetValue(value), versioned);
            if (field!.Name != "__parent")
            {
                node[field.Name] = child;
                continue;
            }
            // Python _struct: 부모가 dict면 병합, typeinfo 필드가 부모뿐이면(len(fields) == 1) 결과 전체가 부모 값
            if (child is JsonObject parent)
                foreach (var (key, item) in parent.ToList())
                {
                    parent.Remove(key);
                    node[key] = item;
                }
            else if (declared.Count == 1)
                return child;
            else
                node[field.Name] = child;
        }
        return node;
    }

    private static JsonObject Base64(byte[] bytes) => new() { ["$b64"] = Convert.ToBase64String(bytes) };

    private static JsonNode Number(BigInteger value) => JsonNode.Parse(value.ToString())!;
}

// 우리 JsonNode와 Python 골든 JsonElement를 비교해 처음 다른 경로를 돌려준다(같으면 null).
// 숫자는 원문 텍스트로 비교한다(64비트를 넘는 정수 포함).
internal static class GoldenComparer
{
    public static string? FirstDifference(JsonNode? actual, JsonElement expected, string path = "$")
    {
        switch (expected.ValueKind)
        {
            case JsonValueKind.Null:
                return actual is null ? null : $"{path}: expected null, got {actual.ToJsonString()}";
            case JsonValueKind.Object:
                if (actual is not JsonObject obj)
                    return $"{path}: expected object, got {actual?.ToJsonString() ?? "null"}";
                var keys = expected.EnumerateObject().Select(p => p.Name).ToHashSet();
                var extra = obj.Select(p => p.Key).Where(k => !keys.Contains(k)).ToList();
                if (extra.Count > 0)
                    return $"{path}: unexpected keys {string.Join(",", extra)}";
                foreach (var property in expected.EnumerateObject())
                {
                    if (!obj.ContainsKey(property.Name))
                        return $"{path}: missing key {property.Name}";
                    if (FirstDifference(obj[property.Name], property.Value, $"{path}.{property.Name}") is { } diff)
                        return diff;
                }
                return null;
            case JsonValueKind.Array:
                if (actual is not JsonArray array)
                    return $"{path}: expected array, got {actual?.ToJsonString() ?? "null"}";
                if (array.Count != expected.GetArrayLength())
                    return $"{path}: length {array.Count} vs {expected.GetArrayLength()}";
                int i = 0;
                foreach (var item in expected.EnumerateArray())
                {
                    if (FirstDifference(array[i], item, $"{path}[{i}]") is { } diff)
                        return diff;
                    i++;
                }
                return null;
            case JsonValueKind.String:
                // JsonNode는 '+' 등을 +로 이스케이프하므로 원문이 아니라 값으로 비교한다.
                string? text = actual is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
                return text == expected.GetString() ? null : $"{path}: expected \"{expected.GetString()}\", got {actual?.ToJsonString() ?? "null"}";
            default:
                string want = expected.GetRawText();
                string got = actual?.ToJsonString() ?? "null";
                return want == got ? null : $"{path}: expected {want}, got {got}";
        }
    }
}
