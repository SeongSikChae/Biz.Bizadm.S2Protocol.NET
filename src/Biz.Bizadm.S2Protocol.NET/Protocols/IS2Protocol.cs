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
    int BaseBuild { get; }

    IHeader DecodeHeader(ReadOnlyMemory<byte> data);

    IDetails DecodeDetails(ReadOnlyMemory<byte> data);

    IInitData DecodeInitData(ReadOnlyMemory<byte> data);

    IEnumerable<GameEvent> DecodeGameEvents(ReadOnlyMemory<byte> data);

    IEnumerable<MessageEvent> DecodeMessageEvents(ReadOnlyMemory<byte> data);

    IEnumerable<TrackerEvent> DecodeTrackerEvents(ReadOnlyMemory<byte> data);

    // 값이 이 빌드의 생성 타입이 아니면 ArgumentException.
    byte[] EncodeHeader(IHeader value);

    byte[] EncodeDetails(IDetails value);

    byte[] EncodeInitData(IInitData value);

    byte[] EncodeGameEvents(IEnumerable<GameEvent> events);

    byte[] EncodeMessageEvents(IEnumerable<MessageEvent> events);

    byte[] EncodeTrackerEvents(IEnumerable<TrackerEvent> events);
}
