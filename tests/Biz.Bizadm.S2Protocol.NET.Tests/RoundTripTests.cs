using Biz.Bizadm.MPQ.NET;
using Biz.Bizadm.S2Protocol.NET.Attributes;
using Biz.Bizadm.S2Protocol.NET.Protocols;

namespace Biz.Bizadm.S2Protocol.NET.Tests;

// 단위 17: 실제 샘플의 각 파트를 디코드 → 인코드해 원본 바이트와 같은지 본다(정확 일치 프로토콜).
[TestClass]
public sealed class RoundTripTests
{
    private static string Replays => Path.Combine(AppContext.BaseDirectory, "TestData", "Replays");

    public static IEnumerable<object[]> Samples =>
        Directory.Exists(Replays)
            ? Directory.GetFiles(Replays, "*.SC2Replay").Select(Path.GetFileName).Order().Select(n => new object[] { n! })
            : [];

    [TestMethod]
    [DynamicData(nameof(Samples))]
    public void Sample_EveryPart_ReEncodesToOriginalBytes(string file)
    {
        using var archive = MpqArchive.Open(Path.Combine(Replays, file));
        byte[] header = archive.UserData!.Content.ToArray();
        var latest = S2ProtocolRegistry.Latest;
        var decodedHeader = latest.DecodeHeader(header);
        var protocol = S2ProtocolRegistry.Get(checked((int)decodedHeader.Version.BaseBuild));

        var failures = new List<string>();
        void Check(string part, byte[] original, Func<byte[]> reencode)
        {
            byte[] actual = reencode();
            if (!original.AsSpan().SequenceEqual(actual))
                failures.Add($"{part}: {Describe(original, actual)}");
        }

        Check("header", header, () => latest.EncodeHeader(decodedHeader));
        byte[] details = Read(archive, "replay.details");
        Check("details", details, () => protocol.EncodeDetails(protocol.DecodeDetails(details)));
        byte[] initData = Read(archive, "replay.initData");
        Check("initData", initData, () => protocol.EncodeInitData(protocol.DecodeInitData(initData)));
        byte[] game = Read(archive, "replay.game.events");
        Check("game", game, () => protocol.EncodeGameEvents(protocol.DecodeGameEvents(game).ToList()));
        byte[] message = Read(archive, "replay.message.events");
        Check("message", message, () => protocol.EncodeMessageEvents(protocol.DecodeMessageEvents(message).ToList()));
        byte[] tracker = Read(archive, "replay.tracker.events");
        Check("tracker", tracker, () => protocol.EncodeTrackerEvents(protocol.DecodeTrackerEvents(tracker).ToList()));
        byte[] attributes = Read(archive, "replay.attributes.events");
        Check("attributes", attributes, () => ReplayAttributes.Decode(attributes).Encode());

        if (failures.Count > 0)
            Assert.Fail($"{file}:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    private static byte[] Read(MpqArchive archive, string name)
    {
        using var stream = archive.OpenFile(name);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string Describe(byte[] original, byte[] actual)
    {
        int i = 0;
        while (i < original.Length && i < actual.Length && original[i] == actual[i])
            i++;
        string around(byte[] b) => Convert.ToHexString(b, Math.Max(0, i - 4), Math.Min(12, b.Length - Math.Max(0, i - 4)));
        return $"length {original.Length} vs {actual.Length}, first difference at byte {i} (original {around(original)} / encoded {around(actual)})";
    }
}
