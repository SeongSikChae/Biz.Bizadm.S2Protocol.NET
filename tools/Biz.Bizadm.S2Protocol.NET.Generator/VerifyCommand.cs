namespace Biz.Bizadm.S2Protocol.NET.Generator;

// 버전마다 json 모델을 읽고 루트·이벤트 타입에서 도달 가능한 decl을 모은 뒤 py typeinfos와 대조한다.
// 미지원 노드, bounds 평가 불일치, 없는 루트, 레이아웃 불일치가 있으면 실패한다.
internal static class VerifyCommand
{
    public static int Run(Upstream upstream, int? build)
    {
        var builds = ProtocolModel.Builds(upstream).Where(b => build is null || b == build).ToList();
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
                using var model = ProtocolModel.Load(upstream, b);
                Console.WriteLine(model.Summary());
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
}
