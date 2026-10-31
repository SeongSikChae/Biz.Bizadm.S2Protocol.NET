---
status: done
category: s2protocol
feature: S2Protocol라이브러리
phase: 설계
---

# S2Protocol 라이브러리 설계

선행: `0001-S2Protocol라이브러리-방안.md`

## 결정 목록

| # | 항목 | 결정 |
|---|---|---|
| D1 | 생성기 입력 | `json/protocolNNNNN.json`이 주 입력(타입명·필드·제약식·enum). `versions/protocolNNNNN.py`는 대조(oracle)와 이벤트 ID 표 추출에 쓴다 |
| D2 | 대조 실패 | json에서 유도한 와이어 레이아웃이 py `typeinfos`와 구조적으로 다르면 생성기가 해당 버전 생성을 실패시킨다 |
| D3 | 공통 인터페이스 | 같은 fullname의 struct가 정의된 모든 버전에서 **이름과 C# 타입이 같은 필드의 교집합**만 노출한다. enum 필드는 기반 정수형(int/long)으로 비교·노출하고, choice 필드는 제외한다. 나머지는 버전별 타입으로 캐스팅해 접근한다 |
| D4 | 원본 보관 | 다운로드 스크립트. 저장소에는 고정 커밋 해시만 기록하고, 원본은 gitignore 폴더로 내려받는다 |
| D5 | 패키징 | 단일 어셈블리. 루트에서 도달 가능한 타입만 생성해 크기를 제한한다. 크기 문제가 확인되면 재검토한다 |
| D6 | 빌드 선택 | 정확 일치가 기본이며, 없으면 `UnsupportedProtocolException`. 옵션으로 가장 가까운 하위 빌드 또는 최신 빌드로 폴백 |
| D7 | 이벤트 API | 지연 `IEnumerable<T>`. 각 이벤트는 메타(eventid, gameloop, userid, bits)와 강타입 본문을 갖는다 |
| D8 | 검증 | 포터블 Python으로 s2protocol을 실행해 만든 JSON 골든과 .NET 결과를 비교. 인코더는 디코드→인코드 바이트 일치로 검증 |
| D9 | 샘플 | 사용자가 `.SC2Replay`를 제공한다. 저장소 커밋 여부는 개발 단계에서 정한다(2026-10-31: 빌드 97563 샘플 4개 커밋) |
| D10 | versioned 필드 존재 | versioned로 디코딩한 struct는 실제로 있던 필드 태그를 기록한다. 골든 직렬화와 versioned 인코딩은 기록된 필드만 다룬다. 기록이 없는(직접 만든) 객체는 모든 필드가 있는 것으로 본다. Python이 빠진 필드를 dict에 넣지 않는 동작과 바이트 왕복을 함께 맞추기 위함(2026-10-31 사용자 결정). 제약: 기록은 internal이라, 디코딩한 객체에서 원래 없던 필드에 값을 넣고 인코딩하면 그 필드는 빠진다 |

## 구조

```
Biz.Bizadm.S2Protocol.NET.slnx
Directory.Build.props / Directory.Packages.props   (net10.0, Nullable, ImplicitUsings, CPM)
src/Biz.Bizadm.S2Protocol.NET/
  Decoding/      BitPackedBuffer, BitPackedDecoder, VersionedDecoder, CorruptedReplayException
  Encoding/      BitPackedBufferWriter, BitPackedEncoder, VersionedEncoder
  Types/         FourCC, S2BitArray
  Events/        ReplayEvent<TBody>, GameEvent, MessageEvent, TrackerEvent
  Attributes/    ReplayAttributes (attributes.py 수작업 포팅, 생성 대상 아님)
  Protocols/     IS2Protocol, S2ProtocolRegistry, ProtocolResolveOptions
  S2Replay.cs    MPQ 진입점
  UnitTag.cs     unit_tag / unit_tag_index / unit_tag_recycle
  Generated/
    Common/      버전 공통 인터페이스 (*.g.cs)
    Versions/Protocol{NNNNN}/  버전별 타입·코덱·IS2Protocol 구현 (*.g.cs)
tests/Biz.Bizadm.S2Protocol.NET.Tests/   (MSTest)
tools/Biz.Bizadm.S2Protocol.NET.Generator/   (console, 패키지·솔루션 빌드 산출물 아님)
  upstream.json  고정 커밋 해시
  .upstream/     내려받은 json·py (gitignore)
```

참조: 라이브러리 → `Biz.Bizadm.MPQ.NET`(NuGet). 테스트 → 라이브러리. 생성기는 라이브러리를 참조하지 않는다.

## 와이어 계층 (s2protocol `decoders.py` 대응)

