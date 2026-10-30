using System.Buffers.Binary;

namespace Biz.Bizadm.S2Protocol.NET.Encoding;

// Decoding.VersionedDecoder의 역. 값마다 skip 바이트를 먼저 쓴다.
// struct는 WriteStructFieldCount 뒤에 필드마다 WriteVInt(태그)와 값을 쓴다.
public sealed class VersionedEncoder
{
    private const int SkipArray = 0;
    private const int SkipBitBlob = 1;
    private const int SkipBlob = 2;
    private const int SkipChoice = 3;
    private const int SkipOptional = 4;
    private const int SkipStruct = 5;
    private const int SkipU8 = 6;
    private const int SkipU32 = 7;
    private const int SkipU64 = 8;
    private const int SkipVInt = 9;

    private readonly BitPackedBufferWriter _writer = new();

    public long UsedBits => _writer.UsedBits;

    public byte[] ToArray() => _writer.ToArray();

    public void WriteVInt(long value)
    {
        // -2^63도 크기로 표현되도록 부호와 크기를 나눈다.
        ulong magnitude = value < 0 ? (ulong)(-(value + 1)) + 1 : (ulong)value;
        WriteVIntMagnitude(magnitude, value < 0);
    }

    public void WriteInt(long value)
    {
        WriteSkip(SkipVInt);
        WriteVInt(value);
    }

    public void WriteUInt64(ulong value)
    {
        WriteSkip(SkipVInt);
        WriteVIntMagnitude(value, negative: false);
    }

    public void WriteBool(bool value)
    {
        WriteSkip(SkipU8);
        _writer.WriteBits(value ? 1UL : 0UL, 8);
    }

    public void WriteOptionalExists(bool exists)
    {
        WriteSkip(SkipOptional);
        _writer.WriteBits(exists ? 1UL : 0UL, 8);
    }

    public void WriteArrayLength(long length)
    {
        WriteSkip(SkipArray);
        WriteVInt(length);
    }

    public void WriteChoiceTag(long tag)
    {
        WriteSkip(SkipChoice);
        WriteVInt(tag);
    }

    public void WriteStructFieldCount(long count)
    {
        WriteSkip(SkipStruct);
        WriteVInt(count);
    }

    public void WriteBlob(byte[] value)
    {
        WriteSkip(SkipBlob);
        WriteVInt(value.Length);
        _writer.WriteAlignedBytes(value);
    }

    public void WriteBitArray(S2BitArray value)
    {
        if (value.Data.Length != (value.Length + 7) / 8)
            throw new ArgumentException($"Expected {(value.Length + 7) / 8} bytes for {value.Length} bits.", nameof(value));
        WriteSkip(SkipBitBlob);
        WriteVInt(value.Length);
        _writer.WriteAlignedBytes(value.Data);
    }

    public void WriteFourCC(FourCC value)
    {
        WriteSkip(SkipU32);
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value.Value);
        _writer.WriteAlignedBytes(bytes);
    }

    public void WriteReal32(float value)
    {
        WriteSkip(SkipU32);
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteSingleBigEndian(bytes, value);
        _writer.WriteAlignedBytes(bytes);
    }

    public void WriteReal64(double value)
    {
        WriteSkip(SkipU64);
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(bytes, value);
        _writer.WriteAlignedBytes(bytes);
    }

    private void WriteSkip(int code) => _writer.WriteBits((ulong)code, 8);

    // 첫 바이트: bit0 부호, bit1~6 하위 6비트, bit7 계속. 이후 바이트: 7비트씩.
    private void WriteVIntMagnitude(ulong magnitude, bool negative)
    {
        int b = (int)((magnitude & 0x3f) << 1) | (negative ? 1 : 0);
        magnitude >>= 6;
        while (true)
        {
            if (magnitude != 0)
                b |= 0x80;
            _writer.WriteBits((ulong)b, 8);
            if (magnitude == 0)
                break;
            b = (int)(magnitude & 0x7f);
            magnitude >>= 7;
        }
    }
}
