using Biz.Bizadm.S2Protocol.NET.Attributes;

namespace Biz.Bizadm.S2Protocol.NET.Tests.Attributes;

// 단위 14: s2protocol decode_replay_attributes_events(리틀엔디언) 포팅.
[TestClass]
public sealed class ReplayAttributesTests
{
    // source=2, mapNamespace=999, count=4, 그 뒤 (namespace, attrid, scope, 4바이트 값) 4건
    private static readonly byte[] Sample =
    [
        0x02,
        0xE7, 0x03, 0x00, 0x00,
        0x04, 0x00, 0x00, 0x00,
        0xE7, 0x03, 0x00, 0x00, 0xB9, 0x0B, 0x00, 0x00, 0x01, (byte)'r', (byte)'r', (byte)'e', (byte)'T',   // scope 1, Race "Terr"
        0xE7, 0x03, 0x00, 0x00, 0xD1, 0x07, 0x00, 0x00, 0x10, 0x00, (byte)'1', (byte)'v', (byte)'1',         // scope 16, PartiesPremade "1v1"
        0xE7, 0x03, 0x00, 0x00, 0xB9, 0x0B, 0x00, 0x00, 0x02, (byte)'g', (byte)'r', (byte)'e', (byte)'Z',   // scope 2, Race "Zerg"
        0xE7, 0x03, 0x00, 0x00, 0xB9, 0x0B, 0x00, 0x00, 0x01, (byte)'t', (byte)'o', (byte)'r', (byte)'P',   // scope 1, Race "Prot"
    ];

    [TestMethod]
    public void Decode_ReadsHeaderAndAttributesInFileOrder()
    {
        var attributes = ReplayAttributes.Decode(Sample);

        Assert.AreEqual(2, attributes.Source);
        Assert.AreEqual(999u, attributes.MapNamespace);
        Assert.HasCount(4, attributes.Attributes);
        Assert.AreEqual(AttributeIds.PartiesPremade, attributes.Attributes[1].AttrId);
        Assert.AreEqual((byte)16, attributes.Attributes[1].Scope);
        CollectionAssert.AreEqual("1v1"u8.ToArray(), attributes.Attributes[1].Value);
    }

    [TestMethod]
    public void Scopes_GroupLikePython()
    {
        var scopes = ReplayAttributes.Decode(Sample).Scopes;

        Assert.HasCount(3, scopes);
        var scope1Race = scopes[1][AttributeIds.Race];
        Assert.HasCount(2, scope1Race);
        CollectionAssert.AreEqual("Terr"u8.ToArray(), scope1Race[0].Value);
        CollectionAssert.AreEqual("Prot"u8.ToArray(), scope1Race[1].Value);
        CollectionAssert.AreEqual("Zerg"u8.ToArray(), scopes[2][AttributeIds.Race][0].Value);
    }

    [TestMethod]
    public void Value_StripsZeroBytesOnBothEnds()
    {
        var attribute = new ReplayAttribute(0, 0, 0, [0x00, (byte)'A', 0x00, 0x00]);   // 역순: 00 00 'A' 00

        CollectionAssert.AreEqual("A"u8.ToArray(), attribute.Value);
    }

    [TestMethod]
    public void Encode_ReproducesInput()
    {
        CollectionAssert.AreEqual(Sample, ReplayAttributes.Decode(Sample).Encode());
    }

    [TestMethod]
    public void Empty_HasNoHeaderAndEncodesToEmpty()
    {
        var attributes = ReplayAttributes.Decode(ReadOnlyMemory<byte>.Empty);

        Assert.IsNull(attributes.Source);
        Assert.IsNull(attributes.MapNamespace);
        Assert.IsEmpty(attributes.Attributes);
        Assert.IsEmpty(attributes.Encode());
    }
}
