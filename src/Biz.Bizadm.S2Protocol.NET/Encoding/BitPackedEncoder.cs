using System.Buffers.Binary;

namespace Biz.Bizadm.S2Protocol.NET.Encoding;

// Decoding.BitPackedDecoder의 역. 범위 밖 값은 Python encoder의 assert처럼 거부한다.
/// <summary>Primitive writes of the bit-packed format, the exact inverse of <see cref="Decoding.BitPackedDecoder"/>.</summary>
public sealed class BitPackedEncoder
{
    private readonly BitPackedBufferWriter _writer = new();

    /// <summary>Number of bits written so far.</summary>
    public long UsedBits => _writer.UsedBits;

    /// <summary>Pads the current byte with zero bits.</summary>
    public void ByteAlign() => _writer.ByteAlign();

    /// <summary>Returns the written bytes.</summary>
    /// <returns>The bytes.</returns>
    public byte[] ToArray() => _writer.ToArray();

    /// <summary>Writes <c>value - offset</c> in <paramref name="bits"/> bits.</summary>
    /// <param name="value">The value.</param>
    /// <param name="offset">The lower bound of the value range.</param>
    /// <param name="bits">Number of bits (0 to 64).</param>
    /// <exception cref="ArgumentOutOfRangeException">The value does not fit the range.</exception>
    public void WriteInt(long value, long offset, int bits)
    {
        ulong raw = unchecked((ulong)(value - offset));
        if (bits < 64 && raw >> bits != 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Value does not fit in ({offset}, {bits}).");
        _writer.WriteBits(raw, bits);
    }

    /// <summary>Writes an unsigned integer whose range does not fit <see cref="long"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="bits">Number of bits (0 to 64).</param>
    /// <exception cref="ArgumentOutOfRangeException">The value does not fit the range.</exception>
    public void WriteUInt64(ulong value, int bits)
    {
        if (bits < 64 && value >> bits != 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Value does not fit in {bits} bits.");
        _writer.WriteBits(value, bits);
    }

    /// <summary>Writes a one-bit boolean.</summary>
    /// <param name="value">The value.</param>
    public void WriteBool(bool value) => _writer.WriteBits(value ? 1UL : 0UL, 1);

    /// <summary>Writes the one-bit presence flag of an optional value.</summary>
    /// <param name="exists">Whether the value follows.</param>
    public void WriteOptionalExists(bool exists) => WriteBool(exists);

    /// <summary>Writes the length, then the byte-aligned bytes.</summary>
    /// <param name="value">The bytes.</param>
    /// <param name="offset">The lower bound of the length.</param>
    /// <param name="bits">Number of bits of the length.</param>
    public void WriteBlob(byte[] value, long offset, int bits)
    {
        WriteInt(value.Length, offset, bits);
        _writer.WriteAlignedBytes(value);
    }

    /// <summary>Writes the length, then the bits.</summary>
    /// <param name="value">The bit array.</param>
    /// <param name="offset">The lower bound of the length.</param>
    /// <param name="bits">Number of bits of the length.</param>
    public void WriteBitArray(S2BitArray value, long offset, int bits)
    {
        WriteInt(value.Length, offset, bits);
        _writer.WriteBitsFromBytes(value.Data, value.Length);
    }

    // 디코더와 같이 8비트씩 4회. 비정렬 위치에서 WriteBits(value, 32)와 다르다.
    /// <summary>Writes four 8-bit characters.</summary>
    /// <param name="value">The code.</param>
    public void WriteFourCC(FourCC value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value.Value);
        _writer.WriteUnalignedBytes(bytes);
    }

    /// <summary>Writes a big-endian 32-bit float.</summary>
    /// <param name="value">The value.</param>
    public void WriteReal32(float value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteSingleBigEndian(bytes, value);
        _writer.WriteUnalignedBytes(bytes);
    }

    /// <summary>Writes a big-endian 64-bit float.</summary>
    /// <param name="value">The value.</param>
    public void WriteReal64(double value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(bytes, value);
        _writer.WriteUnalignedBytes(bytes);
    }

    // 생성 코드용: 길이(offset, bits) 뒤 요소들.
    /// <summary>Writes the length, then the elements.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="values">The elements.</param>
    /// <param name="offset">The lower bound of the length.</param>
    /// <param name="bits">Number of bits of the length.</param>
    /// <param name="writeElement">Writes one element.</param>
    public void WriteArray<T>(T[] values, long offset, int bits, Action<T> writeElement)
    {
        WriteInt(values.Length, offset, bits);
        foreach (var value in values)
            writeElement(value);
    }

    /// <summary>Writes an optional reference-type value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value, or <see langword="null"/> when absent.</param>
    /// <param name="write">Writes the value when present.</param>
    public void WriteOptional<T>(T? value, Action<T> write) where T : class
    {
        WriteOptionalExists(value is not null);
        if (value is not null) write(value);
    }

    /// <summary>Writes an optional value-type value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value, or <see langword="null"/> when absent.</param>
    /// <param name="write">Writes the value when present.</param>
    public void WriteOptionalValue<T>(T? value, Action<T> write) where T : struct
    {
        WriteOptionalExists(value.HasValue);
        if (value.HasValue) write(value.Value);
    }
}
