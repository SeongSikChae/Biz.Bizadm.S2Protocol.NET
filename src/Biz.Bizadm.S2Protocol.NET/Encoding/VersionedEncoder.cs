using System.Buffers.Binary;

namespace Biz.Bizadm.S2Protocol.NET.Encoding;

// Decoding.VersionedDecoder의 역. 값마다 skip 바이트를 먼저 쓴다.
// struct는 WriteStructFieldCount 뒤에 필드마다 WriteVInt(태그)와 값을 쓴다.
/// <summary>Primitive writes of the versioned format, the exact inverse of <see cref="Decoding.VersionedDecoder"/>.</summary>
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

    /// <summary>Number of bits written so far.</summary>
    public long UsedBits => _writer.UsedBits;

    /// <summary>Returns the written bytes.</summary>
    /// <returns>The bytes.</returns>
    public byte[] ToArray() => _writer.ToArray();

    /// <summary>Writes a variable-length integer without a skip code (used for struct field tags).</summary>
    /// <param name="value">The value.</param>
    public void WriteVInt(long value)
    {
        // -2^63도 크기로 표현되도록 부호와 크기를 나눈다.
        ulong magnitude = value < 0 ? (ulong)(-(value + 1)) + 1 : (ulong)value;
        WriteVIntMagnitude(magnitude, value < 0);
    }

    /// <summary>Writes an integer value.</summary>
    /// <param name="value">The value.</param>
    public void WriteInt(long value)
    {
        WriteSkip(SkipVInt);
        WriteVInt(value);
    }

    /// <summary>Writes an unsigned integer whose range does not fit <see cref="long"/>.</summary>
    /// <param name="value">The value.</param>
    public void WriteUInt64(ulong value)
    {
        WriteSkip(SkipVInt);
        WriteVIntMagnitude(value, negative: false);
    }

    /// <summary>Writes a boolean value.</summary>
    /// <param name="value">The value.</param>
    public void WriteBool(bool value)
    {
        WriteSkip(SkipU8);
        _writer.WriteBits(value ? 1UL : 0UL, 8);
    }

    /// <summary>Writes the presence flag of an optional value.</summary>
    /// <param name="exists">Whether the value follows.</param>
    public void WriteOptionalExists(bool exists)
    {
        WriteSkip(SkipOptional);
        _writer.WriteBits(exists ? 1UL : 0UL, 8);
    }

    /// <summary>Writes the length of an array.</summary>
    /// <param name="length">The length.</param>
    public void WriteArrayLength(long length)
    {
        WriteSkip(SkipArray);
        WriteVInt(length);
    }

    /// <summary>Writes the tag of a choice.</summary>
    /// <param name="tag">The tag.</param>
    public void WriteChoiceTag(long tag)
    {
        WriteSkip(SkipChoice);
        WriteVInt(tag);
    }

    /// <summary>Writes the number of struct fields; each field then starts with its tag (<see cref="WriteVInt"/>).</summary>
    /// <param name="count">The field count.</param>
    public void WriteStructFieldCount(long count)
    {
        WriteSkip(SkipStruct);
        WriteVInt(count);
    }

    /// <summary>Writes a byte string.</summary>
    /// <param name="value">The bytes.</param>
    public void WriteBlob(byte[] value)
    {
        WriteSkip(SkipBlob);
        WriteVInt(value.Length);
        _writer.WriteAlignedBytes(value);
    }

    /// <summary>Writes a bit array.</summary>
    /// <param name="value">The bit array.</param>
    public void WriteBitArray(S2BitArray value)
    {
        if (value.Data.Length != (value.Length + 7) / 8)
            throw new ArgumentException($"Expected {(value.Length + 7) / 8} bytes for {value.Length} bits.", nameof(value));
        WriteSkip(SkipBitBlob);
        WriteVInt(value.Length);
        _writer.WriteAlignedBytes(value.Data);
    }

    /// <summary>Writes a four-character code.</summary>
    /// <param name="value">The code.</param>
    public void WriteFourCC(FourCC value)
    {
        WriteSkip(SkipU32);
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value.Value);
        _writer.WriteAlignedBytes(bytes);
    }

    /// <summary>Writes a big-endian 32-bit float.</summary>
    /// <param name="value">The value.</param>
    public void WriteReal32(float value)
    {
        WriteSkip(SkipU32);
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteSingleBigEndian(bytes, value);
        _writer.WriteAlignedBytes(bytes);
    }

    /// <summary>Writes a big-endian 64-bit float.</summary>
    /// <param name="value">The value.</param>
    public void WriteReal64(double value)
    {
        WriteSkip(SkipU64);
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(bytes, value);
        _writer.WriteAlignedBytes(bytes);
    }

    // 생성 코드용: 길이(vint) 뒤 요소들.
    /// <summary>Writes an array.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="values">The elements.</param>
    /// <param name="writeElement">Writes one element.</param>
    public void WriteArray<T>(T[] values, Action<T> writeElement)
    {
        WriteArrayLength(values.Length);
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
