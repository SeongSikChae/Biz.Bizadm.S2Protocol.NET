using Biz.Bizadm.S2Protocol.NET.Generator.Emit;

namespace Biz.Bizadm.S2Protocol.NET.Generator;

// py 대조를 통과한 버전만 src/Biz.Bizadm.S2Protocol.NET/Generated/Versions/ProtocolNNNNN/ProtocolNNNNN.g.cs로 출력한다.
internal static class EmitCommand
{
    public static int Run(Upstream upstream, int? build)
    {
        var builds = ProtocolModel.Builds(upstream).Where(b => build is null || b == build).ToList();
        if (builds.Count == 0)
        {
            Console.Error.WriteLine(build is null ? "No protocol files. Run fetch first." : $"Build {build} not found.");
            return 1;
        }

        string outputRoot = Path.GetFullPath(Path.Combine(
            upstream.ProjectDirectory, "..", "..", "src", "Biz.Bizadm.S2Protocol.NET", "Generated", "Versions"));
        int failed = 0;
        foreach (int b in builds)
        {
            try
            {
                using var model = ProtocolModel.Load(upstream, b);
                var emitter = new VersionEmitter(model);
                string code = emitter.Emit();
                string directory = Path.Combine(outputRoot, $"Protocol{b:D5}");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, $"Protocol{b:D5}.g.cs");
                File.WriteAllText(path, code);
                Console.WriteLine($"{b}: types={emitter.TypeCount} → {path}");
            }
            catch (Exception e)
            {
                failed++;
                Console.WriteLine($"{b}: FAILED {e.GetType().Name}: {e.Message}");
            }
        }
        Console.WriteLine($"emitted {builds.Count - failed}/{builds.Count}");
        return failed == 0 ? 0 : 1;
    }
}
