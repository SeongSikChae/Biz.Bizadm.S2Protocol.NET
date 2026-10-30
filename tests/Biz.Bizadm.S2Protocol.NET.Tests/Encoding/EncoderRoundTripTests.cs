using Biz.Bizadm.S2Protocol.NET.Decoding;
using Biz.Bizadm.S2Protocol.NET.Encoding;

namespace Biz.Bizadm.S2Protocol.NET.Tests.Encoding;

// 단위 3~5 디코더 테스트의 바이트열을 인코더로 다시 만들고, 무작위 값으로 왕복한다.
[TestClass]
public sealed class EncoderRoundTripTests
{
    [TestMethod]
    public void BufferWriter_ReproducesDecoderFixtures()
    {
        var a = new BitPackedBufferWriter();
        a.WriteBits(4, 3);
        a.WriteBits(21, 5);
        CollectionAssert.AreEqual(new byte[] { 0xAC }, a.ToArray());

        var b = new BitPackedBufferWriter();
        b.WriteBits(5485, 13);
        b.WriteBits(6, 3);
        CollectionAssert.AreEqual(new byte[] { 0xAB, 0xCD }, b.ToArray());

        var c = new BitPackedBufferWriter();
        c.WriteBits(3, 3);
        c.WriteBits(5581, 13);
        CollectionAssert.AreEqual(new byte[] { 0xAB, 0xCD }, c.ToArray());

        var d = new BitPackedBufferWriter();
        d.WriteBitsFromBytes(new byte[] { 0x20, 0, 0, 0, 0, 0, 0, 0, 0x01 }, 70);
        d.WriteBits(0x02, 2);   // 마지막 바이트의 남은 상위 2비트
        CollectionAssert.AreEqual(new byte[] { 0x80, 0, 0, 0, 0, 0, 0, 0, 0x81 }, d.ToArray());

        var e = new BitPackedBufferWriter(bigEndian: false);
        e.WriteBits(0x12345678, 32);
        CollectionAssert.AreEqual(new byte[] { 0x78, 0x56, 0x34, 0x12 }, e.ToArray());
    }

    [TestMethod]
    public void BufferWriter_ToArray_IncludesPartialByteWithoutChangingState()
    {
        var writer = new BitPackedBufferWriter();
        writer.WriteBits(1, 1);

        CollectionAssert.AreEqual(new byte[] { 0x01 }, writer.ToArray());
        writer.WriteBits(0x7F, 7);
        CollectionAssert.AreEqual(new byte[] { 0xFF }, writer.ToArray());
        Assert.AreEqual(8L, writer.UsedBits);
    }

    [TestMethod]
    public void BitPackedEncoder_ReproducesDecoderFixtures()
    {
        var blob = new BitPackedEncoder();
        blob.WriteBool(true);
        blob.WriteBlob([0xAA, 0xBB], 0, 8);
        CollectionAssert.AreEqual(new byte[] { 0x03, 0x00, 0xAA, 0xBB }, blob.ToArray());

        var bits = new BitPackedEncoder();
        bits.WriteBitArray(new S2BitArray(3, [0x07]), 0, 8);
        CollectionAssert.AreEqual(new byte[] { 0x03, 0x07 }, bits.ToArray());

        var ints = new BitPackedEncoder();
        ints.WriteInt(-123, -128, 8);
        ints.WriteInt(long.MaxValue, long.MinValue, 64);
        ints.WriteUInt64(ulong.MaxValue, 64);
        CollectionAssert.AreEqual(
            new byte[] { 0x05 }.Concat(Enumerable.Repeat((byte)0xFF, 16)).ToArray(),
            ints.ToArray());
    }

    [TestMethod]
    public void BitPackedEncoder_UnalignedFourCC_MatchesDecoder()
    {
        byte[] data = [0xAB, 0xCD, 0xEF, 0x12, 0x34];
        var decoder = new BitPackedDecoder(data);
        bool flag = decoder.ReadBool();
        FourCC fourCC = decoder.ReadFourCC();

        var encoder = new BitPackedEncoder();
        encoder.WriteBool(flag);
        encoder.WriteFourCC(fourCC);

        // 33비트만 썼으므로 마지막 바이트는 0x34의 최하위 비트(0)만 남는다.
        CollectionAssert.AreEqual(new byte[] { 0xAB, 0xCD, 0xEF, 0x12, 0x00 }, encoder.ToArray());
    }

