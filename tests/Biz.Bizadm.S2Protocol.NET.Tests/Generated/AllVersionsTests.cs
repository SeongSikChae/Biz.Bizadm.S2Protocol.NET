using System.Reflection;
using Biz.Bizadm.S2Protocol.NET.Decoding;
using Biz.Bizadm.S2Protocol.NET.Encoding;
using Biz.Bizadm.S2Protocol.NET.Protocols;

namespace Biz.Bizadm.S2Protocol.NET.Tests.Generated;

// 단위 13: 93개 빌드 전체 출력 후의 등록·기본 동작.
[TestClass]
public sealed class AllVersionsTests
{
    [TestMethod]
    public void Registry_HasAll93Builds()
    {
        Assert.HasCount(93, S2ProtocolRegistry.Builds);
        Assert.AreEqual(15405, S2ProtocolRegistry.Builds[0]);
        Assert.AreEqual(98310, S2ProtocolRegistry.Builds[^1]);
        foreach (int build in S2ProtocolRegistry.Builds)
            Assert.AreEqual(build, S2ProtocolRegistry.Get(build).BaseBuild);
    }

    // 빌드마다 그 빌드의 SHeader를 만들어 IS2Protocol로 인코딩·디코딩하고 공통 인터페이스로 base build를 읽는다.
    [TestMethod]
    public void EveryBuild_HeaderRoundTripsThroughCommonInterface()
    {
        foreach (int build in S2ProtocolRegistry.Builds)
        {
            var protocol = S2ProtocolRegistry.Get(build);
            var assembly = protocol.GetType().Assembly;
            string ns = $"Biz.Bizadm.S2Protocol.NET.Versions.Protocol{build:D5}";
            var header = Activator.CreateInstance(assembly.GetType($"{ns}.Replay.SHeader", throwOnError: true)!)!;
            var version = header.GetType().GetProperty("Version")!.GetValue(header)!;
            var baseBuild = version.GetType().GetProperty("BaseBuild")!;
            baseBuild.SetValue(version, Convert.ChangeType(build, baseBuild.PropertyType));

            var decoded = protocol.DecodeHeader(protocol.EncodeHeader((Common.Replay.IHeader)header));

            Assert.AreEqual(build, decoded.Version.BaseBuild, $"build {build}");
        }
    }

    [TestMethod]
    public void OldBuilds_StreamsPythonCannotDecode_AreNotSupported()
    {
        var oldest = S2ProtocolRegistry.Get(15405);   // replay_userid_typeid = None, tracker 없음
        Assert.ThrowsExactly<NotSupportedException>(() => oldest.DecodeGameEvents(new byte[1]));
        Assert.ThrowsExactly<NotSupportedException>(() => oldest.DecodeMessageEvents(new byte[1]));
        Assert.ThrowsExactly<NotSupportedException>(() => oldest.DecodeTrackerEvents(new byte[1]));

        var noTracker = S2ProtocolRegistry.Get(24944);   // game/message 있음, tracker 없음
        Assert.IsEmpty(noTracker.DecodeGameEvents(ReadOnlyMemory<byte>.Empty).ToList());
        Assert.ThrowsExactly<NotSupportedException>(() => noTracker.DecodeTrackerEvents(new byte[1]));

        Assert.IsEmpty(S2ProtocolRegistry.Get(26490).DecodeTrackerEvents(ReadOnlyMemory<byte>.Empty).ToList());
    }

    // 단위 10의 안정성 검사를 전 빌드의 bitpacked 타입으로 넓힌다: e1 = encode(decode(random)), decode(e1) → e1.
    [TestMethod]
    public void EveryBuild_BitPackedTypes_DecodeThenEncode_IsStable()
    {
        var random = new Random(93);
        var all = typeof(S2ProtocolRegistry).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Biz.Bizadm.S2Protocol.NET.Versions.Protocol") == true)
            .Select(t => (Type: t,
                          Read: t.GetMethod("ReadBitPacked", BindingFlags.NonPublic | BindingFlags.Static, [typeof(BitPackedDecoder)]),
                          Write: t.GetMethod("WriteBitPacked", BindingFlags.NonPublic | BindingFlags.Static)))
            .Where(x => x.Read is not null && x.Write is not null)
            .ToList();
        Assert.IsGreaterThan(93 * 50, all.Count, "bitpacked types across builds");

        int decoded = 0;
        foreach (var (type, read, write) in all)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                var data = new byte[256];
                random.NextBytes(data);
                if (attempt % 2 == 0)
                    for (int i = 0; i < data.Length; i++)
                        data[i] &= (byte)random.Next(256);

                var decoder = new BitPackedDecoder(data);
                object value;
                try
                {
                    value = read!.Invoke(null, [decoder])!;
                }
                catch (TargetInvocationException e) when (e.InnerException is CorruptedReplayException)
                {
                    continue;
                }

                var first = new BitPackedEncoder();
                write!.Invoke(null, [first, value]);
                Assert.AreEqual(decoder.UsedBits, first.UsedBits, $"{type.FullName}: encoded bit count");
                byte[] e1 = first.ToArray();

                var redecoder = new BitPackedDecoder(e1);
                object again = read.Invoke(null, [redecoder])!;
                var second = new BitPackedEncoder();
                write.Invoke(null, [second, again]);
                CollectionAssert.AreEqual(e1, second.ToArray(), $"{type.FullName}: re-encoded bytes");
                decoded++;
            }
        }
        Assert.IsGreaterThan(all.Count, decoded, "successful decodes");
    }
}
