namespace Biz.Bizadm.S2Protocol.NET.Decoding;

// s2protocol decoders.BitPackedBuffer: 바이트 안에서는 LSB부터 소비하고,
// 빅엔디언이면 먼저 읽은 청크가 결과의 상위 비트가 된다.

/// <summary>
/// Bit reader of the s2protocol <c>BitPackedBuffer</c>: bits are taken from the low end of each byte, and in big-endian
/// mode the first chunk read becomes the most significant bits of the value.
/// </summary>
/// <param name="data">The bytes to read.</param>
/// <param name="bigEndian">Big-endian value assembly (protocol data); little-endian is used for replay attributes.</param>
public sealed class BitPackedBuffer(ReadOnlyMemory<byte> data, bool bigEndian = true)
{
    private readonly ReadOnlyMemory<byte> _data = data;
    private readonly bool _bigEndian = bigEndian;
    private int _used;
    private int _next;
    private int _nextBits;

    /// <summary>Whether all bits have been read.</summary>
    public bool Done => _nextBits == 0 && _used >= _data.Length;

    /// <summary>Number of bits read so far.</summary>
    public long UsedBits => _used * 8L - _nextBits;

    // 아직 손대지 않은 바이트 수(현재 바이트의 남은 비트는 제외). 길이 값으로 미리 할당하기 전 검사용.
    internal int RemainingBytes => _data.Length - _used;

    /// <summary>Skips the rest of the current byte.</summary>
    public void ByteAlign() => _nextBits = 0;

    /// <summary>Reads up to 64 bits as one value.</summary>
    /// <param name="bits">Number of bits (0 to 64).</param>
    /// <returns>The value.</returns>
    /// <exception cref="CorruptedReplayException">The data ends first.</exception>
    public ulong ReadBits(int bits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bits);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bits, 64);

        ulong result = 0;
        int resultBits = 0;
        while (resultBits != bits)
        {
            int copyBits = ReadChunk(bits - resultBits, out int copy);
            int shift = _bigEndian ? bits - resultBits - copyBits : resultBits;
            result |= (ulong)copy << shift;
            resultBits += copyBits;
        }
        return result;
    }

    /// <summary>Reads any number of bits as one value, returned as big-endian bytes.</summary>
    /// <param name="bits">Number of bits.</param>
    /// <returns>The value in <c>(bits + 7) / 8</c> big-endian bytes.</returns>
    // ReadBits(bits)가 만들 정수를 빅엔디언 바이트((bits+7)/8개)로 돌려준다. 64비트를 넘는 bitarray용.
    public byte[] ReadBitsToBytes(int bits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bits);

        var result = new byte[(bits + 7) / 8];
        int resultBits = 0;
        while (resultBits != bits)
        {
            int copyBits = ReadChunk(bits - resultBits, out int copy);
            int shift = _bigEndian ? bits - resultBits - copyBits : resultBits;
            int index = result.Length - 1 - shift / 8;
            int offset = shift % 8;
            result[index] |= (byte)(copy << offset);
            if (offset + copyBits > 8)
                result[index - 1] |= (byte)(copy >> (8 - offset));
            resultBits += copyBits;
        }
        return result;
    }

    /// <summary>Skips the rest of the current byte, then reads whole bytes.</summary>
    /// <param name="bytes">Number of bytes.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="CorruptedReplayException">The data ends first.</exception>
    public byte[] ReadAlignedBytes(int bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);

        ByteAlign();
        if (_data.Length - _used < bytes)
            throw new CorruptedReplayException("Truncated: not enough bytes for an aligned read.");
        byte[] result = _data.Slice(_used, bytes).ToArray();
        _used += bytes;
        return result;
    }

    /// <summary>Reads bytes as 8-bit values from the current bit position.</summary>
    /// <param name="bytes">Number of bytes.</param>
    /// <returns>The bytes.</returns>
    public byte[] ReadUnalignedBytes(int bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);

        var result = new byte[bytes];
        for (int i = 0; i < bytes; i++)
            result[i] = (byte)ReadBits(8);
        return result;
    }

    private int ReadChunk(int remainingBits, out int copy)
    {
        if (_nextBits == 0)
        {
            if (_used >= _data.Length)
                throw new CorruptedReplayException("Truncated: no more bytes to read.");
            _next = _data.Span[_used++];
            _nextBits = 8;
        }
        int copyBits = Math.Min(remainingBits, _nextBits);
        copy = _next & ((1 << copyBits) - 1);
        _next >>= copyBits;
        _nextBits -= copyBits;
        return copyBits;
    }
}
