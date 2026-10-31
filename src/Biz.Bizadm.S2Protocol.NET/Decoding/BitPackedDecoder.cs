using System.Buffers.Binary;

namespace Biz.Bizadm.S2Protocol.NET.Decoding;

// s2protocol decoders.BitPackedDecoder의 기본 연산. 타입 트리 순회는 생성 코드가 한다.
// array 길이와 choice 태그는 ReadInt로 읽는다.

/// <summary>
/// Primitive reads of the s2protocol bit-packed format (initdata, game and message events). Generated code walks the types.
/// </summary>
/// <param name="data">The encoded bytes.</param>
public sealed class BitPackedDecoder(ReadOnlyMemory<byte> data)
{
    private readonly BitPackedBuffer _buffer = new(data);

    /// <summary>Whether all bits have been read.</summary>
    public bool Done => _buffer.Done;

    /// <summary>Number of bits read so far.</summary>
    public long UsedBits => _buffer.UsedBits;

    /// <summary>Skips the rest of the current byte.</summary>
    public void ByteAlign() => _buffer.ByteAlign();

    /// <summary>Reads an integer of <paramref name="bits"/> bits and adds <paramref name="offset"/>.</summary>
    /// <param name="offset">The lower bound of the value range.</param>
    /// <param name="bits">Number of bits (0 to 64).</param>
    /// <returns>The value.</returns>
    // (-2^63, 64) 같은 경계도 Python의 offset + read_bits(bits)와 같도록 unchecked로 더한다.
    public long ReadInt(long offset, int bits) => unchecked(offset + (long)_buffer.ReadBits(bits));

    /// <summary>Reads an unsigned integer whose range does not fit <see cref="long"/>.</summary>
    /// <param name="bits">Number of bits (0 to 64).</param>
    /// <returns>The value.</returns>
    // (0, 64)처럼 long에 들어가지 않는 범위.
    public ulong ReadUInt64(int bits) => _buffer.ReadBits(bits);

    /// <summary>Reads a one-bit boolean.</summary>
    /// <returns>The value.</returns>
    public bool ReadBool() => ReadInt(0, 1) != 0;

    /// <summary>Reads the one-bit presence flag of an optional value.</summary>
    /// <returns>Whether the value follows.</returns>
    public bool ReadOptionalExists() => ReadBool();

    /// <summary>Reads a length, then that many byte-aligned bytes.</summary>
    /// <param name="offset">The lower bound of the length.</param>
    /// <param name="bits">Number of bits of the length.</param>
    /// <returns>The bytes.</returns>
    public byte[] ReadBlob(long offset, int bits) => _buffer.ReadAlignedBytes(checked((int)ReadInt(offset, bits)));

    /// <summary>Reads a length, then that many bits.</summary>
    /// <param name="offset">The lower bound of the length.</param>
    /// <param name="bits">Number of bits of the length.</param>
    /// <returns>The bit array.</returns>
    public S2BitArray ReadBitArray(long offset, int bits)
    {
        int length = checked((int)ReadInt(offset, bits));
        return new S2BitArray(length, _buffer.ReadBitsToBytes(length));
    }

    /// <summary>Reads four 8-bit characters.</summary>
    /// <returns>The four-character code.</returns>
    // Python은 read_unaligned_bytes(4)(8비트 4회)로 읽는다. 비정렬 위치에서는 ReadBits(32)와 결과가 다르다.
    public FourCC ReadFourCC() => new(BinaryPrimitives.ReadUInt32BigEndian(_buffer.ReadUnalignedBytes(4)));

    /// <summary>Reads a big-endian 32-bit float.</summary>
    /// <returns>The value.</returns>
    public float ReadReal32() => BinaryPrimitives.ReadSingleBigEndian(_buffer.ReadUnalignedBytes(4));

    /// <summary>Reads a big-endian 64-bit float.</summary>
    /// <returns>The value.</returns>
    public double ReadReal64() => BinaryPrimitives.ReadDoubleBigEndian(_buffer.ReadUnalignedBytes(8));

    /// <summary>Reads a length, then that many elements.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="offset">The lower bound of the length.</param>
    /// <param name="bits">Number of bits of the length.</param>
    /// <param name="readElement">Reads one element.</param>
    /// <returns>The elements.</returns>
    // 생성 코드용: 길이(offset, bits) 뒤 요소들.
    public T[] ReadArray<T>(long offset, int bits, Func<T> readElement)
    {
        var result = new T[checked((int)ReadInt(offset, bits))];
        for (int i = 0; i < result.Length; i++)
            result[i] = readElement();
        return result;
    }

    /// <summary>Reads an optional reference-type value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="read">Reads the value when present.</param>
    /// <returns>The value, or <see langword="null"/> when absent.</returns>
    public T? ReadOptional<T>(Func<T> read) where T : class => ReadOptionalExists() ? read() : null;

    /// <summary>Reads an optional value-type value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="read">Reads the value when present.</param>
    /// <returns>The value, or <see langword="null"/> when absent.</returns>
    public T? ReadOptionalValue<T>(Func<T> read) where T : struct => ReadOptionalExists() ? read() : null;
}
