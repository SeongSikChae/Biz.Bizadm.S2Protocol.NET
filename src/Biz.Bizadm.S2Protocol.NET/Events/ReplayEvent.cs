namespace Biz.Bizadm.S2Protocol.NET.Events;

// s2protocol _decode_event_stream이 이벤트 dict에 붙이는 _eventid, _event, _gameloop, _bits와 본문.
// Body는 해당 빌드의 생성 타입(Versions.ProtocolNNNNN...)이다. Bits는 인코딩 시 쓰지 않는다.
public abstract record ReplayEvent(int EventId, string EventName, long GameLoop, long Bits, object Body);

// game/message 이벤트는 _userid(SGameUserId.m_userId)를 갖는다.
public sealed record GameEvent(int EventId, string EventName, long GameLoop, int UserId, long Bits, object Body)
    : ReplayEvent(EventId, EventName, GameLoop, Bits, Body);

public sealed record MessageEvent(int EventId, string EventName, long GameLoop, int UserId, long Bits, object Body)
    : ReplayEvent(EventId, EventName, GameLoop, Bits, Body);

public sealed record TrackerEvent(int EventId, string EventName, long GameLoop, long Bits, object Body)
    : ReplayEvent(EventId, EventName, GameLoop, Bits, Body);
