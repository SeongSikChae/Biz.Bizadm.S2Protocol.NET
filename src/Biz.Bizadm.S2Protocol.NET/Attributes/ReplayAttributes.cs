using Biz.Bizadm.S2Protocol.NET.Decoding;
using Biz.Bizadm.S2Protocol.NET.Encoding;

namespace Biz.Bizadm.S2Protocol.NET.Attributes;

// s2protocol decode_replay_attributes_events(93개 버전에서 동일한 구현)의 포팅. 리틀엔디언이며,
// 헤더(source 8비트, mapNamespace 32비트, count 32비트) 뒤에 namespace·attrid(32비트), scope(8비트),
// 정렬된 4바이트 값이 끝까지 반복된다. count는 Python처럼 읽고 쓰지 않는다.
public sealed class ReplayAttributes
{
    // 빈 입력이면 Python({})처럼 헤더가 없다.
    public int? Source { get; init; }

    public uint? MapNamespace { get; init; }

    // 파일 순서 그대로. Python의 scopes 묶음은 순서를 잃으므로 인코딩은 이 목록으로 한다.
    public IReadOnlyList<ReplayAttribute> Attributes { get; init; } = [];

    // Python attributes['scopes'][scope][attrid] 와 같은 묶음.
    public IReadOnlyDictionary<int, IReadOnlyDictionary<uint, IReadOnlyList<ReplayAttribute>>> Scopes =>
        Attributes.GroupBy(a => (int)a.Scope).ToDictionary(
            s => s.Key,
            s => (IReadOnlyDictionary<uint, IReadOnlyList<ReplayAttribute>>)s.GroupBy(a => a.AttrId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<ReplayAttribute>)g.ToList()));

    public static ReplayAttributes Decode(ReadOnlyMemory<byte> data)
    {
        var buffer = new BitPackedBuffer(data, bigEndian: false);
        if (buffer.Done)
            return new ReplayAttributes();

        int source = (int)buffer.ReadBits(8);
        uint mapNamespace = (uint)buffer.ReadBits(32);
        buffer.ReadBits(32);   // count
        var attributes = new List<ReplayAttribute>();
        while (!buffer.Done)
        {
            uint ns = (uint)buffer.ReadBits(32);
            uint attrId = (uint)buffer.ReadBits(32);
            byte scope = (byte)buffer.ReadBits(8);
            attributes.Add(new ReplayAttribute(ns, attrId, scope, buffer.ReadAlignedBytes(4)));
        }
        return new ReplayAttributes { Source = source, MapNamespace = mapNamespace, Attributes = attributes };
    }

    public byte[] Encode()
    {
        if (Source is null || MapNamespace is null)
        {
            if (Attributes.Count > 0)
                throw new InvalidOperationException("Attributes require Source and MapNamespace.");
            return [];
        }

        var writer = new BitPackedBufferWriter(bigEndian: false);
        writer.WriteBits((ulong)Source.Value, 8);
        writer.WriteBits(MapNamespace.Value, 32);
        writer.WriteBits((ulong)Attributes.Count, 32);
        foreach (var attribute in Attributes)
        {
            if (attribute.RawValue.Length != 4)
                throw new InvalidOperationException($"Attribute {attribute.AttrId} raw value must be 4 bytes.");
            writer.WriteBits(attribute.Namespace, 32);
            writer.WriteBits(attribute.AttrId, 32);
            writer.WriteBits(attribute.Scope, 8);
            writer.WriteAlignedBytes(attribute.RawValue);
        }
        return writer.ToArray();
    }
}
