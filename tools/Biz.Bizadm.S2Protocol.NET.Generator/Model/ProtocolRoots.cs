namespace Biz.Bizadm.S2Protocol.NET.Generator.Model;

// py *_typeid 이름 ↔ json 루트 fullname. py 값이 None이면 Python도 그 스트림을 디코딩하지 못하므로 미지원이다.
internal sealed class ProtocolRoots
{
    private static readonly (string PyName, string FullName)[] Fixed =
    [
        ("replay_header_typeid", "NNet.Replay.SHeader"),
        ("game_details_typeid", "NNet.Game.SDetails"),
        ("replay_initdata_typeid", "NNet.Replay.SInitData"),
        ("svaruint32_typeid", "NNet.SVarUint32"),
        ("replay_userid_typeid", "NNet.Replay.SGameUserId"),
        ("game_eventid_typeid", "NNet.Game.EEventId"),
        ("message_eventid_typeid", "NNet.Game.EMessageId"),
        ("tracker_eventid_typeid", "NNet.Replay.Tracker.EEventId"),
    ];

    public Dictionary<string, string> Roots { get; } = [];          // py 이름 → json fullname

    public List<string> MissingInPy { get; } = [];                  // py 값이 None인 루트

    public Dictionary<string, SortedDictionary<int, (int TypeId, string Name)>> EventTables { get; } = [];

    public IEnumerable<string> AllTypeNames =>
        Roots.Values.Concat(EventTables.Values.SelectMany(t => t.Values.Select(e => e.Name))).Distinct();

    // game/message는 userid와 eventid가, tracker는 eventid가 있어야 디코딩할 수 있다.
    public IEnumerable<string> UnsupportedStreams()
    {
        bool userId = Roots.ContainsKey("replay_userid_typeid");
        if (!userId || !Roots.ContainsKey("game_eventid_typeid")) yield return "game";
        if (!userId || !Roots.ContainsKey("message_eventid_typeid")) yield return "message";
        if (!Roots.ContainsKey("tracker_eventid_typeid")) yield return "tracker";
    }

    public static ProtocolRoots From(PyProtocolFile py)
    {
        var roots = new ProtocolRoots();
        foreach (var (pyName, fullName) in Fixed)
        {
            if (!py.TypeIds.TryGetValue(pyName, out int? typeId))
                throw new FormatException($"py has no '{pyName}'.");
            if (typeId is null)
                roots.MissingInPy.Add(pyName);
            else
                roots.Roots[pyName] = fullName;
        }
        foreach (var (stream, table) in py.EventTables)
            roots.EventTables[stream] = table;
        return roots;
    }
}
