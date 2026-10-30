using Biz.Bizadm.S2Protocol.NET.Decoding;
using Biz.Bizadm.S2Protocol.NET.Encoding;
using Biz.Bizadm.S2Protocol.NET.Events;
using Biz.Bizadm.S2Protocol.NET.Protocols;
using Biz.Bizadm.S2Protocol.NET.Versions.Protocol98310;
using Biz.Bizadm.S2Protocol.NET.Versions.Protocol98310.Game;
using Biz.Bizadm.S2Protocol.NET.Versions.Protocol98310.Replay;
using Biz.Bizadm.S2Protocol.NET.Versions.Protocol98310.Replay.Tracker;

namespace Biz.Bizadm.S2Protocol.NET.Tests.Generated;

// 단위 11: 생성 Protocol의 이벤트 스트림(Python _decode_event_stream 대응)과 루트 Decode/Encode.
[TestClass]
public sealed class Protocol98310EventStreamTests
{
    private readonly IS2Protocol _protocol = new Protocol();

    // protocol98310.py: svaruint32 choice (0,2){uint6,uint14,uint22,uint32}, userid (0,5), game eventid (0,7),
    // 5 = SUserFinishedLoadingSyncEvent(빈 struct), 9 = SBankFileEvent(m_name blob (0,7))
    private static byte[] HandEncodedGameEvents()
    {
        var e = new BitPackedEncoder();
        e.WriteInt(0, 0, 2); e.WriteInt(3, 0, 6);        // gameloop 차이 3 (uint6)
        e.WriteInt(2, 0, 5);                              // userid 2
        e.WriteInt(5, 0, 7);                              // eventid 5
        e.ByteAlign();
        e.WriteInt(1, 0, 2); e.WriteInt(100, 0, 14);     // gameloop 차이 100 (uint14)
        e.WriteInt(1, 0, 5);                              // userid 1
        e.WriteInt(9, 0, 7);                              // eventid 9
        e.WriteBlob("abc"u8.ToArray(), 0, 7);
        e.ByteAlign();
        return e.ToArray();
    }

    [TestMethod]
    public void DecodeGameEvents_HandEncodedStream()
    {
        var events = _protocol.DecodeGameEvents(HandEncodedGameEvents()).ToList();

        Assert.HasCount(2, events);
        Assert.AreEqual(5, events[0].EventId);
        Assert.AreEqual("NNet.Game.SUserFinishedLoadingSyncEvent", events[0].EventName);
        Assert.AreEqual(3L, events[0].GameLoop);
        Assert.AreEqual(2, events[0].UserId);
        Assert.AreEqual(24L, events[0].Bits);   // 2+6+5+7 = 20비트 → 바이트 정렬
        Assert.IsInstanceOfType<SUserFinishedLoadingSyncEvent>(events[0].Body);

        Assert.AreEqual(9, events[1].EventId);
        Assert.AreEqual(103L, events[1].GameLoop);   // 누적
        Assert.AreEqual(1, events[1].UserId);
        CollectionAssert.AreEqual("abc"u8.ToArray(), ((SBankFileEvent)events[1].Body).Name);
    }

    [TestMethod]
    public void EncodeGameEvents_ReproducesHandEncodedStream()
    {
        byte[] bytes = HandEncodedGameEvents();
        var events = _protocol.DecodeGameEvents(bytes).ToList();

        CollectionAssert.AreEqual(bytes, _protocol.EncodeGameEvents(events));
    }

    [TestMethod]
    public void MessageEvents_RoundTrip()
    {
        var events = new[]
        {
            new MessageEvent(0, "", 10, 3, 0, new SChatMessage { Recipient = 0, String = "gg"u8.ToArray() }),
            new MessageEvent(0, "", 10, 4, 0, new SChatMessage { Recipient = 0, String = "wp"u8.ToArray() }),
        };

        byte[] bytes = _protocol.EncodeMessageEvents(events);
        var decoded = _protocol.DecodeMessageEvents(bytes).ToList();

        Assert.HasCount(2, decoded);
        Assert.AreEqual("NNet.Game.SChatMessage", decoded[0].EventName);
        Assert.AreEqual(10L, decoded[1].GameLoop);   // 차이 0
        Assert.AreEqual(4, decoded[1].UserId);
        CollectionAssert.AreEqual("wp"u8.ToArray(), ((SChatMessage)decoded[1].Body).String);
        CollectionAssert.AreEqual(bytes, _protocol.EncodeMessageEvents(decoded));
    }

    [TestMethod]
    public void TrackerEvents_RoundTrip()
    {
        var events = new[]
        {
            new TrackerEvent(0, "", 1, 0, new SPlayerStatsEvent { PlayerId = 1 }),
            new TrackerEvent(1, "", 5000000, 0, new SUnitBornEvent { UnitTagIndex = 7, UnitTypeName = "Marine"u8.ToArray() }),
        };

        byte[] bytes = _protocol.EncodeTrackerEvents(events);
        var decoded = _protocol.DecodeTrackerEvents(bytes).ToList();

        Assert.HasCount(2, decoded);
        Assert.AreEqual("NNet.Replay.Tracker.SPlayerStatsEvent", decoded[0].EventName);
        Assert.AreEqual(5000000L, decoded[1].GameLoop);   // 차이가 uint22를 넘어 uint32 경우
        var born = (SUnitBornEvent)decoded[1].Body;
        Assert.AreEqual(7, born.UnitTagIndex);
        CollectionAssert.AreEqual("Marine"u8.ToArray(), born.UnitTypeName);
        CollectionAssert.AreEqual(bytes, _protocol.EncodeTrackerEvents(decoded));
    }

    [TestMethod]
    public void Header_DecodeEncode_ThroughInterface()
    {
        var header = new SHeader { Version = new SVersion { BaseBuild = 98310 }, ElapsedGameLoops = 42 };
        byte[] bytes = _protocol.EncodeHeader(header);

        var decoded = (SHeader)_protocol.DecodeHeader(bytes);

        Assert.AreEqual(98310L, decoded.Version.BaseBuild);
        Assert.AreEqual(42L, decoded.ElapsedGameLoops);
        Assert.AreEqual(98310, _protocol.BaseBuild);
    }

    [TestMethod]
    public void Encode_WrongType_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => _protocol.EncodeHeader(new SVersion()));
        Assert.ThrowsExactly<ArgumentException>(() => _protocol.EncodeGameEvents(
            [new GameEvent(9, "", 0, 0, 0, new SUserFinishedLoadingSyncEvent())]));
    }

    [TestMethod]
    public void DecodeGameEvents_UnknownEventId_Throws()
    {
        var e = new BitPackedEncoder();
        e.WriteInt(0, 0, 2); e.WriteInt(0, 0, 6);
        e.WriteInt(0, 0, 5);
        e.WriteInt(127, 0, 7);   // 98310 game 표에 없는 id

        Assert.ThrowsExactly<CorruptedReplayException>(() => _protocol.DecodeGameEvents(e.ToArray()).ToList());
    }
}
