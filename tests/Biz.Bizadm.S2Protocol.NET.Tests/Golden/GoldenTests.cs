using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Biz.Bizadm.S2Protocol.NET.Protocols;

namespace Biz.Bizadm.S2Protocol.NET.Tests.Golden;

// 단위 16: TestData/Replays의 샘플을 .NET으로 디코딩해 Python s2protocol 골든(TestData/Golden, make_golden.py)과 비교한다.
[TestClass]
public sealed class GoldenTests
{
    private static string Root => Path.Combine(AppContext.BaseDirectory, "TestData");

    public static IEnumerable<object[]> Samples =>
        Directory.Exists(Path.Combine(Root, "Golden"))
            ? Directory.GetDirectories(Path.Combine(Root, "Golden")).Select(Path.GetFileName).Order().Select(n => new object[] { n! })
            : [];

    [TestMethod]
    [DynamicData(nameof(Samples))]
    public void Sample_MatchesPythonGolden(string name)
    {
        string golden = Path.Combine(Root, "Golden", name);
        using var replay = S2Replay.Open(Path.Combine(Root, "Replays", name + ".SC2Replay"));
        Assert.Contains(replay.Protocol.BaseBuild, S2ProtocolRegistry.Builds);

        Compare(GoldenSerializer.ToNode(replay.Header, versioned: true), golden, "header.json.gz");
        Compare(GoldenSerializer.ToNode(replay.ReadDetails(), versioned: true), golden, "details.json.gz");
        Compare(GoldenSerializer.ToNode(replay.ReadInitData(), versioned: false), golden, "initdata.json.gz");
        Compare(GoldenSerializer.Attributes(replay.ReadAttributes()), golden, "attributes.json.gz");
        CompareLines(replay.ReadGameEvents().Select(e => GoldenSerializer.Event(e, versioned: false)), golden, "game.ndjson.gz");
        CompareLines(replay.ReadMessageEvents().Select(e => GoldenSerializer.Event(e, versioned: false)), golden, "message.ndjson.gz");
        CompareLines(replay.ReadTrackerEvents().Select(e => GoldenSerializer.Event(e, versioned: true)), golden, "tracker.ndjson.gz");
    }

    // 설계 D10: 샘플 헤더의 SMD5에는 m_dataDeprecated(태그 0)가 없다(make_golden.py stats). 존재 기록이 이를 반영하고
    // 골든 JSON에도 그 키가 없어야 한다. 헤더는 최신 프로토콜(98310)로 디코딩된다.
    [TestMethod]
    public void VersionedMissingField_IsNotRecordedAndNotSerialized()
    {
        using var replay = S2Replay.Open(Path.Combine(Root, "Replays", "20260818_174245.SC2Replay"));
        var header = (Versions.Protocol98310.Replay.SHeader)replay.Header;

        Assert.IsNotNull(header.NgdpRootKey.PresentTags);
        Assert.DoesNotContain(0L, header.NgdpRootKey.PresentTags);   // m_dataDeprecated
        Assert.Contains(1L, header.NgdpRootKey.PresentTags);         // m_data
        var node = (JsonObject)GoldenSerializer.ToNode(header, versioned: true)!;
        Assert.IsFalse(((JsonObject)node["m_ngdpRootKey"]!).ContainsKey("m_dataDeprecated"));
    }

    private static void Compare(JsonNode? actual, string directory, string file)
    {
        using var reader = OpenText(directory, file);
        using var expected = JsonDocument.Parse(reader.ReadToEnd());
        if (GoldenComparer.FirstDifference(actual, expected.RootElement) is { } diff)
            Assert.Fail($"{Path.GetFileName(directory)}/{file}: {diff}");
    }

    private static void CompareLines(IEnumerable<JsonNode> actual, string directory, string file)
    {
        using var reader = OpenText(directory, file);
        int index = 0;
        foreach (var node in actual)
        {
            string? line = reader.ReadLine();
            if (line is null)
                Assert.Fail($"{Path.GetFileName(directory)}/{file}: .NET has more than {index} events");
            using var expected = JsonDocument.Parse(line);
            if (GoldenComparer.FirstDifference(node, expected.RootElement) is { } diff)
                Assert.Fail($"{Path.GetFileName(directory)}/{file} #{index}: {diff}");
            index++;
        }
        Assert.IsNull(reader.ReadLine(), $"{Path.GetFileName(directory)}/{file}: Python has more than {index} events");
    }

    private static StreamReader OpenText(string directory, string file) =>
        new(new GZipStream(File.OpenRead(Path.Combine(directory, file)), CompressionMode.Decompress));
}
