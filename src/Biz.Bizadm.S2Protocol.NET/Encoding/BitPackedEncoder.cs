using System.Buffers.Binary;

namespace Biz.Bizadm.S2Protocol.NET.Encoding;

// Decoding.BitPackedDecoder의 역. 범위 밖 값은 Python encoder의 assert처럼 거부한다.
public sealed class BitPackedEncoder
{
    private readonly BitPackedBufferWriter _writer = new();

    public long UsedBits => _writer.UsedBits;

    public void ByteAlign() => _writer.ByteAlign();

    public byte[] ToArray() => _writer.ToArray();

    public void WriteInt(long value, long offset, int bits)
    {
        ulong raw = unchecked((ulong)(value - offset));
        if (bits < 64 && raw >> bits != 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Value does not fit in ({offset}, {bits}).");
        _writer.WriteBits(raw, bits);
    }

    public void WriteUInt64(ulong value, int bits)
    {
        if (bits < 64 && value >> bits != 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Value does not fit in {bits} bits.");
        _writer.WriteBits(value, bits);
    }

    public void WriteBool(bool value) => _writer.WriteBits(value ? 1UL : 0UL, 1);

    public void WriteOptionalExists(bool exists) => WriteBool(exists);

    public void WriteBlob(byte[] value, long offset, int bits)
    {
        WriteInt(value.Length, offset, bits);
        _writer.WriteAlignedBytes(value);
    }

    public void WriteBitArray(S2BitArray value, long offset, int bits)
    {
        WriteInt(value.Length, offset, bits);
        _writer.WriteBitsFromBytes(value.Data, value.Length);
    }

    // 디코더와 같이 8비트씩 4회. 비정렬 위치에서 WriteBits(value, 32)와 다르다.
    public void WriteFourCC(FourCC value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value.Value);
        _writer.WriteUnalignedBytes(bytes);
    }

    public void WriteReal32(float value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteSingleBigEndian(bytes, value);
        _writer.WriteUnalignedBytes(bytes);
    }

    public void WriteReal64(double value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(bytes, value);
        _writer.WriteUnalignedBytes(bytes);
    }

    // 생성 코드용: 길이(offset, bits) 뒤 요소들.
    public void WriteArray<T>(T[] values, long offset, int bits, Action<T> writeElement)
    {
        WriteInt(values.Length, offset, bits);
        foreach (var value in values)
            writeElement(value);
    }

    public void WriteOptional<T>(T? value, Action<T> write) where T : class
    {
        WriteOptionalExists(value is not null);
        if (value is not null) write(value);
    }

    public void WriteOptionalValue<T>(T? value, Action<T> write) where T : struct
    {
        WriteOptionalExists(value.HasValue);
        if (value.HasValue) write(value.Value);
    }
}
