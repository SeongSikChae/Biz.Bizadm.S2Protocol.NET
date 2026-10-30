using Biz.Bizadm.S2Protocol.NET.Common.Game;
using Biz.Bizadm.S2Protocol.NET.Common.Replay;
using Biz.Bizadm.S2Protocol.NET.Protocols;
using Biz.Bizadm.S2Protocol.NET.Versions.Protocol98310;
using Biz.Bizadm.S2Protocol.NET.Versions.Protocol98310.Game;
using Biz.Bizadm.S2Protocol.NET.Versions.Protocol98310.Replay;

namespace Biz.Bizadm.S2Protocol.NET.Tests.Generated;

// 단위 12: 버전 타입이 공통 인터페이스로 노출되는지(설계 D3: enum은 기반 정수형, 배열은 IReadOnlyList).
[TestClass]
public sealed class CommonInterfaceTests
{
    [TestMethod]
    public void Header_ExposesVersionBaseBuildAndEnumAsInt()
    {
        var protocol = S2ProtocolRegistry.Get(98310);
        byte[] bytes = protocol.EncodeHeader(new SHeader
        {
            Version = new SVersion { BaseBuild = 98310 },
            Type = EReplayType.Multiplayer,
        });

        IHeader header = protocol.DecodeHeader(bytes);

        Assert.AreEqual(98310L, header.Version.BaseBuild);
        Assert.AreEqual((int)EReplayType.Multiplayer, header.Type);
    }

    [TestMethod]
    public void Details_ExposesPlayerListThroughInterfaces()
    {
        var details = new SDetails
        {
            PlayerList = [new SPlayerDetails { Name = "Player"u8.ToArray(), TeamId = 1 }],
        };

        IDetails common = details;

        Assert.IsNotNull(common.PlayerList);
        Assert.HasCount(1, common.PlayerList);
        CollectionAssert.AreEqual("Player"u8.ToArray(), common.PlayerList[0].Name);
        Assert.AreEqual(1, common.PlayerList[0].TeamId);
    }

    [TestMethod]
    public void Encode_RejectsNull()
    {
        Assert.ThrowsExactly<ArgumentException>(() => S2ProtocolRegistry.Get(98310).EncodeDetails(null!));
    }
}