| 구성 | 동작 |
|---|---|
| `BitPackedBuffer` | 빅엔디언, 바이트 내 LSB부터 소비. `ReadBits(int)`, `ReadAlignedBytes(int)`, `ReadUnalignedBytes(int)`, `ByteAlign()`, `Done`, `UsedBits`. 입력은 `ReadOnlyMemory<byte>`(지연 열거 때문에 span 불가) |
| `BitPackedDecoder` | `ReadInt(offset, bits)`, `ReadBool`, `ReadBlob(bounds)`, `ReadBitArray(bounds)`, `ReadArrayLength(bounds)`, `ReadChoiceTag(bounds)`, `ReadOptionalExists`, `ReadFourCC`, `ReadReal32/64`. 미지원 choice 태그는 예외 |
| `VersionedDecoder` | skip 바이트(0 array, 1 bitblob, 2 blob, 3 choice, 4 optional, 5 struct, 6 u8, 7 u32, 8 u64, 9 vint) 검사 + `vint`. struct는 태그 매칭, 모르는 태그는 `SkipInstance`. 모르는 choice 태그는 skip 후 빈 값 |
| 인코더 | 위 각 연산의 정확한 역. 생성 코드가 같은 순서로 호출한다. `encoders.py`는 참고만 한다(원본 버그: bitpacked `_blob`이 길이 대신 bytes를 `_int`에 전달, bitpacked `_real32`·versioned `_null` 시그니처 오류, bitpacked `_fourcc`를 정렬 바이트로 기록). 정확성은 디코드→인코드 왕복으로 판정한다 |

Python과 달리 typeinfos 표를 런타임에 해석하지 않는다. 생성 코드가 타입마다 `Read`/`Write` 정적 메서드를 갖고 위 기본 연산을 직접 호출한다.

## 타입 매핑 (json → C#)

| json `type_info` | C# | 비고 |
|---|---|---|
| `IntType` + 제약 | `int`(범위가 int 안) / `long`(long 안) / `ulong`(min ≥ 0, max > long.MaxValue. 98310의 `(0,64)` 등) | bitpacked: `offset=min`, `bits`=`max-min` 비트 길이. `(-2^63, 64)`는 unchecked 덧셈으로 long. 제약식(`IntLiteral`, `IdentifierExpr`→`ConstDecl`, `PowExpr`, `NegateExpr`)은 생성기가 평가 |
| `BoolType` | `bool` | |
| `BlobType`, `StringType`, `AsciiStringType` | `byte[]` | Python과 같이 원시 바이트 유지(왕복 보존) |
| `FourCCType` | `FourCC` (readonly struct, uint) | |
| `BitArrayType` | `S2BitArray` (Length, `byte[]`) | Python은 bitpacked에서 `(length, 정수)`, versioned에서 `(length, bytes)`를 돌려준다. C#은 `byte[]`로 통일하고, bitpacked 정수 ↔ 바이트 변환 순서는 개발 단위 3에서 정한다 |
| `ArrayType` | `T[]` | |
| `OptionalType` | `T?` | |
| `StructType` | `sealed class`(필드=속성). `parents`는 버전별 기반 클래스로 생성 | Python `__parent` 병합과 같은 결과. 98310 py에는 `__parent`가 없으므로 json `parents`의 실제 와이어 표현은 py 대조(D2)로 확정한다. 부모가 struct가 아니고 필드가 부모뿐이면(Python에서 결과 전체가 부모 값) 부모 타입의 별칭으로 처리한다 |
| `ChoiceType` | 추상 기반 + 경우별 중첩 `sealed class`(`Tag` 보유). 값 없는 경우(`NullType`)는 빈 클래스 | |
| `EnumType` | `enum`(기반 타입은 범위로 결정) | 정의 밖 값도 그대로 보존 |
| `UserType` | 참조 대상 decl로 해석. 별칭(typedef)은 대상 타입으로 펼침 | |
| real32/64 | `float` / `double` | json 표기는 생성기 구현 시 확인 |

이름: C# 네임스페이스 `Biz.Bizadm.S2Protocol.NET.Versions.Protocol{NNNNN}.{모듈 경로}`, 클래스명은 json `name` 그대로(`SGameSCmdEvent` 등). 필드는 `m_` 접두를 떼고 PascalCase로 바꾸며 원본 이름은 `[S2Field("m_...")]` 특성으로 남긴다. enum 멤버는 `e_` 접두를 떼고 PascalCase로 바꾼다.

공통 인터페이스 이름: 네임스페이스 `Biz.Bizadm.S2Protocol.NET.Common.{모듈 경로}`, 타입명은 struct의 `S` 접두를 떼고 `I`를 붙인다(`NNet.Replay.SHeader` → `Common.Replay.IHeader`).

생성기 실패·생략 규칙:

- 한 타입 안에서 변환 후 속성·경우 이름이 겹치거나 둘러싼 타입 이름과 같으면 생성 실패(이름과 경로 출력).
- 같은 fullname이 버전마다 종류(struct/choice/enum 등)가 다르면 해당 공통 인터페이스는 만들지 않고 경고를 출력한다.

## 루트·이벤트 표

