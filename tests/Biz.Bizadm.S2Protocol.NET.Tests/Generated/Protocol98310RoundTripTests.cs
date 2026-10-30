using System.Reflection;
using Biz.Bizadm.S2Protocol.NET.Decoding;
using Biz.Bizadm.S2Protocol.NET.Encoding;
using Biz.Bizadm.S2Protocol.NET.Versions.Protocol98310;
using Biz.Bizadm.S2Protocol.NET.Versions.Protocol98310.Replay;

namespace Biz.Bizadm.S2Protocol.NET.Tests.Generated;

// 생성기 단위 10 출력(protocol98310)의 Read/Write 왕복.
[TestClass]
public sealed class Protocol98310RoundTripTests
{
    [TestMethod]
    public void SVarUint32_DecodesHandEncodedBytes()
    {
        // 태그 0(2비트) 뒤 값 5(6비트): 바이트 안에서 LSB부터 → 0b0001_0100
        var value = SVarUint32.ReadBitPacked(new BitPackedDecoder(new byte[] { 0x14 }));

        Assert.AreEqual(5, ((SVarUint32.Uint6)value).Value);
    }

    [TestMethod]
    [DataRow(0, 63L)]
    [DataRow(1, 16383L)]
    [DataRow(2, 4194303L)]
    [DataRow(3, 4294967295L)]
    public void SVarUint32_EachCase_RoundTrips(int tag, long max)
    {
        SVarUint32 original = tag switch
        {
            0 => new SVarUint32.Uint6 { Value = (int)max },
            1 => new SVarUint32.Uint14 { Value = (int)max },
            2 => new SVarUint32.Uint22 { Value = (int)max },
            _ => new SVarUint32.Uint32 { Value = max },
        };
        var encoder = new BitPackedEncoder();
        SVarUint32.WriteBitPacked(encoder, original);

        var decoded = SVarUint32.ReadBitPacked(new BitPackedDecoder(encoder.ToArray()));

        Assert.AreEqual(original.GetType(), decoded.GetType());
        var reencoder = new BitPackedEncoder();
        SVarUint32.WriteBitPacked(reencoder, decoded);
        CollectionAssert.AreEqual(encoder.ToArray(), reencoder.ToArray());
    }

    [TestMethod]
    public void SHeader_Versioned_RoundTrips()
    {
        var header = new SHeader
        {
            Signature = "StarCraft II replay\u001b11"u8.ToArray(),
            Version = new SVersion { Flags = 1, Major = 5, Minor = 0, Revision = 17, Build = 98310, BaseBuild = 98310 },
            Type = EReplayType.Multiplayer,
            ElapsedGameLoops = 12345,
            UseScaledTime = true,
            NgdpRootKey = new SMD5 { Data = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray() },
            DataBuildNum = 98310,
            ReplayCompatibilityHash = new SMD5 { Data = new byte[16] },
            NgdpRootKeyIsDevData = false,
        };
        var encoder = new VersionedEncoder();
        SHeader.WriteVersioned(encoder, header);
        byte[] bytes = encoder.ToArray();

        var decoder = new VersionedDecoder(bytes);
        var decoded = SHeader.ReadVersioned(decoder);

        Assert.IsTrue(decoder.Done);
        Assert.AreEqual(0x05, bytes[0]);   // struct skip 코드
        CollectionAssert.AreEqual(header.Signature, decoded.Signature);
        Assert.AreEqual(98310L, decoded.Version.BaseBuild);
        Assert.AreEqual(17, decoded.Version.Revision);
        Assert.AreEqual(EReplayType.Multiplayer, decoded.Type);
        Assert.AreEqual(12345L, decoded.ElapsedGameLoops);
        Assert.IsTrue(decoded.UseScaledTime);
        CollectionAssert.AreEqual(header.NgdpRootKey.Data, decoded.NgdpRootKey.Data);
        Assert.AreEqual(98310L, decoded.DataBuildNum);

        var reencoder = new VersionedEncoder();
        SHeader.WriteVersioned(reencoder, decoded);
        CollectionAssert.AreEqual(bytes, reencoder.ToArray());
    }

    // 모든 bitpacked 생성 타입: 무작위 바이트를 디코딩해 성공하면 e1 = encode(decode(random))을 만든다.
    // 정렬 읽기가 버리는 패딩 비트는 원본에선 무작위, 인코딩에선 0이므로 원본과 직접 비교하지 않고
    // decode(e1)이 같은 비트 수를 소비하고 다시 e1로 인코딩되는지 본다.
    [TestMethod]
    public void AllBitPackedTypes_DecodeThenEncode_IsStable()
    {
        var types = typeof(SHeader).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Biz.Bizadm.S2Protocol.NET.Versions.Protocol98310") == true)
            .Select(t => (Type: t,
                          Read: t.GetMethod("ReadBitPacked", BindingFlags.NonPublic | BindingFlags.Static, [typeof(BitPackedDecoder)]),
                          Write: t.GetMethod("WriteBitPacked", BindingFlags.NonPublic | BindingFlags.Static)))
            .Where(x => x.Read is not null && x.Write is not null)
            .ToList();
        Assert.IsGreaterThan(100, types.Count, "bitpacked types found");

        var random = new Random(98310);
        int decodedCount = 0;
        foreach (var (type, read, write) in types)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var data = new byte[256];
                random.NextBytes(data);
                if (attempt % 2 == 0)   // 0이 많은 입력은 짧은 배열·없는 optional을 만들어 성공률을 높인다
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
                    continue;   // 잘린 입력 또는 정의되지 않은 choice 태그
                }

                var first = new BitPackedEncoder();
                write!.Invoke(null, [first, value]);
                Assert.AreEqual(decoder.UsedBits, first.UsedBits, $"{type.FullName}: encoded bit count");
                byte[] e1 = first.ToArray();

                var redecoder = new BitPackedDecoder(e1);
                object again = read.Invoke(null, [redecoder])!;
                Assert.AreEqual(decoder.UsedBits, redecoder.UsedBits, $"{type.FullName}: re-decoded bit count");
                var second = new BitPackedEncoder();
                write.Invoke(null, [second, again]);
                CollectionAssert.AreEqual(e1, second.ToArray(), $"{type.FullName}: re-encoded bytes");
                decodedCount++;
            }
        }
        Assert.IsGreaterThan(types.Count, decodedCount, "successful decodes");
    }
}
