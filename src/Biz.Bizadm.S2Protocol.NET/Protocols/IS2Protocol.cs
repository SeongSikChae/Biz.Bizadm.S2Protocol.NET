using Biz.Bizadm.S2Protocol.NET.Common.Game;
using Biz.Bizadm.S2Protocol.NET.Common.Replay;
using Biz.Bizadm.S2Protocol.NET.Events;

namespace Biz.Bizadm.S2Protocol.NET.Protocols;

// 한 빌드의 디코딩·인코딩. 구현은 생성 코드(Versions.ProtocolNNNNN.Protocol)다.
// header/details/initdata는 버전 공통 인터페이스로 돌려주며, 실제 객체는 해당 빌드의 생성 타입이다.
// 이벤트 스트림은 지연 열거하며, Python s2protocol도 디코딩하지 못하는 구버전 스트림은 NotSupportedException.

/// <summary>Decodes and encodes the replay parts of one StarCraft II base build.</summary>
/// <remarks>
/// Decoding then encoding reproduces the original bytes only when this protocol's base build is the replay's
/// own build. A protocol chosen by <see cref="ProtocolFallback"/> skips versioned fields it does not know, so
/// those fields are lost on encoding. Fields absent from the decoded data stay absent when re-encoded, even if a
/// value is assigned to them after decoding.
/// </remarks>
public interface IS2Protocol
{
    /// <summary>The s2protocol base build of this protocol.</summary>
    int BaseBuild { get; }

    /// <summary>Decodes a replay header (the MPQ user data contents).</summary>
    /// <param name="data">The encoded header.</param>
    /// <returns>The header; the object is this build's generated type.</returns>
    IHeader DecodeHeader(ReadOnlyMemory<byte> data);

    /// <summary>Decodes <c>replay.details</c>.</summary>
    /// <param name="data">The file contents.</param>
    /// <returns>The details; the object is this build's generated type.</returns>
    IDetails DecodeDetails(ReadOnlyMemory<byte> data);

    /// <summary>Decodes <c>replay.initData</c>.</summary>
    /// <param name="data">The file contents.</param>
    /// <returns>The init data; the object is this build's generated type.</returns>
    IInitData DecodeInitData(ReadOnlyMemory<byte> data);

    /// <summary>Decodes <c>replay.game.events</c> lazily.</summary>
    /// <param name="data">The file contents.</param>
    /// <returns>The events.</returns>
    /// <exception cref="NotSupportedException">s2protocol cannot decode game events of this build.</exception>
    IEnumerable<GameEvent> DecodeGameEvents(ReadOnlyMemory<byte> data);

    /// <summary>Decodes <c>replay.message.events</c> lazily.</summary>
    /// <param name="data">The file contents.</param>
    /// <returns>The events.</returns>
    /// <exception cref="NotSupportedException">s2protocol cannot decode message events of this build.</exception>
    IEnumerable<MessageEvent> DecodeMessageEvents(ReadOnlyMemory<byte> data);

    /// <summary>Decodes <c>replay.tracker.events</c> lazily.</summary>
    /// <param name="data">The file contents.</param>
    /// <returns>The events.</returns>
    /// <exception cref="NotSupportedException">This build has no tracker events.</exception>
    IEnumerable<TrackerEvent> DecodeTrackerEvents(ReadOnlyMemory<byte> data);

    // 값이 이 빌드의 생성 타입이 아니면 ArgumentException.
    /// <summary>Encodes a replay header.</summary>
    /// <param name="value">A header of this build's generated type.</param>
    /// <returns>The encoded header.</returns>
    /// <exception cref="ArgumentException">The value is not this build's generated type.</exception>
    byte[] EncodeHeader(IHeader value);

    /// <summary>Encodes <c>replay.details</c>.</summary>
    /// <param name="value">Details of this build's generated type.</param>
    /// <returns>The file contents.</returns>
    /// <exception cref="ArgumentException">The value is not this build's generated type.</exception>
    byte[] EncodeDetails(IDetails value);

    /// <summary>Encodes <c>replay.initData</c>.</summary>
    /// <param name="value">Init data of this build's generated type.</param>
    /// <returns>The file contents.</returns>
    /// <exception cref="ArgumentException">The value is not this build's generated type.</exception>
    byte[] EncodeInitData(IInitData value);

    /// <summary>Encodes <c>replay.game.events</c>.</summary>
    /// <param name="events">Events whose bodies are this build's generated types.</param>
    /// <returns>The file contents.</returns>
    /// <exception cref="ArgumentException">An event body does not match its event id in this build.</exception>
    byte[] EncodeGameEvents(IEnumerable<GameEvent> events);

    /// <summary>Encodes <c>replay.message.events</c>.</summary>
    /// <param name="events">Events whose bodies are this build's generated types.</param>
    /// <returns>The file contents.</returns>
    /// <exception cref="ArgumentException">An event body does not match its event id in this build.</exception>
    byte[] EncodeMessageEvents(IEnumerable<MessageEvent> events);

    /// <summary>Encodes <c>replay.tracker.events</c>.</summary>
    /// <param name="events">Events whose bodies are this build's generated types.</param>
    /// <returns>The file contents.</returns>
    /// <exception cref="ArgumentException">An event body does not match its event id in this build.</exception>
    byte[] EncodeTrackerEvents(IEnumerable<TrackerEvent> events);
}
