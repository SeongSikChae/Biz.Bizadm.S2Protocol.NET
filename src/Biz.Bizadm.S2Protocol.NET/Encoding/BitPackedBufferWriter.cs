namespace Biz.Bizadm.S2Protocol.NET.Encoding;

// Decoding.BitPackedBuffer의 역. 현재 바이트의 다음 빈 비트(LSB 쪽)부터 채우고,
// 빅엔디언이면 값의 상위 비트 청크부터 쓴다. 청크 경계가 읽기와 같아야 하므로
// 하나의 값은 한 번의 호출로 써야 한다.
public sealed class BitPackedBufferWriter(bool bigEndian = true)
{
    private readonly bool _bigEndian = bigEndian;
    private readonly MemoryStream _data = new();
    private int _next;
    private int _nextBits;

    public long UsedBits => _data.Length * 8 + _nextBits;

    public void ByteAlign()
    {
        if (_nextBits > 0)
            Flush();
    }

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

    public void WriteAlignedBytes(ReadOnlySpan<byte> data)
    {
        ByteAlign();
        _data.Write(data);
    }

    public void WriteUnalignedBytes(ReadOnlySpan<byte> data)
    {
        foreach (byte b in data)
            WriteBits(b, 8);
    }

    // 채우는 중인 바이트가 있으면 남는 비트를 0으로 둔 채 포함한다. 상태는 바꾸지 않는다.
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
