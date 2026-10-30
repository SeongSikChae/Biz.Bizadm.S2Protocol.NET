using Biz.Bizadm.S2Protocol.NET.Protocols;

namespace Biz.Bizadm.S2Protocol.NET.Tests.Protocols;

// 단위 12 시점에는 98310만 출력되어 있다(단위 13에서 전 버전).
[TestClass]
public sealed class S2ProtocolRegistryTests
{
    [TestMethod]
    public void Get_ExactBuild()
    {
        var protocol = S2ProtocolRegistry.Get(98310);

        Assert.AreEqual(98310, protocol.BaseBuild);
        Assert.AreSame(protocol, S2ProtocolRegistry.Get(98310));   // 캐시
        CollectionAssert.Contains(S2ProtocolRegistry.Builds.ToList(), 98310);
    }

    [TestMethod]
    public void Get_UnknownBuild_ThrowsByDefault()
    {
        var e = Assert.ThrowsExactly<UnsupportedProtocolException>(() => S2ProtocolRegistry.Get(98311));

        Assert.AreEqual(98311, e.BaseBuild);
    }

    [TestMethod]
    public void Get_NearestLower()
    {
        var options = new ProtocolResolveOptions { Fallback = ProtocolFallback.NearestLower };

        Assert.AreEqual(98310, S2ProtocolRegistry.Get(98311, options).BaseBuild);
        Assert.ThrowsExactly<UnsupportedProtocolException>(() => S2ProtocolRegistry.Get(1, options));   // 더 낮은 빌드 없음
    }

    [TestMethod]
    public void Get_Latest()
    {
        var options = new ProtocolResolveOptions { Fallback = ProtocolFallback.Latest };

        Assert.AreSame(S2ProtocolRegistry.Latest, S2ProtocolRegistry.Get(1, options));
        Assert.AreEqual(S2ProtocolRegistry.Builds[^1], S2ProtocolRegistry.Latest.BaseBuild);
    }
}
