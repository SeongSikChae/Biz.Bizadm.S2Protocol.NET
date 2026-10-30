using System.Text.Json;

namespace Biz.Bizadm.S2Protocol.NET.Generator;

// upstream.json(고정 커밋)과 내려받은 원본 폴더(.upstream, gitignore) 위치.
internal sealed record Upstream(string Repository, string Commit, string ProjectDirectory)
{
    public string Directory => Path.Combine(ProjectDirectory, ".upstream");

    // dotnet run의 호출 cwd와 무관하게 실행 파일 위치에서 상위로 올라가며 upstream.json을 찾는다.
    public static Upstream Load()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string path = Path.Combine(dir.FullName, "upstream.json");
            if (!File.Exists(path))
                continue;

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            return new Upstream(
                root.GetProperty("repository").GetString()!,
                root.GetProperty("commit").GetString()!,
                dir.FullName);
        }
        throw new FileNotFoundException("upstream.json was not found above " + AppContext.BaseDirectory);
    }
}
