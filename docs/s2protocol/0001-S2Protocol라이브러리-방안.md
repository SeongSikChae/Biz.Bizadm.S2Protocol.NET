---
status: done
category: s2protocol
feature: S2Protocol라이브러리
phase: 방안
---

# S2Protocol 라이브러리 방안

## 배경

`Biz.Bizadm.S2Protocol.NET` 저장소는 빈 `.slnx`(src/, tests/ 폴더)만 있는 상태다. 다음 자료를 참고해
StarCraft II 리플레이 내용을 디코딩·인코딩하는 C# 클래스 라이브러리를 만든다.

- s2protocol: https://github.com/Blizzard/s2protocol (MIT)
- s2protocol 프로토콜 정의: https://github.com/Blizzard/s2protocol/tree/master/json (`protocolNNNNN.json` 93개, 15405~98310)
- Biz.Bizadm.MPQ.NET: `D:\Git\Lib\Biz.Bizadm.MPQ.NET` (nuget.org `Biz.Bizadm.MPQ.NET` 1.0.0)

## 문제·목표

- .NET 10에서 `.SC2Replay`의 header, details, initdata, game/message/tracker events, attributes를 강타입 객체로 디코딩한다.
- 같은 강타입 객체를 리플레이 바이너리 형식으로 다시 인코딩한다.
- MPQ 아카이브 처리는 `Biz.Bizadm.MPQ.NET`에 맡기고, 본 라이브러리는 추출된 바이트의 해석을 담당한다.

## 범위

| 항목 | 결정 |
|---|---|
| 기능 | 읽기 전체(header, details, initdata, game/message/tracker events, attributes, unit tag 유틸) + 인코더(디코더의 정확한 역연산. `encoders.py`는 참고만 하며, 원본의 버그는 따르지 않는다) |
| 프로토콜 정의 | 원본 `json/protocolNNNNN.json`을 입력으로 **오프라인 생성기**가 빌드별 C# 코드를 생성하고 `.cs`로 커밋한다 |
| 결과 모델 | 전체 강타입 클래스. 동적 사전(`Dictionary<string, object?>`) 모델은 두지 않는다 |
| 타입 배치 | 빌드별 네임스페이스(예: `Versions.Protocol98310`) + 주요 이벤트·구조에 버전 공통 인터페이스를 생성한다 |
| MPQ | `Biz.Bizadm.MPQ.NET` NuGet 패키지 참조. 패키지 선정은 `dotnet-nuget-package` 규칙(TFM 호환·최신 패치·비취약)을 따른다 |
| 구성 | `src/Biz.Bizadm.S2Protocol.NET`(classlib, net10.0) + `tests/Biz.Bizadm.S2Protocol.NET.Tests`(MSTest) + `tools/`(생성기 콘솔), `Directory.Build.props`/`Directory.Packages.props`(CPM). NuGet 패키징 설정은 MPQ.NET csproj와 같은 패턴 |

## 비범위

- CLI(`s2_cli.py`), `diff.py`, `build.py` 대응 도구.
- 원본 json의 런타임 로드·임베드.
- 리플레이 분석 로직(APM, 빌드오더 등). 상위 라이브러리가 담당한다.
- MPQ 아카이브 읽기·쓰기 자체. `Biz.Bizadm.MPQ.NET`이 담당한다.

## 채택 방안 개요

설계 단계에서 구체화한다.

- 공개 API
  - 리플레이 진입점: `MpqArchive`의 user data에서 header를 디코딩해 base build를 얻고, 해당 빌드의 프로토콜로 각 파일(`replay.details`, `replay.initData`, `replay.game.events` 등)을 디코딩
  - 디코더/인코더: `BitPackedBuffer`, `BitPackedDecoder`, `VersionedDecoder` 및 그 역연산 인코더
  - 빌드별 생성 코드: 타입 클래스, 디코드/인코드 메서드, 이벤트 ID ↔ 타입 매핑
- 생성기: json AST(`StructType`, `ChoiceType`, `EnumType`, `ArrayType`, `OptionalType`, `UserType`, `MinMaxConstraint` 등)를 읽어 C# 소스를 출력

설계 단계 결정 사항:

- json AST → Python `typeinfos` 대응 비트팩/버전드 디코더 코드 매핑
- 공통 인터페이스 대상 선정 기준과 버전별 필드 차이 처리(누락 필드 nullable 등)
- 93개 버전 생성물의 어셈블리 크기 대응(필요 시 버전 범위 축소·분리 패키지)
- 원본 json 보관 위치(저장소 미포함, 서브모듈, 다운로드 스크립트 중 선택)
- 빌드 번호 → 프로토콜 선택 규칙(정확 일치만 vs 가장 가까운 하위 빌드 폴백)
- 테스트용 리플레이 샘플 확보와 Python s2protocol 출력 대조 검증 방식

## 다음 단계

1. 설계: `docs/s2protocol/0002-S2Protocol라이브러리-설계.md`
2. 개발 계획: `docs/s2protocol/0003-S2Protocol라이브러리-개발.md`
3. 검토 후 단위별 구현·테스트
