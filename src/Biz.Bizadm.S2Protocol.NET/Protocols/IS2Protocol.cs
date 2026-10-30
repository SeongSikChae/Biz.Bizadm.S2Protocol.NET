using Biz.Bizadm.S2Protocol.NET.Events;

namespace Biz.Bizadm.S2Protocol.NET.Protocols;

// 한 빌드의 디코딩·인코딩. 구현은 생성 코드(Versions.ProtocolNNNNN.Protocol)다.
// header/details/initdata는 해당 빌드의 생성 타입을 object로 다룬다(공통 인터페이스는 단위 12에서 추가).
// 이벤트 스트림은 지연 열거하며, Python s2protocol도 디코딩하지 못하는 구버전 스트림은 NotSupportedException.
public interface IS2Protocol
{
    int BaseBuild { get; }

    object DecodeHeader(ReadOnlyMemory<byte> data);

    object DecodeDetails(ReadOnlyMemory<byte> data);

    object DecodeInitData(ReadOnlyMemory<byte> data);

    IEnumerable<GameEvent> DecodeGameEvents(ReadOnlyMemory<byte> data);

    IEnumerable<MessageEvent> DecodeMessageEvents(ReadOnlyMemory<byte> data);

    IEnumerable<TrackerEvent> DecodeTrackerEvents(ReadOnlyMemory<byte> data);

    // 값이 이 빌드의 생성 타입이 아니면 ArgumentException.
    byte[] EncodeHeader(object value);

    byte[] EncodeDetails(object value);

    byte[] EncodeInitData(object value);

    byte[] EncodeGameEvents(IEnumerable<GameEvent> events);

    byte[] EncodeMessageEvents(IEnumerable<MessageEvent> events);

    byte[] EncodeTrackerEvents(IEnumerable<TrackerEvent> events);
}
