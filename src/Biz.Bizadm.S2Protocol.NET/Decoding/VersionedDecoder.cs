using System.Buffers.Binary;

namespace Biz.Bizadm.S2Protocol.NET.Decoding;

// s2protocol decoders.VersionedDecoder의 기본 연산. 값마다 skip 바이트(종류 코드)가 앞서고
// 정수·길이·태그는 vint다. struct/choice 순회와 모르는 태그의 SkipInstance 호출은 생성 코드가 한다.
public sealed class VersionedDecoder(ReadOnlyMemory<byte> data)
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

    private readonly BitPackedBuffer _buffer = new(data);

    public bool Done => _buffer.Done;

    public long UsedBits => _buffer.UsedBits;

    public void ByteAlign() => _buffer.ByteAlign();

    public void ExpectSkip(int expected)
    {
        int actual = (int)_buffer.ReadBits(8);
        if (actual != expected)
            throw new CorruptedReplayException($"Expected skip code {expected} but found {actual}.");
    }

    // -2^63도 표현하도록 unchecked로 부호를 붙인다.
    public long ReadVInt()
    {
        ulong magnitude = ReadVIntMagnitude(out bool negative);
        return negative ? unchecked(-(long)magnitude) : unchecked((long)magnitude);
    }

    public long ReadInt()
    {
        ExpectSkip(SkipVInt);
        return ReadVInt();
    }

    // (0, 64)처럼 long에 들어가지 않는 범위.
    public ulong ReadUInt64()
    {
        ExpectSkip(SkipVInt);
        ulong magnitude = ReadVIntMagnitude(out bool negative);
        if (negative && magnitude != 0)
            throw new CorruptedReplayException("Negative value for an unsigned 64-bit field.");
        return magnitude;
    }

    public bool ReadBool()
    {
        ExpectSkip(SkipU8);
        return _buffer.ReadBits(8) != 0;
    }

    public bool ReadOptionalExists()
    {
        ExpectSkip(SkipOptional);
        return _buffer.ReadBits(8) != 0;
    }

    public long ReadArrayLength()
    {
        ExpectSkip(SkipArray);
        return ReadVInt();
    }

    public long ReadChoiceTag()
    {
        ExpectSkip(SkipChoice);
        return ReadVInt();
    }

    // 이후 필드마다 ReadVInt()로 태그를 읽는다.
    public long ReadStructFieldCount()
    {
        ExpectSkip(SkipStruct);
        return ReadVInt();
    }

    public byte[] ReadBlob()
    {
        ExpectSkip(SkipBlob);
        return _buffer.ReadAlignedBytes(checked((int)ReadVInt()));
    }

    public S2BitArray ReadBitArray()
    {
        ExpectSkip(SkipBitBlob);
        int length = checked((int)ReadVInt());
        return new S2BitArray(length, _buffer.ReadAlignedBytes((length + 7) / 8));
    }

    public FourCC ReadFourCC()
    {
        ExpectSkip(SkipU32);
        return new FourCC(BinaryPrimitives.ReadUInt32BigEndian(_buffer.ReadAlignedBytes(4)));
    }

    public float ReadReal32()
    {
        ExpectSkip(SkipU32);
        return BinaryPrimitives.ReadSingleBigEndian(_buffer.ReadAlignedBytes(4));
    }

    public double ReadReal64()
    {
        ExpectSkip(SkipU64);
        return BinaryPrimitives.ReadDoubleBigEndian(_buffer.ReadAlignedBytes(8));
    }

    public void SkipInstance()
    {
        switch ((int)_buffer.ReadBits(8))
        {
            case SkipArray:
                for (long n = ReadVInt(); n > 0; n--) SkipInstance();
                break;
            case SkipBitBlob:
                _buffer.ReadAlignedBytes(checked((int)((ReadVInt() + 7) / 8)));
                break;
            case SkipBlob:
                _buffer.ReadAlignedBytes(checked((int)ReadVInt()));
                break;
            case SkipChoice:
                ReadVInt();
                SkipInstance();
                break;
            case SkipOptional:
                if (_buffer.ReadBits(8) != 0) SkipInstance();
                break;
            case SkipStruct:
                for (long n = ReadVInt(); n > 0; n--)
                {
                    ReadVInt();
                    SkipInstance();
                }
                break;
            case SkipU8:
                _buffer.ReadAlignedBytes(1);
                break;
            case SkipU32:
                _buffer.ReadAlignedBytes(4);
                break;
            case SkipU64:
                _buffer.ReadAlignedBytes(8);
                break;
            case SkipVInt:
                ReadVInt();
                break;
            // Python과 같이 알 수 없는 코드는 무시한다.
        }
    }

    // 첫 바이트: bit0 부호, bit1~6 하위 6비트, bit7 계속. 이후 바이트: 7비트씩.
    private ulong ReadVIntMagnitude(out bool negative)
    {
        int b = (int)_buffer.ReadBits(8);
        negative = (b & 1) != 0;
        ulong result = (ulong)((b >> 1) & 0x3f);
        int bits = 6;
        while ((b & 0x80) != 0)
        {
            b = (int)_buffer.ReadBits(8);
            ulong chunk = (ulong)(b & 0x7f);
            if (bits >= 64 || (bits > 57 && chunk >> (64 - bits) != 0))
                throw new CorruptedReplayException("vint exceeds 64 bits.");
            result |= chunk << bits;
            bits += 7;
        }
        return result;
    }
}
