namespace Biz.Bizadm.S2Protocol.NET.Decoding;

// s2protocol decoders.BitPackedBuffer: 바이트 안에서는 LSB부터 소비하고,
// 빅엔디언이면 먼저 읽은 청크가 결과의 상위 비트가 된다.
public sealed class BitPackedBuffer(ReadOnlyMemory<byte> data, bool bigEndian = true)
{
    private readonly ReadOnlyMemory<byte> _data = data;
    private readonly bool _bigEndian = bigEndian;
    private int _used;
    private int _next;
    private int _nextBits;

    public bool Done => _nextBits == 0 && _used >= _data.Length;

    public long UsedBits => _used * 8L - _nextBits;

    public void ByteAlign() => _nextBits = 0;

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
