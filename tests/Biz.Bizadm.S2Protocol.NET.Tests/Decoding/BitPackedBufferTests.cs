using Biz.Bizadm.S2Protocol.NET.Decoding;

namespace Biz.Bizadm.S2Protocol.NET.Tests.Decoding;

// 기대값은 s2protocol decoders.BitPackedBuffer.read_bits의 청크 배치를 손으로 따라 계산했다.
[TestClass]
public sealed class BitPackedBufferTests
{
    [TestMethod]
    public void ReadBits_ConsumesLowBitsOfByteFirst()
    {
        var buffer = new BitPackedBuffer(new byte[] { 0xAC });   // 1010_1100

        Assert.AreEqual(4UL, buffer.ReadBits(3));    // 100
        Assert.AreEqual(21UL, buffer.ReadBits(5));   // 10101
        Assert.IsTrue(buffer.Done);
        Assert.AreEqual(8L, buffer.UsedBits);
    }

    [TestMethod]
    public void ReadBits_AcrossBytes_FirstChunkIsMostSignificant()
    {
        var buffer = new BitPackedBuffer(new byte[] { 0xAB, 0xCD });

        Assert.AreEqual(5485UL, buffer.ReadBits(13));   // (0xAB << 5) | (0xCD & 0x1F)
        Assert.AreEqual(13L, buffer.UsedBits);
        Assert.AreEqual(6UL, buffer.ReadBits(3));       // 0xCD >> 5
        Assert.IsTrue(buffer.Done);
    }

    [TestMethod]
    public void ReadBits_FromUnalignedPosition()
    {
        var buffer = new BitPackedBuffer(new byte[] { 0xAB, 0xCD, 0xEF });

        Assert.AreEqual(3UL, buffer.ReadBits(3));
        Assert.AreEqual(5581UL, buffer.ReadBits(13));   // ((0xAB >> 3) << 8) | 0xCD
    }

    [TestMethod]
    public void ReadBitsToBytes_MatchesReadBitsAsBigEndianBytes()
    {
        var buffer = new BitPackedBuffer(new byte[] { 0xAB, 0xCD });

        CollectionAssert.AreEqual(new byte[] { 0x15, 0x6D }, buffer.ReadBitsToBytes(13));   // 5485
    }

    [TestMethod]
    public void ReadBitsToBytes_FromUnalignedPosition()
    {
        var buffer = new BitPackedBuffer(new byte[] { 0xAB, 0xCD, 0xEF });

        buffer.ReadBits(3);
        CollectionAssert.AreEqual(new byte[] { 0x15, 0xCD }, buffer.ReadBitsToBytes(13));   // 5581
    }

    [TestMethod]
    public void ReadBitsToBytes_AgreesWithReadBits_ForRandomInputs()
    {
        var random = new Random(20261030);
        for (int i = 0; i < 500; i++)
        {
            var data = new byte[12];
            random.NextBytes(data);
            int skip = random.Next(0, 8);
            int bits = random.Next(1, 65);

            var expectedBuffer = new BitPackedBuffer(data);
            expectedBuffer.ReadBits(skip);
            ulong value = expectedBuffer.ReadBits(bits);

            var actualBuffer = new BitPackedBuffer(data);
            actualBuffer.ReadBits(skip);
            byte[] actual = actualBuffer.ReadBitsToBytes(bits);

            var expected = new byte[(bits + 7) / 8];
            for (int b = 0; b < expected.Length; b++)
                expected[expected.Length - 1 - b] = (byte)(value >> (8 * b));
            CollectionAssert.AreEqual(expected, actual, $"skip={skip} bits={bits}");
            Assert.AreEqual(expectedBuffer.UsedBits, actualBuffer.UsedBits);
        }
    }

    [TestMethod]
    public void ReadBitsToBytes_MoreThan64Bits()
    {
        var buffer = new BitPackedBuffer(new byte[] { 0x80, 0, 0, 0, 0, 0, 0, 0, 0x01 });

        // 70비트: 8바이트가 시프트 62..6에, 마지막 바이트의 하위 6비트가 시프트 0에 놓인다 → 2^69 + 1
        CollectionAssert.AreEqual(new byte[] { 0x20, 0, 0, 0, 0, 0, 0, 0, 0x01 }, buffer.ReadBitsToBytes(70));
    }

    [TestMethod]
    public void ReadUnalignedBytes_ReadsEightBitsPerByte()
    {
        var buffer = new BitPackedBuffer(new byte[] { 0xAB, 0xCD });

        buffer.ReadBits(4);
        CollectionAssert.AreEqual(new byte[] { 0xAD }, buffer.ReadUnalignedBytes(1));   // (0xA << 4) | 0xD
    }

    [TestMethod]
    public void ReadAlignedBytes_DiscardsPartialByte()
    {
        var buffer = new BitPackedBuffer(new byte[] { 0xFF, 0x01, 0x02 });

        buffer.ReadBits(3);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x02 }, buffer.ReadAlignedBytes(2));
        Assert.IsTrue(buffer.Done);
    }

    [TestMethod]
    public void ByteAlign_SkipsRestOfCurrentByte()
    {
        var buffer = new BitPackedBuffer(new byte[] { 0xFF, 0x05 });

        buffer.ReadBits(1);
        buffer.ByteAlign();
        Assert.AreEqual(8L, buffer.UsedBits);
        Assert.AreEqual(5UL, buffer.ReadBits(8));
    }

    [TestMethod]
    public void ReadBits_PastEnd_Throws()
    {
        var buffer = new BitPackedBuffer(new byte[] { 0x01 });

        Assert.ThrowsExactly<CorruptedReplayException>(() => buffer.ReadBits(9));
    }

    [TestMethod]
    public void ReadAlignedBytes_PastEnd_Throws()
    {
        var buffer = new BitPackedBuffer(new byte[] { 0x01 });

        Assert.ThrowsExactly<CorruptedReplayException>(() => buffer.ReadAlignedBytes(2));
    }

    [TestMethod]
    public void LittleEndian_FirstChunkIsLeastSignificant()
    {
        var buffer16 = new BitPackedBuffer(new byte[] { 0x34, 0x12 }, bigEndian: false);
        var buffer32 = new BitPackedBuffer(new byte[] { 0x78, 0x56, 0x34, 0x12 }, bigEndian: false);

        Assert.AreEqual(0x1234UL, buffer16.ReadBits(16));
        Assert.AreEqual(0x12345678UL, buffer32.ReadBits(32));
    }

    [TestMethod]
    public void Empty_IsDone()
    {
        Assert.IsTrue(new BitPackedBuffer(ReadOnlyMemory<byte>.Empty).Done);
    }
}