| 루트 | json fullname | 코덱 | 출처 |
|---|---|---|---|
| header | `NNet.Replay.SHeader` | versioned | user data |
| details | `NNet.Game.SDetails` | versioned | `replay.details` |
| initdata | `NNet.Replay.SInitData` | bitpacked | `replay.initData` |
| game events | 이벤트 ID 표 | bitpacked | `replay.game.events` |
| message events | 이벤트 ID 표 | bitpacked | `replay.message.events` |
| tracker events | 이벤트 ID 표 | versioned | `replay.tracker.events` |
| svaruint32, userid | `NNet.SVarUint32`, `NNet.Replay.SGameUserId` | bitpacked | 이벤트 헤더 |

- 루트 fullname은 생성기 구현 시 json에서 확인하고, py의 `*_typeid`가 가리키는 구조와 대조한다.
- 이벤트 ID → 타입 fullname 표(`game_event_types` 등)는 py에서 읽는다. json enum에서 이름 규칙으로 유추하지 않는다.
- 이벤트 스트림 디코딩 순서(델타 gameloop, userid, eventid, 본문, byte align)는 py `_decode_event_stream`과 같다.
- 이벤트 스트림 인코딩은 원본에 없으므로 새로 만든다. 디코딩의 역순으로 이벤트마다 gameloop 차이(`SVarUint32`), userid(game/message만), eventid, 본문을 쓰고 바이트 정렬한다.

## 생성기 파이프라인

1. **Fetch**: `upstream.json`의 커밋으로 `json/*.json`, `s2protocol/versions/*.py`를 `.upstream/`에 내려받는다.
2. **Parse json**: module/decl 트리를 읽어 fullname → decl 사전을 만든다.
3. **Reach**: 루트와 이벤트 표의 타입에서 도달 가능한 decl만 모은다.
4. **Layout**: 각 타입의 와이어 레이아웃(종류, bounds, 필드 순서·태그)을 계산한다.
5. **Oracle**: py `typeinfos`를 파싱(리터럴 튜플)해 루트·이벤트 typeid에서 시작한 트리와 4의 결과를 구조 비교한다. 불일치면 버전 이름과 경로를 출력하고 실패한다.
6. **Emit**: 버전별 타입·`Read`/`Write`·`IS2Protocol` 구현, 그리고 전 버전을 모아 공통 인터페이스와 `S2ProtocolRegistry` 표를 출력한다.

생성기는 수동 실행 도구다. 결과 `.cs`를 커밋하고, 라이브러리 빌드는 생성기를 실행하지 않는다.

## 공개 API 초안

```csharp
public interface IS2Protocol
{
    int BaseBuild { get; }
    IHeader DecodeHeader(ReadOnlyMemory<byte> data);
    IDetails DecodeDetails(ReadOnlyMemory<byte> data);
    IInitData DecodeInitData(ReadOnlyMemory<byte> data);
    IEnumerable<GameEvent> DecodeGameEvents(ReadOnlyMemory<byte> data);
    IEnumerable<MessageEvent> DecodeMessageEvents(ReadOnlyMemory<byte> data);
    IEnumerable<TrackerEvent> DecodeTrackerEvents(ReadOnlyMemory<byte> data);
    byte[] EncodeHeader(IHeader value);   // 버전 타입이 아니면 ArgumentException
    // EncodeDetails / EncodeInitData / EncodeGameEvents ...
}

public static class S2ProtocolRegistry
{
    public static IReadOnlyList<int> Builds { get; }
    public static IS2Protocol Get(int baseBuild, ProtocolResolveOptions? options = null);
    public static IS2Protocol Latest { get; }
}

public sealed class S2Replay : IDisposable   // MpqArchive 래퍼
{
    public static S2Replay Open(string path, ProtocolResolveOptions? options = null);
    public IHeader Header { get; }        // 최신 프로토콜로 user data 디코딩 (Python과 동일)
    public IS2Protocol Protocol { get; }  // Header의 base build로 선택
    public IDetails ReadDetails();
    public IEnumerable<GameEvent> ReadGameEvents();
    // ReadInitData / ReadMessageEvents / ReadTrackerEvents / ReadAttributes
}
```

이벤트: `ReplayEvent`는 `EventId`, `EventName`(fullname), `GameLoop`, `UserId?`, `Bits`, `Body`(버전별 타입, 공통 인터페이스 구현)를 갖는다.

## 의존성

- `Biz.Bizadm.MPQ.NET` (NuGet, 라이브러리)
- `MSTest` (테스트)
- 생성기: BCL `System.Text.Json`만 사용. py `typeinfos`는 생성기 안의 작은 리터럴 파서로 읽는다.
- 골든 생성: 포터블 Python 3.11 + s2protocol(`.upstream`과 같은 커밋) + mpyq. `versions/__init__.py`가 3.12에서 제거된 `imp`를 import하므로 3.12는 쓰지 않는다

## 위험

- json → 레이아웃 유도 규칙(비트 수, inclusive 처리, typedef 펼침)이 비공개 변환기와 다를 수 있다. D2 대조로 버전별로 드러낸다.
- 생성 타입 수(버전 93 × 루트 도달 타입)에 따른 어셈블리 크기·빌드 시간. 첫 생성 후 측정한다.
- 공통 인터페이스 교집합이 오래된 버전 때문에 지나치게 작아질 수 있다.
