using Biz.Bizadm.S2Protocol.NET.Decoding;

namespace Biz.Bizadm.S2Protocol.NET.Tests.Decoding;

// vint 첫 바이트: bit0 부호, bit1~6 하위 6비트, bit7 계속. 이후 바이트는 7비트씩.
[TestClass]
public sealed class VersionedDecoderTests
{
    [TestMethod]
    [DataRow(new byte[] { 0x00 }, 0L)]
    [DataRow(new byte[] { 0x02 }, 1L)]
    [DataRow(new byte[] { 0x03 }, -1L)]
    [DataRow(new byte[] { 0x7E }, 63L)]
    [DataRow(new byte[] { 0x80, 0x01 }, 64L)]
    [DataRow(new byte[] { 0x81, 0x01 }, -64L)]
    [DataRow(new byte[] { 0xD8, 0x04 }, 300L)]
    [DataRow(new byte[] { 0x81, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x02 }, long.MinValue)]
    public void ReadVInt(byte[] data, long expected)
    {
        var decoder = new VersionedDecoder(data);

        Assert.AreEqual(expected, decoder.ReadVInt());
        Assert.IsTrue(decoder.Done);
    }

    [TestMethod]
    public void ReadVInt_MoreThan64Bits_Throws()
    {
        var decoder = new VersionedDecoder(new byte[] { 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x07 });

        Assert.ThrowsExactly<CorruptedReplayException>(() => decoder.ReadVInt());
    }

    [TestMethod]
    public void ReadInt_ChecksSkipCode()
    {
        Assert.AreEqual(1L, new VersionedDecoder(new byte[] { 0x09, 0x02 }).ReadInt());
        Assert.ThrowsExactly<CorruptedReplayException>(() => new VersionedDecoder(new byte[] { 0x06, 0x02 }).ReadInt());
    }

    [TestMethod]
    public void ReadUInt64_FullRange()
    {
        var decoder = new VersionedDecoder(new byte[] { 0x09, 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x03 });

        Assert.AreEqual(ulong.MaxValue, decoder.ReadUInt64());
    }

    [TestMethod]
    public void ReadUInt64_Negative_Throws()
    {
        Assert.ThrowsExactly<CorruptedReplayException>(() => new VersionedDecoder(new byte[] { 0x09, 0x03 }).ReadUInt64());
    }

    [TestMethod]
    public void ReadScalars()
    {
        var decoder = new VersionedDecoder(new byte[]
        {
            0x06, 0x01,                               // bool
            0x04, 0x00,                               // optional 없음
            0x04, 0x01,                               // optional 있음
            0x02, 0x04, 0xAA, 0xBB,                   // blob 길이 2
            0x01, 0x12, 0x01, 0xFF,                   // bitarray 길이 9 → 2바이트
            0x07, 0x73, 0x32, 0x6D, 0x61,             // fourcc "s2ma"
            0x07, 0x3F, 0xC0, 0x00, 0x00,             // real32 1.5
            0x08, 0x3F, 0xF8, 0, 0, 0, 0, 0, 0,       // real64 1.5
            0x00, 0x04,                               // array 길이 2
            0x03, 0x0A,                               // choice 태그 5
            0x05, 0x06,                               // struct 필드 3개
        });

        Assert.IsTrue(decoder.ReadBool());
        Assert.IsFalse(decoder.ReadOptionalExists());
        Assert.IsTrue(decoder.ReadOptionalExists());
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB }, decoder.ReadBlob());
        var bits = decoder.ReadBitArray();
        Assert.AreEqual(9, bits.Length);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0xFF }, bits.Data);
        Assert.AreEqual("s2ma", decoder.ReadFourCC().ToString());
        Assert.AreEqual(1.5f, decoder.ReadReal32());
        Assert.AreEqual(1.5d, decoder.ReadReal64());
        Assert.AreEqual(2L, decoder.ReadArrayLength());
        Assert.AreEqual(5L, decoder.ReadChoiceTag());
        Assert.AreEqual(3L, decoder.ReadStructFieldCount());
        Assert.IsTrue(decoder.Done);
    }

    [TestMethod]
    [DataRow(0, new byte[] { 0x00, 0x04, 0x09, 0x02, 0x06, 0x01 })]           // array: [vint 1, u8]
    [DataRow(1, new byte[] { 0x01, 0x12, 0xAA, 0xBB })]                       // bitblob 9비트 → 2바이트
    [DataRow(2, new byte[] { 0x02, 0x04, 0x01, 0x02 })]                       // blob 2바이트
    [DataRow(3, new byte[] { 0x03, 0x02, 0x09, 0x02 })]                       // choice 태그 1, vint
    [DataRow(4, new byte[] { 0x04, 0x01, 0x09, 0x02 })]                       // optional 있음
    [DataRow(4, new byte[] { 0x04, 0x00 })]                                   // optional 없음
    [DataRow(5, new byte[] { 0x05, 0x04, 0x00, 0x09, 0x02, 0x02, 0x06, 0x01 })] // struct 2필드
    [DataRow(6, new byte[] { 0x06, 0xFF })]                                   // u8
    [DataRow(7, new byte[] { 0x07, 0x01, 0x02, 0x03, 0x04 })]                 // u32
    [DataRow(8, new byte[] { 0x08, 1, 2, 3, 4, 5, 6, 7, 8 })]                 // u64
    [DataRow(9, new byte[] { 0x09, 0x80, 0x01 })]                             // vint
    public void SkipInstance_ConsumesWholeValue(int skipCode, byte[] data)
    {
        var decoder = new VersionedDecoder(data);

        decoder.SkipInstance();

        Assert.IsTrue(decoder.Done, $"skip code {skipCode}");
    }

    [TestMethod]
    public void Struct_UnknownTag_IsSkipped()
    {
        // 생성 코드의 versioned struct 읽기 패턴(개발 문서 단위 5)
        var decoder = new VersionedDecoder(new byte[]
        {
            0x05, 0x06,                 // struct 필드 3개
            0x00, 0x09, 0x0A,           // 태그 0: int 5
            0x0E, 0x02, 0x04, 0xAA, 0xBB, // 태그 7(모름): blob
            0x02, 0x06, 0x01,           // 태그 1: bool true
        });

        long first = 0;
        bool second = false;
        for (long n = decoder.ReadStructFieldCount(); n > 0; n--)
        {
            switch (decoder.ReadVInt())
            {
                case 0: first = decoder.ReadInt(); break;
                case 1: second = decoder.ReadBool(); break;
                default: decoder.SkipInstance(); break;
            }
        }

        Assert.AreEqual(5L, first);
        Assert.IsTrue(second);
        Assert.IsTrue(decoder.Done);
    }
}
