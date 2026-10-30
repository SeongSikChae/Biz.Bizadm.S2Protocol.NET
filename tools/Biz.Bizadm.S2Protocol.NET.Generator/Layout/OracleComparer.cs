namespace Biz.Bizadm.S2Protocol.NET.Generator.Layout;

// json에서 유도한 레이아웃과 py typeinfos를 구조 비교한다. 같은 (json 노드, py 노드) 쌍은 한 번만 본다.
internal sealed class OracleComparer
{
    private const int MaxErrors = 20;
    private readonly HashSet<(WireType, WireType)> _visited = new(PairComparer.Instance);

    public List<string> Errors { get; } = [];

    public int Compared => _visited.Count;

    public void Compare(WireType json, WireType py, string path)
    {
        if (Errors.Count >= MaxErrors || !_visited.Add((json, py)))
            return;

        switch (json, py)
        {
            case (WInt a, WInt b):
                Range(path, a.Offset, a.Bits, b.Offset, b.Bits);
                break;
            case (WBlob a, WBlob b):
                Range(path, a.Offset, a.Bits, b.Offset, b.Bits);
                break;
            case (WBitArray a, WBitArray b):
                Range(path, a.Offset, a.Bits, b.Offset, b.Bits);
                break;
            case (WArray a, WArray b):
                Range(path, a.Offset, a.Bits, b.Offset, b.Bits);
                Compare(a.Element, b.Element, path + "[]");
                break;
            case (WOptional a, WOptional b):
                Compare(a.Element, b.Element, path + "?");
                break;
            case (WBool, WBool) or (WFourCC, WFourCC) or (WNull, WNull):
                break;
            case (WStruct a, WStruct b):
                if (a.Fields.Count != b.Fields.Count)
                {
                    Fail(path, $"struct fields json=[{Names(a.Fields)}] py=[{Names(b.Fields)}]");
                    break;
                }
                for (int i = 0; i < a.Fields.Count; i++)
                {
                    var (fa, fb) = (a.Fields[i], b.Fields[i]);
                    if (fa.Name != fb.Name || fa.Tag != fb.Tag)
                        Fail(path, $"field #{i} json=({fa.Name},{fa.Tag}) py=({fb.Name},{fb.Tag})");
                    else
                        Compare(fa.Type, fb.Type, path + "." + fa.Name);
                }
                break;
            case (WChoice a, WChoice b):
                Range(path, a.Offset, a.Bits, b.Offset, b.Bits);
                var cases = b.Cases.ToDictionary(c => c.Tag);
                if (a.Cases.Count != b.Cases.Count)
                    Fail(path, $"choice cases json={a.Cases.Count} py={b.Cases.Count}");
                foreach (var ca in a.Cases)
                {
                    if (!cases.TryGetValue(ca.Tag, out var cb) || cb.Name != ca.Name)
                        Fail(path, $"choice case {ca.Tag}:{ca.Name} json vs py {cb?.Name ?? "(none)"}");
                    else
                        Compare(ca.Type, cb.Type, path + "." + ca.Name);
                }
                break;
            default:
                Fail(path, $"kind json={json.GetType().Name} py={py.GetType().Name}");
                break;
        }
    }

    private void Range(string path, System.Numerics.BigInteger offsetA, int bitsA, System.Numerics.BigInteger offsetB, int bitsB)
    {
        if (offsetA != offsetB || bitsA != bitsB)
            Fail(path, $"bounds json=({offsetA},{bitsA}) py=({offsetB},{bitsB})");
    }

    private void Fail(string path, string message)
    {
        if (Errors.Count < MaxErrors)
            Errors.Add($"{path}: {message}");
    }

    private static string Names(IEnumerable<WField> fields) => string.Join(",", fields.Select(f => $"{f.Name}:{f.Tag}"));

    private sealed class PairComparer : IEqualityComparer<(WireType, WireType)>
    {
        public static readonly PairComparer Instance = new();

        public bool Equals((WireType, WireType) x, (WireType, WireType) y) =>
            ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);

        public int GetHashCode((WireType, WireType) obj) =>
            HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item1),
                             System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item2));
    }
}
