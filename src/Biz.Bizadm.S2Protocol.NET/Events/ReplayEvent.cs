namespace Biz.Bizadm.S2Protocol.NET.Events;

// s2protocol _decode_event_stream이 이벤트 dict에 붙이는 _eventid, _event, _gameloop, _bits와 본문.
// Body는 해당 빌드의 생성 타입(Versions.ProtocolNNNNN...)이다. Bits는 인코딩 시 쓰지 않는다.
/// <summary>An event of a replay event stream (s2protocol <c>_eventid</c>, <c>_event</c>, <c>_gameloop</c>, <c>_bits</c>).</summary>
/// <param name="EventId">The event id.</param>
/// <param name="EventName">The s2protocol type name, for example <c>NNet.Game.SCmdEvent</c>.</param>
/// <param name="GameLoop">The game loop of the event.</param>
/// <param name="Bits">Number of bits the event took in the stream (ignored when encoding).</param>
/// <param name="Body">The event, an instance of the build's generated type (<c>Versions.ProtocolNNNNN...</c>).</param>
public abstract record ReplayEvent(int EventId, string EventName, long GameLoop, long Bits, object Body);

// game/message 이벤트는 _userid(SGameUserId.m_userId)를 갖는다.
/// <summary>An event of <c>replay.game.events</c>.</summary>
/// <param name="EventId">The event id.</param>
/// <param name="EventName">The s2protocol type name.</param>
/// <param name="GameLoop">The game loop of the event.</param>
/// <param name="UserId">The user id (s2protocol <c>_userid.m_userId</c>).</param>
/// <param name="Bits">Number of bits the event took in the stream.</param>
/// <param name="Body">The event, an instance of the build's generated type.</param>
public sealed record GameEvent(int EventId, string EventName, long GameLoop, int UserId, long Bits, object Body)
    : ReplayEvent(EventId, EventName, GameLoop, Bits, Body);

/// <summary>An event of <c>replay.message.events</c>.</summary>
/// <param name="EventId">The event id.</param>
/// <param name="EventName">The s2protocol type name.</param>
/// <param name="GameLoop">The game loop of the event.</param>
/// <param name="UserId">The user id (s2protocol <c>_userid.m_userId</c>).</param>
/// <param name="Bits">Number of bits the event took in the stream.</param>
/// <param name="Body">The event, an instance of the build's generated type.</param>
public sealed record MessageEvent(int EventId, string EventName, long GameLoop, int UserId, long Bits, object Body)
    : ReplayEvent(EventId, EventName, GameLoop, Bits, Body);

/// <summary>An event of <c>replay.tracker.events</c>.</summary>
/// <param name="EventId">The event id.</param>
/// <param name="EventName">The s2protocol type name.</param>
/// <param name="GameLoop">The game loop of the event.</param>
/// <param name="Bits">Number of bits the event took in the stream.</param>
/// <param name="Body">The event, an instance of the build's generated type.</param>
public sealed record TrackerEvent(int EventId, string EventName, long GameLoop, long Bits, object Body)
    : ReplayEvent(EventId, EventName, GameLoop, Bits, Body);
