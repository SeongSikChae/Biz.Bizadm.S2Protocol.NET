using Biz.Bizadm.S2Protocol.NET.Generator.Model;

namespace Biz.Bizadm.S2Protocol.NET.Generator;

// 버전마다 json 모델을 읽고 루트·이벤트 타입에서 도달 가능한 decl을 모은다.
// 미지원 노드, bounds 평가 불일치, 없는 루트가 있으면 실패한다.
internal static class VerifyCommand
{
    public static int Run(Upstream upstream, int? build)
    {
        var builds = Builds(upstream).Where(b => build is null || b == build).ToList();
        if (builds.Count == 0)
        {
            Console.Error.WriteLine(build is null ? "No protocol files. Run fetch first." : $"Build {build} not found.");
            return 1;
        }

        int failed = 0;
        foreach (int b in builds)
        {
            try
            {
                Console.WriteLine(VerifyBuild(upstream, b));
            }
            catch (Exception e)
            {
                failed++;
                Console.WriteLine($"{b}: FAILED {e.GetType().Name}: {e.Message}");
            }
        }
        Console.WriteLine($"verified {builds.Count - failed}/{builds.Count}");
        return failed == 0 ? 0 : 1;
    }

    public static IEnumerable<int> Builds(Upstream upstream) =>
        Directory.EnumerateFiles(Path.Combine(upstream.Directory, "json"), "protocol*.json")
            .Select(p => int.Parse(Path.GetFileNameWithoutExtension(p)["protocol".Length..]))
            .Order();

    private static string VerifyBuild(Upstream upstream, int build)
    {
        string name = $"protocol{build:D5}";
        using var document = ProtocolDocument.Load(Path.Combine(upstream.Directory, "json", name + ".json"), build);
        var py = PyProtocolFile.Parse(Path.Combine(upstream.Directory, "s2protocol", "versions", name + ".py"));
        var roots = ProtocolRoots.From(py);

        var reach = new Reachability(document, new ConstantEvaluator(document));
        foreach (string fullName in roots.AllTypeNames)
            reach.Visit(fullName);

        string events = string.Join(" ", roots.EventTables.Select(t => $"{t.Key}={t.Value.Count}"));
        string unsupported = string.Join(",", roots.UnsupportedStreams());
        return $"{build}: decls={document.Decls.Count} reached={reach.Reached.Count} bounds={reach.BoundsChecked} "
             + $"enumValues={reach.EnumValuesEvaluated} {events}"
             + (unsupported.Length > 0 ? $" unsupported=[{unsupported}]" : "");
    }
}
