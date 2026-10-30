using System.Text.RegularExpressions;

namespace Biz.Bizadm.S2Protocol.NET.Generator.Model;

// versions/protocolNNNNN.py에서 *_typeid 값과 이벤트 ID 표(game/message/tracker)를 읽는다.
// typeinfos 목록 파싱은 레이아웃 대조(단위 9)에서 추가한다.
internal sealed partial class PyProtocolFile
{
    public Dictionary<string, int?> TypeIds { get; } = [];

    public Dictionary<string, SortedDictionary<int, (int TypeId, string Name)>> EventTables { get; } = [];

    [GeneratedRegex(@"^(\w+_typeid) = (\d+|None)\s*$")]
    private static partial Regex TypeIdLine();

    [GeneratedRegex(@"^(game|message|tracker)_event_types = \{")]
    private static partial Regex TableStart();

    [GeneratedRegex(@"^\s+(\d+): \((\d+), '([^']+)'\),?\s*$")]
    private static partial Regex TableEntry();

    public static PyProtocolFile Parse(string path)
    {
        var file = new PyProtocolFile();
        SortedDictionary<int, (int, string)>? table = null;
        foreach (string line in File.ReadLines(path))
        {
            if (table is not null)
            {
                if (line.StartsWith('}'))
                {
                    table = null;
                    continue;
                }
                var entry = TableEntry().Match(line);
                if (!entry.Success)
                    throw new FormatException($"{path}: unexpected event table line '{line}'.");
                table[int.Parse(entry.Groups[1].Value)] = (int.Parse(entry.Groups[2].Value), entry.Groups[3].Value);
                continue;
            }

            var start = TableStart().Match(line);
            if (start.Success)
            {
                table = [];
                file.EventTables[start.Groups[1].Value] = table;
                if (line.TrimEnd().EndsWith('}'))   // 빈 표: "tracker_event_types = {}"
                    table = null;
                continue;
            }

            var typeId = TypeIdLine().Match(line);
            if (typeId.Success)
                file.TypeIds[typeId.Groups[1].Value] = typeId.Groups[2].Value == "None" ? null : int.Parse(typeId.Groups[2].Value);
        }
        return file;
    }
}
