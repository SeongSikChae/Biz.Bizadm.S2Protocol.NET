using System.Buffers.Binary;
using Biz.Bizadm.MPQ.NET;
using Biz.Bizadm.S2Protocol.NET.Attributes;
using Biz.Bizadm.S2Protocol.NET.Decoding;
using Biz.Bizadm.S2Protocol.NET.Encoding;
using Biz.Bizadm.S2Protocol.NET.Protocols;
using Biz.Bizadm.S2Protocol.NET.Versions.Protocol98310;
using Biz.Bizadm.S2Protocol.NET.Versions.Protocol98310.Game;
using Biz.Bizadm.S2Protocol.NET.Versions.Protocol98310.Replay;

namespace Biz.Bizadm.S2Protocol.NET.Tests;

// 단위 15: S2Replay. 합성 리플레이(MPQ.NET으로 만든 아카이브 앞에 user data 블록)와 사용자 제공 샘플.
[TestClass]
public sealed class S2ReplayTests
{
    private const int HeaderOffset = 0x400;

    [TestMethod]
    public void Open_SyntheticReplay_ReadsEveryPart()
    {
        using var stream = SyntheticReplay(baseBuild: 98310, includeTracker: false);
        using var replay = S2Replay.Open(stream);

        Assert.AreEqual(98310L, replay.Header.Version.BaseBuild);
        Assert.AreEqual(98310, replay.Protocol.BaseBuild);

        var details = replay.ReadDetails();
        Assert.HasCount(2, details.PlayerList!);
        CollectionAssert.AreEqual("Alice"u8.ToArray(), details.PlayerList![0].Name);

        var events = replay.ReadGameEvents().ToList();
        Assert.HasCount(1, events);
        Assert.AreEqual(7L, events[0].GameLoop);

        Assert.AreEqual(1, replay.ReadAttributes().Attributes.Count);
        Assert.IsEmpty(replay.ReadTrackerEvents().ToList());   // 파일 없음 → 빈 결과
        Assert.IsEmpty(replay.ReadMessageEvents().ToList());
    }

    [TestMethod]
    public void Open_UnknownBuild_UsesFallbackOption()
    {
        using (var stream = SyntheticReplay(baseBuild: 98311, includeTracker: false))
            Assert.ThrowsExactly<UnsupportedProtocolException>(() => S2Replay.Open(stream));

        using var fallback = SyntheticReplay(baseBuild: 98311, includeTracker: false);
        using var replay = S2Replay.Open(fallback, options: new ProtocolResolveOptions { Fallback = ProtocolFallback.NearestLower });
        Assert.AreEqual(98311L, replay.Header.Version.BaseBuild);
        Assert.AreEqual(98310, replay.Protocol.BaseBuild);
    }

    [TestMethod]
    public void Open_ArchiveWithoutUserData_Throws()
    {
        var archive = new MemoryStream();
        using (var mpq = MpqArchive.Create(archive, leaveOpen: true))
        {
            mpq.AddFile("replay.details", new MemoryStream([0x05, 0x00]));
            mpq.Flush();
        }
        archive.Position = 0;

        Assert.ThrowsExactly<CorruptedReplayException>(() => S2Replay.Open(archive));
    }

    // 사용자가 tests/.../TestData/Replays에 넣은 실제 리플레이 전체를 끝까지 디코딩한다.
    [TestMethod]
    public void Open_SampleReplays_DecodeCompletely()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "TestData", "Replays");
        var files = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.SC2Replay") : [];
        if (files.Length == 0)
            Assert.Inconclusive($"No sample replays in {directory}.");

        foreach (string file in files)
        {
            using var replay = S2Replay.Open(file, new ProtocolResolveOptions { Fallback = ProtocolFallback.None });
            string name = Path.GetFileName(file);
            Assert.AreEqual(replay.Header.Version.BaseBuild, replay.Protocol.BaseBuild, name);
            Assert.IsNotEmpty(replay.ReadDetails().PlayerList ?? [], $"{name}: players");
            replay.ReadInitData();
            replay.ReadAttributes();
            if (replay.Protocol.BaseBuild > 23260)   // 그 이하는 Python도 game/message 이벤트를 디코딩하지 못한다
            {
                Assert.IsNotEmpty(replay.ReadGameEvents().ToList(), $"{name}: game events");
                replay.ReadMessageEvents().ToList();
            }
            if (replay.Protocol.BaseBuild > 24944)
                replay.ReadTrackerEvents().ToList();
        }
    }

    // MPQ 아카이브 앞에 "MPQ\x1B" user data 블록(16바이트 + 헤더 내용)을 붙여 0x400에 아카이브를 둔다.
    private static MemoryStream SyntheticReplay(int baseBuild, bool includeTracker)
    {
        IS2Protocol protocol = new Protocol();
        byte[] header = protocol.EncodeHeader(new SHeader
        {
            Signature = "StarCraft II replay\u001b11"u8.ToArray(),
            Version = new SVersion { BaseBuild = baseBuild, Build = baseBuild },
        });
        byte[] details = protocol.EncodeDetails(new SDetails
        {
            PlayerList = [new SPlayerDetails { Name = "Alice"u8.ToArray() }, new SPlayerDetails { Name = "Bob"u8.ToArray() }],
        });
        byte[] gameEvents = protocol.EncodeGameEvents(
            [new Events.GameEvent(5, "", 7, 0, 0, new SUserFinishedLoadingSyncEvent())]);
        byte[] attributes = new ReplayAttributes
        {
            Source = 0,
            MapNamespace = 999,
            Attributes = [new ReplayAttribute(999, AttributeIds.Race, 1, "rreT"u8.ToArray())],
        }.Encode();

        var archive = new MemoryStream();
        using (var mpq = MpqArchive.Create(archive, leaveOpen: true))
        {
            mpq.AddFile("replay.details", new MemoryStream(details));
            mpq.AddFile("replay.game.events", new MemoryStream(gameEvents));
            mpq.AddFile("replay.attributes.events", new MemoryStream(attributes));
            if (includeTracker)
                mpq.AddFile("replay.tracker.events", new MemoryStream(protocol.EncodeTrackerEvents([])));
            mpq.Flush();
        }

        var replay = new MemoryStream();
        Span<byte> block = stackalloc byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(block, 0x1B51504D);                 // "MPQ\x1B"
        BinaryPrimitives.WriteUInt32LittleEndian(block[4..], 0x200);                  // user data 최대 크기
        BinaryPrimitives.WriteUInt32LittleEndian(block[8..], HeaderOffset);           // MPQ 헤더 위치
        BinaryPrimitives.WriteUInt32LittleEndian(block[12..], (uint)header.Length);   // 내용 크기
        replay.Write(block);
        replay.Write(header);
        replay.Write(new byte[HeaderOffset - 16 - header.Length]);
        replay.Write(archive.ToArray());
        replay.Position = 0;
        return replay;
    }
}
