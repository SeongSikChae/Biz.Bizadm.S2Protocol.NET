namespace Biz.Bizadm.S2Protocol.NET.Encoding;

// Decoding.BitPackedBuffer의 역. 현재 바이트의 다음 빈 비트(LSB 쪽)부터 채우고,
// 빅엔디언이면 값의 상위 비트 청크부터 쓴다. 청크 경계가 읽기와 같아야 하므로
// 하나의 값은 한 번의 호출로 써야 한다.
/// <summary>Bit writer, the exact inverse of <see cref="Decoding.BitPackedBuffer"/>.</summary>
/// <param name="bigEndian">Big-endian value layout (protocol data); little-endian is used for replay attributes.</param>
public sealed class BitPackedBufferWriter(bool bigEndian = true)
{
    private readonly bool _bigEndian = bigEndian;
    private readonly MemoryStream _data = new();
    private int _next;
    private int _nextBits;

    /// <summary>Number of bits written so far.</summary>
    public long UsedBits => _data.Length * 8 + _nextBits;

    /// <summary>Pads the current byte with zero bits.</summary>
    public void ByteAlign()
    {
        if (_nextBits > 0)
            Flush();
    }

    /// <summary>Writes up to 64 bits as one value. A value must be written in one call to match how it is read.</summary>
    /// <param name="value">The value.</param>
    /// <param name="bits">Number of bits (0 to 64).</param>
    public void WriteBits(ulong value, int bits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bits);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bits, 64);

        int written = 0;
        while (written != bits)
        {
            int copyBits = Math.Min(bits - written, 8 - _nextBits);
            int shift = _bigEndian ? bits - written - copyBits : written;
            WriteChunk((int)(value >> shift) & ((1 << copyBits) - 1), copyBits);
            written += copyBits;
        }
    }

    // BitPackedBuffer.ReadBitsToBytes의 역. data는 bits 비트 정수의 빅엔디언 표현((bits+7)/8바이트).
    /// <summary>Writes any number of bits as one value given as big-endian bytes (the inverse of <c>ReadBitsToBytes</c>).</summary>
    /// <param name="data">The value in <c>(bits + 7) / 8</c> big-endian bytes.</param>
    /// <param name="bits">Number of bits.</param>
    public void WriteBitsFromBytes(ReadOnlySpan<byte> data, int bits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bits);
        if (data.Length != (bits + 7) / 8)
            throw new ArgumentException($"Expected {(bits + 7) / 8} bytes for {bits} bits.", nameof(data));

        int written = 0;
        while (written != bits)
        {
            int copyBits = Math.Min(bits - written, 8 - _nextBits);
            int shift = _bigEndian ? bits - written - copyBits : written;
            int index = data.Length - 1 - shift / 8;
            int offset = shift % 8;
            int copy = data[index] >> offset;
            if (offset + copyBits > 8)
                copy |= data[index - 1] << (8 - offset);
            WriteChunk(copy & ((1 << copyBits) - 1), copyBits);
            written += copyBits;
        }
    }

    /// <summary>Pads the current byte, then writes whole bytes.</summary>
    /// <param name="data">The bytes.</param>
    public void WriteAlignedBytes(ReadOnlySpan<byte> data)
    {
        ByteAlign();
        _data.Write(data);
    }

    /// <summary>Writes bytes as 8-bit values from the current bit position.</summary>
    /// <param name="data">The bytes.</param>
    public void WriteUnalignedBytes(ReadOnlySpan<byte> data)
    {
        foreach (byte b in data)
            WriteBits(b, 8);
    }

    // 채우는 중인 바이트가 있으면 남는 비트를 0으로 둔 채 포함한다. 상태는 바꾸지 않는다.
    /// <summary>Returns the written bytes, including a partially filled last byte.</summary>
    /// <returns>The bytes.</returns>
    public byte[] ToArray()
    {
        byte[] written = _data.ToArray();
        if (_nextBits == 0)
            return written;
        var result = new byte[written.Length + 1];
        written.CopyTo(result, 0);
        result[^1] = (byte)_next;
        return result;
    }

    private void WriteChunk(int copy, int copyBits)
    {
        _next |= copy << _nextBits;
        _nextBits += copyBits;
        if (_nextBits == 8)
            Flush();
    }

    private void Flush()
    {
        _data.WriteByte((byte)_next);
        _next = 0;
        _nextBits = 0;
    }
}
