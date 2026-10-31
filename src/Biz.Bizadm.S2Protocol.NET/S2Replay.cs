using Biz.Bizadm.MPQ.NET;
using Biz.Bizadm.S2Protocol.NET.Attributes;
using Biz.Bizadm.S2Protocol.NET.Common.Game;
using Biz.Bizadm.S2Protocol.NET.Common.Replay;
using Biz.Bizadm.S2Protocol.NET.Decoding;
using Biz.Bizadm.S2Protocol.NET.Events;
using Biz.Bizadm.S2Protocol.NET.Protocols;

namespace Biz.Bizadm.S2Protocol.NET;

// .SC2Replay(MPQ) 진입점. s2_cli와 같이 user data의 헤더를 최신 프로토콜로 디코딩하고,
// 헤더의 base build로 고른 프로토콜로 나머지 파일을 디코딩한다.
public sealed class S2Replay : IDisposable
{
    private readonly MpqArchive _archive;

    private S2Replay(MpqArchive archive, ProtocolResolveOptions? options)
    {
        _archive = archive;
        var userData = archive.UserData ?? throw new CorruptedReplayException("The archive has no replay header (MPQ user data).");
        Header = S2ProtocolRegistry.Latest.DecodeHeader(userData.Content);
        Protocol = S2ProtocolRegistry.Get(checked((int)Header.Version.BaseBuild), options);
    }

    public IHeader Header { get; }

    public IS2Protocol Protocol { get; }

    public static S2Replay Open(string path, ProtocolResolveOptions? options = null) =>
        Create(MpqArchive.Open(path), options);

    public static S2Replay Open(Stream stream, bool leaveOpen = false, ProtocolResolveOptions? options = null) =>
        Create(MpqArchive.Open(stream, leaveOpen: leaveOpen), options);

    public IDetails ReadDetails() => Protocol.DecodeDetails(ReadRequired("replay.details"));

    public IInitData ReadInitData() => Protocol.DecodeInitData(ReadRequired("replay.initData"));

    // 이벤트·attributes 파일이 없으면(예: tracker가 없던 구버전 리플레이) 빈 결과.
    public IEnumerable<GameEvent> ReadGameEvents() =>
        ReadOptional("replay.game.events") is { } data ? Protocol.DecodeGameEvents(data) : [];

    public IEnumerable<MessageEvent> ReadMessageEvents() =>
        ReadOptional("replay.message.events") is { } data ? Protocol.DecodeMessageEvents(data) : [];

    public IEnumerable<TrackerEvent> ReadTrackerEvents() =>
        ReadOptional("replay.tracker.events") is { } data ? Protocol.DecodeTrackerEvents(data) : [];

    public ReplayAttributes ReadAttributes() =>
        ReadOptional("replay.attributes.events") is { } data ? ReplayAttributes.Decode(data) : new ReplayAttributes();

    public void Dispose() => _archive.Dispose();

    private static S2Replay Create(MpqArchive archive, ProtocolResolveOptions? options)
    {
        try
        {
            return new S2Replay(archive, options);
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    private byte[] ReadRequired(string name) =>
        ReadOptional(name) ?? throw new FileNotFoundException($"The replay has no '{name}'.", name);

    private byte[]? ReadOptional(string name)
    {
        if (!_archive.Contains(name))
            return null;
        using var stream = _archive.OpenFile(name);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