    [TestMethod]
    public void BitPackedEncoder_RejectsOutOfRange()
    {
        var encoder = new BitPackedEncoder();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => encoder.WriteInt(256, 0, 8));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => encoder.WriteInt(-1, 0, 8));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => encoder.WriteUInt64(16, 4));
    }

    [TestMethod]
    [DataRow(new byte[] { 0x00 }, 0L)]
    [DataRow(new byte[] { 0x02 }, 1L)]
    [DataRow(new byte[] { 0x03 }, -1L)]
    [DataRow(new byte[] { 0x7E }, 63L)]
    [DataRow(new byte[] { 0x80, 0x01 }, 64L)]
    [DataRow(new byte[] { 0x81, 0x01 }, -64L)]
    [DataRow(new byte[] { 0xD8, 0x04 }, 300L)]
    [DataRow(new byte[] { 0x81, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x02 }, long.MinValue)]
    public void VersionedEncoder_VInt_ReproducesDecoderFixtures(byte[] expected, long value)
    {
        var encoder = new VersionedEncoder();
        encoder.WriteVInt(value);

        CollectionAssert.AreEqual(expected, encoder.ToArray());
    }

    [TestMethod]
    public void VersionedEncoder_ReproducesScalarsFixture()
    {
        var encoder = new VersionedEncoder();
        encoder.WriteBool(true);
        encoder.WriteOptionalExists(false);
        encoder.WriteOptionalExists(true);
        encoder.WriteBlob([0xAA, 0xBB]);
        encoder.WriteBitArray(new S2BitArray(9, [0x01, 0xFF]));
        encoder.WriteFourCC(new FourCC(0x7332_6D61));
        encoder.WriteReal32(1.5f);
        encoder.WriteReal64(1.5d);
        encoder.WriteArrayLength(2);
        encoder.WriteChoiceTag(5);
        encoder.WriteStructFieldCount(3);

        CollectionAssert.AreEqual(new byte[]
        {
            0x06, 0x01,
            0x04, 0x00,
            0x04, 0x01,
            0x02, 0x04, 0xAA, 0xBB,
            0x01, 0x12, 0x01, 0xFF,
            0x07, 0x73, 0x32, 0x6D, 0x61,
            0x07, 0x3F, 0xC0, 0x00, 0x00,
            0x08, 0x3F, 0xF8, 0, 0, 0, 0, 0, 0,
            0x00, 0x04,
            0x03, 0x0A,
            0x05, 0x06,
        }, encoder.ToArray());
    }

    [TestMethod]
    public void VersionedEncoder_UInt64Max_ReproducesFixture()
    {
        var encoder = new VersionedEncoder();
        encoder.WriteUInt64(ulong.MaxValue);

        CollectionAssert.AreEqual(
            new byte[] { 0x09, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x03 },
            encoder.ToArray());
    }

    [TestMethod]
    public void BufferWriter_RandomRoundTrip()
    {
        var random = new Random(20261030);
        for (int round = 0; round < 200; round++)
        {
            var writer = new BitPackedBufferWriter();
            var expected = new List<(int Bits, ulong Value, byte[]? Bytes)>();
            for (int i = 0; i < 20; i++)
            {
                if (random.Next(4) == 0)
                {
                    int bits = random.Next(0, 150);
                    byte[] bytes = RandomBigEndianInteger(random, bits);
                    writer.WriteBitsFromBytes(bytes, bits);
                    expected.Add((bits, 0, bytes));
                }
                else
                {
                    int bits = random.Next(0, 65);
                    ulong value = bits == 0 ? 0 : (ulong)random.NextInt64() << 1 ^ (ulong)random.Next(2);
                    if (bits < 64) value &= (1UL << bits) - 1;
                    writer.WriteBits(value, bits);
                    expected.Add((bits, value, null));
                }
            }

            var reader = new BitPackedBuffer(writer.ToArray());
            foreach (var (bits, value, bytes) in expected)
            {
                if (bytes is null)
                    Assert.AreEqual(value, reader.ReadBits(bits), $"round {round}");
                else
                    CollectionAssert.AreEqual(bytes, reader.ReadBitsToBytes(bits), $"round {round}");
            }
            Assert.AreEqual(writer.UsedBits, reader.UsedBits);
        }
    }

    [TestMethod]
    public void BitPackedEncoder_RandomRoundTrip()
    {
        var random = new Random(1030);
        for (int round = 0; round < 200; round++)
        {
            var encoder = new BitPackedEncoder();
            var checks = new List<Action<BitPackedDecoder>>();
            for (int i = 0; i < 15; i++)
            {
                switch (random.Next(6))
                {
                    case 0:
                    {
                        int bits = random.Next(1, 33);
                        long offset = random.Next(-1000, 1000);
                        long value = offset + random.NextInt64(1L << bits);
                        encoder.WriteInt(value, offset, bits);
                        checks.Add(d => Assert.AreEqual(value, d.ReadInt(offset, bits)));
                        break;
                    }
                    case 1:
                    {
                        bool value = random.Next(2) == 1;
                        encoder.WriteBool(value);
                        checks.Add(d => Assert.AreEqual(value, d.ReadBool()));
                        break;
                    }
                    case 2:
                    {
                        var value = new byte[random.Next(0, 20)];
                        random.NextBytes(value);
                        encoder.WriteBlob(value, 0, 8);
                        checks.Add(d => CollectionAssert.AreEqual(value, d.ReadBlob(0, 8)));
                        break;
                    }
                    case 3:
                    {
                        int length = random.Next(0, 100);
                        var value = new S2BitArray(length, RandomBigEndianInteger(random, length));
                        encoder.WriteBitArray(value, 0, 8);
                        checks.Add(d =>
                        {
                            var actual = d.ReadBitArray(0, 8);
                            Assert.AreEqual(value.Length, actual.Length);
                            CollectionAssert.AreEqual(value.Data, actual.Data);
                        });
                        break;
                    }
                    case 4:
                    {
                        var value = new FourCC((uint)random.NextInt64(1L << 32));
                        encoder.WriteFourCC(value);
                        checks.Add(d => Assert.AreEqual(value, d.ReadFourCC()));
                        break;
                    }
                    default:
                    {
                        float f = (float)random.NextDouble();
                        double g = random.NextDouble();
                        encoder.WriteReal32(f);
                        encoder.WriteReal64(g);
                        checks.Add(d =>
                        {
                            Assert.AreEqual(f, d.ReadReal32());
                            Assert.AreEqual(g, d.ReadReal64());
                        });
                        break;
                    }
                }
            }

            var decoder = new BitPackedDecoder(encoder.ToArray());
            foreach (var check in checks)
                check(decoder);
            Assert.AreEqual(encoder.UsedBits, decoder.UsedBits, $"round {round}");
        }
    }

    [TestMethod]
    public void VersionedEncoder_RandomVIntRoundTrip()
    {
        var random = new Random(42);
        long[] edges = [0, 1, -1, 63, 64, -64, long.MaxValue, long.MinValue, long.MinValue + 1];
        var values = edges.Concat(Enumerable.Range(0, 500).Select(_ => random.NextInt64(long.MinValue, long.MaxValue))).ToArray();

        var encoder = new VersionedEncoder();
        foreach (long value in values)
            encoder.WriteInt(value);
        encoder.WriteUInt64(ulong.MaxValue);

        var decoder = new VersionedDecoder(encoder.ToArray());
        foreach (long value in values)
            Assert.AreEqual(value, decoder.ReadInt());
        Assert.AreEqual(ulong.MaxValue, decoder.ReadUInt64());
        Assert.IsTrue(decoder.Done);
    }

    // bits 비트 정수의 빅엔디언 표현. 최상위 바이트의 남는 비트는 0.
    private static byte[] RandomBigEndianInteger(Random random, int bits)
    {
        var bytes = new byte[(bits + 7) / 8];
        random.NextBytes(bytes);
        if (bits % 8 != 0)
            bytes[0] &= (byte)((1 << (bits % 8)) - 1);
        return bytes;
    }
}
