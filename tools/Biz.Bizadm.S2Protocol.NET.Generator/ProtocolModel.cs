using Biz.Bizadm.S2Protocol.NET.Generator.Layout;
using Biz.Bizadm.S2Protocol.NET.Generator.Model;

namespace Biz.Bizadm.S2Protocol.NET.Generator;

// 한 버전의 json·py를 읽고 도달 분석과 py typeinfos 대조까지 마친 모델. 대조가 실패하면 예외(설계 D2).
internal sealed class ProtocolModel : IDisposable
{
    private ProtocolModel(int build, ProtocolDocument document, ProtocolRoots roots, LayoutBuilder layout,
                          Reachability reach, IReadOnlyList<WireType> pyTypes, OracleComparer comparer)
    {
        Build = build;
        Document = document;
        Roots = roots;
        Layout = layout;
        Reach = reach;
        PyTypes = pyTypes;
        Comparer = comparer;
    }

    public int Build { get; }

    public ProtocolDocument Document { get; }

    public ProtocolRoots Roots { get; }

    public LayoutBuilder Layout { get; }

    public Reachability Reach { get; }

    public IReadOnlyList<WireType> PyTypes { get; }

    public OracleComparer Comparer { get; }

    public static IEnumerable<int> Builds(Upstream upstream) =>
        Directory.EnumerateFiles(Path.Combine(upstream.Directory, "json"), "protocol*.json")
            .Select(p => int.Parse(Path.GetFileNameWithoutExtension(p)["protocol".Length..]))
            .Order();

    public static ProtocolModel Load(Upstream upstream, int build)
    {
        string name = $"protocol{build:D5}";
        string pyPath = Path.Combine(upstream.Directory, "s2protocol", "versions", name + ".py");
        var document = ProtocolDocument.Load(Path.Combine(upstream.Directory, "json", name + ".json"), build);
        try
        {
            var py = PyProtocolFile.Parse(pyPath);
            var roots = ProtocolRoots.From(py);

            var evaluator = new ConstantEvaluator(document);
            var reach = new Reachability(document, evaluator);
            foreach (string fullName in roots.AllTypeNames)
                reach.Visit(fullName);

            // json에서 유도한 레이아웃을 py typeinfos와 대조한다.
            var pyTypes = PyTypeInfoParser.Parse(pyPath);
            var layout = new LayoutBuilder(document, evaluator);
            var comparer = new OracleComparer();
            foreach (var (pyName, fullName) in roots.Roots)
                comparer.Compare(layout.ForDecl(fullName), pyTypes[py.TypeIds[pyName]!.Value], fullName);
            foreach (var table in roots.EventTables.Values)
                foreach (var (typeId, typeName) in table.Values)
                    comparer.Compare(layout.ForDecl(typeName), pyTypes[typeId], typeName);
            if (comparer.Errors.Count > 0)
                throw new InvalidOperationException("layout mismatch:" + Environment.NewLine + "  "
                    + string.Join(Environment.NewLine + "  ", comparer.Errors));

            return new ProtocolModel(build, document, roots, layout, reach, pyTypes, comparer);
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    public string Summary()
    {
        string events = string.Join(" ", Roots.EventTables.Select(t => $"{t.Key}={t.Value.Count}"));
        string unsupported = string.Join(",", Roots.UnsupportedStreams());
        return $"{Build}: decls={Document.Decls.Count} reached={Reach.Reached.Count} bounds={Reach.BoundsChecked} "
             + $"enumValues={Reach.EnumValuesEvaluated} {events} pyTypes={PyTypes.Count} compared={Comparer.Compared}"
             + (unsupported.Length > 0 ? $" unsupported=[{unsupported}]" : "");
    }

    public void Dispose() => Document.Dispose();
}
