using System.Buffers.Binary;

namespace Biz.Bizadm.S2Protocol.NET.Decoding;

// s2protocol decoders.BitPackedDecoder의 기본 연산. 타입 트리 순회는 생성 코드가 한다.
// array 길이와 choice 태그는 ReadInt로 읽는다.
public sealed class BitPackedDecoder(ReadOnlyMemory<byte> data)
{
    private readonly BitPackedBuffer _buffer = new(data);

    public bool Done => _buffer.Done;

    public long UsedBits => _buffer.UsedBits;

    public void ByteAlign() => _buffer.ByteAlign();

    // (-2^63, 64) 같은 경계도 Python의 offset + read_bits(bits)와 같도록 unchecked로 더한다.
    public long ReadInt(long offset, int bits) => unchecked(offset + (long)_buffer.ReadBits(bits));

    // (0, 64)처럼 long에 들어가지 않는 범위.
    public ulong ReadUInt64(int bits) => _buffer.ReadBits(bits);

    public bool ReadBool() => ReadInt(0, 1) != 0;

    public bool ReadOptionalExists() => ReadBool();

    public byte[] ReadBlob(long offset, int bits) => _buffer.ReadAlignedBytes(checked((int)ReadInt(offset, bits)));

    public S2BitArray ReadBitArray(long offset, int bits)
    {
        int length = checked((int)ReadInt(offset, bits));
        return new S2BitArray(length, _buffer.ReadBitsToBytes(length));
    }

    // Python은 read_unaligned_bytes(4)(8비트 4회)로 읽는다. 비정렬 위치에서는 ReadBits(32)와 결과가 다르다.
    public FourCC ReadFourCC() => new(BinaryPrimitives.ReadUInt32BigEndian(_buffer.ReadUnalignedBytes(4)));

    public float ReadReal32() => BinaryPrimitives.ReadSingleBigEndian(_buffer.ReadUnalignedBytes(4));

    public double ReadReal64() => BinaryPrimitives.ReadDoubleBigEndian(_buffer.ReadUnalignedBytes(8));

    // 생성 코드용: 길이(offset, bits) 뒤 요소들.
    public T[] ReadArray<T>(long offset, int bits, Func<T> readElement)
    {
        var result = new T[checked((int)ReadInt(offset, bits))];
        for (int i = 0; i < result.Length; i++)
            result[i] = readElement();
        return result;
    }

    public T? ReadOptional<T>(Func<T> read) where T : class => ReadOptionalExists() ? read() : null;

    public T? ReadOptionalValue<T>(Func<T> read) where T : struct => ReadOptionalExists() ? read() : null;
}
