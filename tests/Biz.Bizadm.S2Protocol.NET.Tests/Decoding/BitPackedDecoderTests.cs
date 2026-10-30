using Biz.Bizadm.S2Protocol.NET.Decoding;

namespace Biz.Bizadm.S2Protocol.NET.Tests.Decoding;

[TestClass]
public sealed class BitPackedDecoderTests
{
    [TestMethod]
    public void ReadInt_AddsOffset()
    {
        var decoder = new BitPackedDecoder(new byte[] { 0x05 });

        Assert.AreEqual(-123L, decoder.ReadInt(-128, 8));
    }

    [TestMethod]
    public void ReadInt_SignedInt64Bounds()
    {
        var max = new BitPackedDecoder(Enumerable.Repeat((byte)0xFF, 8).ToArray());
        var min = new BitPackedDecoder(new byte[8]);

        Assert.AreEqual(long.MaxValue, max.ReadInt(long.MinValue, 64));   // -2^63 + (2^64 - 1)
        Assert.AreEqual(long.MinValue, min.ReadInt(long.MinValue, 64));
    }

    [TestMethod]
    public void ReadUInt64_FullRange()
    {
        var decoder = new BitPackedDecoder(Enumerable.Repeat((byte)0xFF, 8).ToArray());

        Assert.AreEqual(ulong.MaxValue, decoder.ReadUInt64(64));
    }

    [TestMethod]
    public void ReadBool_ReadsOneBit()
    {
        var decoder = new BitPackedDecoder(new byte[] { 0x01 });

        Assert.IsTrue(decoder.ReadBool());
        Assert.IsFalse(decoder.ReadBool());
        Assert.AreEqual(2L, decoder.UsedBits);
    }

    [TestMethod]
    public void ReadOptionalExists_AbsentAndPresent()
    {
        var decoder = new BitPackedDecoder(new byte[] { 0x02 });

        Assert.IsFalse(decoder.ReadOptionalExists());
        Assert.IsTrue(decoder.ReadOptionalExists());
    }

    [TestMethod]
    public void ReadBlob_ReadsLengthThenAlignedBytes()
    {
        var decoder = new BitPackedDecoder(new byte[] { 0x02, 0xAA, 0xBB });

        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB }, decoder.ReadBlob(0, 8));
        Assert.IsTrue(decoder.Done);
    }

    [TestMethod]
    public void ReadBlob_AfterUnalignedLength_SkipsToNextByte()
    {
        // bool(1) 다음 8비트 길이: 0x03 >> 1 = 1이 상위 7비트, 0x00의 최하위 비트 0이 하위 1비트 → 길이 2
        var decoder = new BitPackedDecoder(new byte[] { 0x03, 0x00, 0xAA, 0xBB });

        Assert.IsTrue(decoder.ReadBool());
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB }, decoder.ReadBlob(0, 8));
        Assert.IsTrue(decoder.Done);
    }

    [TestMethod]
    public void ReadBitArray_ReadsLengthThenBits()
    {
        var decoder = new BitPackedDecoder(new byte[] { 0x03, 0xFF });

        var bits = decoder.ReadBitArray(0, 8);

        Assert.AreEqual(3, bits.Length);
        CollectionAssert.AreEqual(new byte[] { 0x07 }, bits.Data);
    }

    [TestMethod]
    public void ReadFourCC_Aligned()
    {
        var decoder = new BitPackedDecoder("s2ma"u8.ToArray());

        Assert.AreEqual("s2ma", decoder.ReadFourCC().ToString());
    }

    [TestMethod]
    public void ReadFourCC_Unaligned_ReadsFourEightBitChunks()
    {
        byte[] data = [0xAB, 0xCD, 0xEF, 0x12, 0x34];
        var reference = new BitPackedBuffer(data);
        reference.ReadBits(1);
        uint expected = 0;
        for (int i = 0; i < 4; i++)
            expected = (expected << 8) | (uint)reference.ReadBits(8);

        var decoder = new BitPackedDecoder(data);
        decoder.ReadBool();

        Assert.AreEqual(new FourCC(expected), decoder.ReadFourCC());
    }

    [TestMethod]
    public void ReadReal32AndReal64_BigEndian()
    {
        var decoder = new BitPackedDecoder(new byte[] { 0x3F, 0xC0, 0x00, 0x00, 0x3F, 0xF8, 0, 0, 0, 0, 0, 0 });

        Assert.AreEqual(1.5f, decoder.ReadReal32());
        Assert.AreEqual(1.5d, decoder.ReadReal64());
    }
}
