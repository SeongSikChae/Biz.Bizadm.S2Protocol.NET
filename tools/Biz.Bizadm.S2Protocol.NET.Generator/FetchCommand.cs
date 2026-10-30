using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Biz.Bizadm.S2Protocol.NET.Generator;

// 고정 커밋의 json/protocolNNNNN.json과 s2protocol/ 아래 .py 전체(골든 생성용 decoders.py 포함)를 내려받는다.
internal static partial class FetchCommand
{
    [GeneratedRegex(@"^(json/protocol\d+\.json|s2protocol/.+\.py)$")]
    private static partial Regex TargetPath();

    public static async Task<int> RunAsync(Upstream upstream)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Biz.Bizadm.S2Protocol.NET.Generator");

        var tree = await http.GetFromJsonAsync<GitTree>(
            $"https://api.github.com/repos/{upstream.Repository}/git/trees/{upstream.Commit}?recursive=1")
            ?? throw new InvalidOperationException("Empty tree response.");
        if (tree.Truncated)
            throw new InvalidOperationException("GitHub returned a truncated tree; files would be missing.");

        var paths = tree.Tree
            .Where(t => t.Type == "blob" && TargetPath().IsMatch(t.Path))
            .Select(t => t.Path)
            .ToList();
        Console.WriteLine($"{upstream.Repository}@{upstream.Commit}: {paths.Count} files → {upstream.Directory}");

        await Parallel.ForEachAsync(paths, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (path, ct) =>
        {
            byte[] bytes = await http.GetByteArrayAsync(
                $"https://raw.githubusercontent.com/{upstream.Repository}/{upstream.Commit}/{path}", ct);
            string target = Path.Combine(upstream.Directory, path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllBytesAsync(target, bytes, ct);
        });

        Console.WriteLine($"Downloaded {paths.Count} files.");
        return 0;
    }

    private sealed record GitTree(
        [property: JsonPropertyName("tree")] List<GitTreeEntry> Tree,
        [property: JsonPropertyName("truncated")] bool Truncated);

    private sealed record GitTreeEntry(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("type")] string Type);
}
