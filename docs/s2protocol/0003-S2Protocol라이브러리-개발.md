---
status: done
category: s2protocol
feature: S2Protocol라이브러리
phase: 개발
---

# S2Protocol 라이브러리 개발 계획

선행 문서: `docs/s2protocol/0001-S2Protocol라이브러리-방안.md`, `docs/s2protocol/0002-S2Protocol라이브러리-설계.md`

## 공통

- 단위마다 구현 → 해당 단위 테스트 통과 → 다음 단위. 전체 일괄 테스트로 대체하지 않는다.
- 테스트 실행: `dotnet test tests/Biz.Bizadm.S2Protocol.NET.Tests --filter "FullyQualifiedName~<클래스>"`
- 코드 변경 시 `vibe-harness` 태스크 파일을 사용한다.
- 네임스페이스: 공개 `Biz.Bizadm.S2Protocol.NET`, 와이어 계층 `.Decoding`/`.Encoding`, 생성물 `.Versions.Protocol{NNNNN}.*`, 공통 인터페이스 `.Common.*`.
- 생성기 실행: `dotnet run --project tools/Biz.Bizadm.S2Protocol.NET.Generator -- <fetch|verify|emit> [--build NNNNN]`

### 확정 외부 요소

| 항목 | 값 | 근거 |
|---|---|---|
| s2protocol 커밋 | `3b40c5ef01834096b4305172ef5485ec00133dcc` | 2026-10-30 `git ls-remote` HEAD. json/py 93개(15405~98310) |
| Biz.Bizadm.MPQ.NET | 1.0.0 | nuget.org 공개 버전. 설치 시 `dotnet-nuget-package` 규칙으로 취약·deprecated 재확인 |
| MSTest | 4.5.2 | nuget.org 최신, MPQ.NET 저장소와 동일 |
| Python | 포터블 CPython 3.11 + mpyq | 골든 생성 전용. 3.11이 tools에 없으면 `portable-python` 절차로 설치·등록. s2protocol `versions/__init__.py`의 `import imp` 때문에 3.12 불가 |

### 단위 순서

1 스캐폴딩 → 2 기본 타입 → 3 BitPackedBuffer → 4 BitPackedDecoder → 5 VersionedDecoder → 6 인코더 → 7 생성기 Fetch → 8 json 모델·상수 평가·도달 → 9 레이아웃·py 대조 → 10 버전 코드 출력(1개 버전) → 11 이벤트 스트림·IS2Protocol → 12 공통 인터페이스·레지스트리 → 13 전 버전 출력·크기 측정 → 14 Attributes → 15 S2Replay → 16 골든 비교 → 17 인코더 왕복 → 18 패키징

단위 16·17은 사용자가 제공하는 `.SC2Replay`가 필요하다. 샘플이 없으면 해당 단위 테스트는 `Inconclusive`로 두고 보고한다.

---

## 단위 1. 스캐폴딩

- **대상**: `Directory.Build.props`, `Directory.Packages.props`, `src/Biz.Bizadm.S2Protocol.NET/*.csproj`, `tests/Biz.Bizadm.S2Protocol.NET.Tests/*.csproj`, `tools/Biz.Bizadm.S2Protocol.NET.Generator/*.csproj`, `Biz.Bizadm.S2Protocol.NET.slnx`, `.gitignore`(`tools/**/.upstream/`)
- **코드 초안**:

```xml
<!-- Directory.Packages.props -->
<ItemGroup>
  <PackageVersion Include="Biz.Bizadm.MPQ.NET" Version="1.0.0" />
  <PackageVersion Include="MSTest" Version="4.5.2" />
</ItemGroup>

<!-- src/Biz.Bizadm.S2Protocol.NET/Biz.Bizadm.S2Protocol.NET.csproj -->
<ItemGroup>
  <InternalsVisibleTo Include="Biz.Bizadm.S2Protocol.NET.Tests" />
  <PackageReference Include="Biz.Bizadm.MPQ.NET" />
</ItemGroup>
```

```powershell
dotnet new classlib -n Biz.Bizadm.S2Protocol.NET -f net10.0 -o src/Biz.Bizadm.S2Protocol.NET
dotnet new mstest -n Biz.Bizadm.S2Protocol.NET.Tests -f net10.0 -o tests/Biz.Bizadm.S2Protocol.NET.Tests
dotnet new console -n Biz.Bizadm.S2Protocol.NET.Generator -f net10.0 -o tools/Biz.Bizadm.S2Protocol.NET.Generator
dotnet sln Biz.Bizadm.S2Protocol.NET.slnx add src/Biz.Bizadm.S2Protocol.NET/Biz.Bizadm.S2Protocol.NET.csproj --solution-folder src
dotnet sln Biz.Bizadm.S2Protocol.NET.slnx add tests/Biz.Bizadm.S2Protocol.NET.Tests/Biz.Bizadm.S2Protocol.NET.Tests.csproj --solution-folder tests
dotnet sln Biz.Bizadm.S2Protocol.NET.slnx add tools/Biz.Bizadm.S2Protocol.NET.Generator/Biz.Bizadm.S2Protocol.NET.Generator.csproj --solution-folder tools
```

- **검증**: `dotnet build Biz.Bizadm.S2Protocol.NET.slnx` 성공, `dotnet package list --vulnerable --include-transitive`·`--deprecated` 결과 없음, 템플릿 테스트 1건 통과
- **선행 단위**: 없음

## 단위 2. 기본 타입

- **대상**: `Types/FourCC.cs`, `Types/S2BitArray.cs`, `Types/S2FieldAttribute.cs`, `UnitTag.cs`, `Decoding/CorruptedReplayException.cs`, `Protocols/UnsupportedProtocolException.cs`
- **코드 초안**:

```csharp
public readonly record struct FourCC(uint Value)
{
    public override string ToString() =>
        Encoding.ASCII.GetString([(byte)(Value >> 24), (byte)(Value >> 16), (byte)(Value >> 8), (byte)Value]);
}

public sealed record S2BitArray(int Length, byte[] Data);

[AttributeUsage(AttributeTargets.Property)]
public sealed class S2FieldAttribute(string name) : Attribute
{
    public string Name { get; } = name;   // 원본 필드명(m_...). 골든 직렬화에 쓴다
}

public static class UnitTag   // 태그는 32비트 부호 없는 값이므로 long (index >= 0x2000이면 int는 음수)
{
    public static long Create(int index, int recycle) => ((long)index << 18) + recycle;
    public static int Index(long tag) => (int)((tag >> 18) & 0x3fff);
    public static int Recycle(long tag) => (int)(tag & 0x3ffff);
}
```

- **검증**: `UnitTagTests`(Create/Index/Recycle 왕복), `FourCCTests`(`"s2ma"` 문자열화)
- **선행 단위**: 1

## 단위 3. BitPackedBuffer

- **대상**: `Decoding/BitPackedBuffer.cs`
- **코드 초안** (Python `read_bits`: 바이트 내 LSB부터, 빅엔디언 누적):

```csharp
public ulong ReadBits(int bits)
{
    ulong result = 0;
    int resultBits = 0;
    while (resultBits != bits)
    {
        if (_nextBits == 0)
        {
            if (Done) throw new CorruptedReplayException("truncated");
            _next = _data.Span[_used++];
            _nextBits = 8;
        }
        int copyBits = Math.Min(bits - resultBits, _nextBits);
        ulong copy = (ulong)(_next & ((1 << copyBits) - 1));
        result |= _bigEndian ? copy << (bits - resultBits - copyBits) : copy << resultBits;
        _next >>= copyBits;
        _nextBits -= copyBits;
        resultBits += copyBits;
    }
    return result;
}
public bool Done => _nextBits == 0 && _used >= _data.Length;
public long UsedBits => _used * 8L - _nextBits;
```

64비트를 넘는 읽기(`S2BitArray`)는 `ReadBitsToBytes(int bits)`로 따로 둔다. Python bitpacked `read_bits(length)`가 만드는 정수를 빅엔디언 바이트로 표현한다(길이 `(bits+7)/8`, 상위 바이트의 남는 비트는 0). versioned는 정렬 바이트를 그대로 쓴다.

빅엔디언 `read_bits`는 분할 읽기로 합성되지 않는다(13비트 한 번 ≠ 5비트 후 8비트: 청크 크기가 현재 바이트 위치로 정해지기 때문). 따라서 `ReadBits`를 나눠 부르지 않고 같은 청크 배치를 바이트 배열에 직접 OR 한다.

```csharp
int copyBits = ReadChunk(bits - resultBits, out int copy);   // ReadBits와 공유
int shift = _bigEndian ? bits - resultBits - copyBits : resultBits;
int index = result.Length - 1 - shift / 8, offset = shift % 8;
result[index] |= (byte)(copy << offset);
if (offset + copyBits > 8) result[index - 1] |= (byte)(copy >> (8 - offset));
```

- **검증**: `BitPackedBufferTests` — 손으로 만든 바이트열에서 3·5·13비트 읽기, `ReadBitsToBytes(13)`이 `ReadBits(13)` 정수의 빅엔디언 2바이트와 같음, `ByteAlign` 후 정렬 읽기, 끝에서 읽으면 예외, 리틀엔디언 모드
- **선행 단위**: 2

## 단위 4. BitPackedDecoder

- **대상**: `Decoding/BitPackedDecoder.cs`
- **코드 초안**:

```csharp
public long ReadInt(long offset, int bits) => unchecked(offset + (long)_buffer.ReadBits(bits));   // (-2^63, 64) 포함
public ulong ReadUInt64(int bits) => _buffer.ReadBits(bits);                                       // (0, 64) → ulong 필드
public bool ReadBool() => ReadInt(0, 1) != 0;
public bool Done => _buffer.Done;
public long UsedBits => _buffer.UsedBits;
public void ByteAlign() => _buffer.ByteAlign();
public byte[] ReadBlob(long offset, int bits) => _buffer.ReadAlignedBytes(checked((int)ReadInt(offset, bits)));
public bool ReadOptionalExists() => ReadBool();
// Python: read_unaligned_bytes(4) = 8비트 4회. 비정렬 위치에서는 ReadBits(32)와 결과가 다르다
public FourCC ReadFourCC() => new(BinaryPrimitives.ReadUInt32BigEndian(_buffer.ReadUnalignedBytes(4)));
public float ReadReal32() => BinaryPrimitives.ReadSingleBigEndian(_buffer.ReadUnalignedBytes(4));
```

array 길이·choice 태그는 `ReadInt`를 그대로 쓴다. 미지원 choice 태그 예외는 생성 코드가 던진다.

`VersionedDecoder`도 `Done`/`UsedBits`/`ByteAlign`을 같은 방식으로 노출한다.

- **검증**: `BitPackedDecoderTests` — offset 있는 int, `(-2^63,64)`·`(0,64)` 경계값, blob 길이·정렬, optional 없음/있음
- **선행 단위**: 3

## 단위 5. VersionedDecoder

- **대상**: `Decoding/VersionedDecoder.cs`
- **코드 초안**:

```csharp
public long ReadVInt()
{
    int b = (int)_buffer.ReadBits(8);
    bool negative = (b & 1) != 0;
    long result = (b >> 1) & 0x3f;
    int bits = 6;
    while ((b & 0x80) != 0)
    {
        b = (int)_buffer.ReadBits(8);
        result |= (long)(b & 0x7f) << bits;
        bits += 7;
    }
    return negative ? -result : result;
}

public void ExpectSkip(int expected)
{
    if ((int)_buffer.ReadBits(8) != expected) throw new CorruptedReplayException("skip mismatch");
}

public void SkipInstance()
{
    switch ((int)_buffer.ReadBits(8))
    {
        case 0: for (long n = ReadVInt(); n > 0; n--) SkipInstance(); break;
        case 1: _buffer.ReadAlignedBytes((int)((ReadVInt() + 7) / 8)); break;
        case 2: _buffer.ReadAlignedBytes((int)ReadVInt()); break;
        case 3: ReadVInt(); SkipInstance(); break;
        case 4: if (_buffer.ReadBits(8) != 0) SkipInstance(); break;
        case 5: for (long n = ReadVInt(); n > 0; n--) { ReadVInt(); SkipInstance(); } break;
        case 6: _buffer.ReadAlignedBytes(1); break;
        case 7: _buffer.ReadAlignedBytes(4); break;
        case 8: _buffer.ReadAlignedBytes(8); break;
        case 9: ReadVInt(); break;
    }
}
```

생성 코드의 versioned struct 읽기 패턴(단위 10에서 출력):

```csharp
for (long n = d.ReadStructFieldCount(); n > 0; n--)   // ExpectSkip(5) + ReadVInt
{
    switch (d.ReadVInt())
    {
        case 0: r.Flags = (int)d.ReadInt(); break;   // ReadInt = ExpectSkip(9) + ReadVInt
        // ...
        default: d.SkipInstance(); break;
    }
}
```

- **검증**: `VersionedDecoderTests` — 음수·다중 바이트 vint, skip 코드 0~9 각각, 알 수 없는 struct 태그 skip
- **선행 단위**: 3

## 단위 6. 인코더

- **대상**: `Encoding/BitPackedBufferWriter.cs`, `Encoding/BitPackedEncoder.cs`, `Encoding/VersionedEncoder.cs`
- **코드 초안**:

```csharp
public void WriteBits(ulong value, int bits)   // ReadBits의 역: 빅엔디언에서 상위 비트부터 현재 바이트의 하위로 채움
{
    while (bits > 0)
    {
        int copyBits = Math.Min(bits, 8 - _nextBits);
        int copy = (int)(value >> (bits - copyBits)) & ((1 << copyBits) - 1);
        _next |= copy << _nextBits;
        _nextBits += copyBits;
        bits -= copyBits;
        if (_nextBits == 8) Flush();
    }
}

public void WriteVInt(long value)
{
    ulong v = (ulong)Math.Abs(value);
    int b = (int)((v & 0x3f) << 1) | (value < 0 ? 1 : 0);
    v >>= 6;
    while (true)
    {
        if (v != 0) b |= 0x80;
        _writer.WriteBits((ulong)b, 8);
        if (v == 0) break;
        b = (int)(v & 0x7f);
        v >>= 7;
    }
}
```

인코더는 `encoders.py` 포팅이 아니라 단위 3~5 디코더의 정확한 역이다. 특히 bitpacked FourCC·real은 정렬 없이 8비트씩 바이트 수만큼 쓰고(`WriteBits(b, 8)` 반복. 비정렬 위치에서 `WriteBits(value, 32)`와 다름), blob은 길이를 `_int`로 쓴 뒤 정렬 바이트를 쓴다.

- **검증**: `EncoderRoundTripTests` — 단위 3~5 테스트의 바이트열을 인코더로 다시 만들어 일치, 랜덤 값 왕복(시드 고정)
- **선행 단위**: 4, 5

## 단위 7. 생성기 Fetch

- **대상**: `tools/.../upstream.json`, `tools/.../FetchCommand.cs`
- **코드 초안**:

```json
{ "repository": "Blizzard/s2protocol", "commit": "3b40c5ef01834096b4305172ef5485ec00133dcc" }
```

```csharp
var tree = await http.GetFromJsonAsync<GitTree>($"https://api.github.com/repos/{repo}/git/trees/{commit}?recursive=1");
foreach (var path in tree.Tree.Select(t => t.Path)
             .Where(p => Regex.IsMatch(p, @"^(json/protocol\d+\.json|s2protocol/.+\.py)$")))   // 골든용 decoders.py 등 패키지 전체 포함
{
    var bytes = await http.GetByteArrayAsync($"https://raw.githubusercontent.com/{repo}/{commit}/{path}");
    await File.WriteAllBytesAsync(Path.Combine(upstreamDir, path), bytes);
}
```

- **검증**: `fetch` 실행 후 `.upstream/json` 93개, `.upstream/s2protocol/versions/protocol*.py` 93개, `.upstream/s2protocol/decoders.py` 존재, `git status`에 `.upstream`이 나오지 않음
- **선행 단위**: 1

## 단위 8. json 모델·상수 평가·도달

- **대상**: `tools/.../Model/ProtocolDocument.cs`, `ConstantEvaluator.cs`, `Reachability.cs`
- **코드 초안**:

```csharp
// decl 사전: fullname → JsonElement(type_info)
static void Collect(JsonElement module, Dictionary<string, JsonElement> decls)
{
    foreach (var decl in module.GetProperty("decls").EnumerateArray())
        if (decl.GetProperty("type").GetString() == "Module") Collect(decl, decls);
        else decls[decl.GetProperty("fullname").GetString()!] = decl;
}

BigInteger Eval(JsonElement expr) => expr.GetProperty("type").GetString() switch
{
    "IntLiteral" => BigInteger.Parse(expr.GetProperty("value").GetString()!),
    "NegateExpr" => -Eval(expr.GetProperty("rhs")),
    "PowExpr" => BigInteger.Pow(Eval(expr.GetProperty("lhs")), (int)Eval(expr.GetProperty("rhs"))),
    // LShiftExpr, MulExpr, OrExpr도 lhs/rhs
    "IdentifierExpr" => EvalIdentifier(expr.GetProperty("fullname").GetString()!),   // ConstDecl 또는 enum 멤버
    var t => throw new NotSupportedException(t),
};
```

내려받은 원본 93개 전체 조사로 확정한 사실(구현 시 반영):

- 구버전 23개 json에 비표준 이스케이프 `\x1b`(리플레이 서명 StringLiteral)가 있다. 파싱 전에 `\xHH` → `\u00HH`로 바꾼다.
- 타입 노드: `IntType`, `BoolType`, `BlobType`, `StringType`, `AsciiStringType`, `FourCCType`, `BitArrayType`, `NullType`, `ArrayType`·`DynArrayType`(`element_type`), `OptionalType`(`type_info`), `StructType`(`parents`=`ParentStructField`, `fields`=`MemberStructField`), `ChoiceType`(`MemberChoiceField`), `EnumType`·`InumType`(`MemberEnumField`·`MemberInumField`, 인라인 정의 있음), `UserType`. real 타입은 json·py 모두 없다.
- 식 노드: `IntLiteral`, `NegateExpr`(`rhs`), `PowExpr`·`LShiftExpr`·`MulExpr`·`OrExpr`(`lhs`, `rhs`), `IdentifierExpr`(`fullname`).
- bounds의 min/max에는 원본이 계산한 `evalue`와 `inclusive`가 있다. 레이아웃은 `evalue`를 쓰고, 평가기는 모든 bounds의 식을 `evalue`와 대조해 스스로를 검증한다. 평가기가 꼭 필요한 곳은 enum 멤버 값이다.
- 루트 fullname: `NNet.Replay.SHeader`, `NNet.Game.SDetails`, `NNet.Replay.SInitData`, `NNet.SVarUint32`, `NNet.Replay.SGameUserId`, eventid는 `NNet.Game.EEventId`·`NNet.Game.EMessageId`·`NNet.Replay.Tracker.EEventId`.
- 15405~23260(17개)은 py `replay_userid_typeid = None`이라 Python도 game/message 이벤트를 디코딩하지 못하고, 24944 이하(18개)는 `tracker_eventid_typeid = None`이다. 이 스트림은 해당 버전에서 미지원으로 표시한다(단위 11에서 `NotSupportedException`).

- **검증**: `verify --build 98310` 출력을 증거로 쓴다(테스트 프로젝트는 생성기를 참조하지 않는다) — 98310에서 루트와 이벤트 표 타입이 모두 해석되고, 미지원 노드·bounds 평가 불일치가 없음. 전 버전 `verify`도 실행해 보고한다
- **선행 단위**: 7

## 단위 9. 레이아웃·py 대조

- **대상**: `tools/.../Layout/WireType.cs`, `LayoutBuilder.cs`, `PyTypeInfoParser.cs`, `OracleComparer.cs`, `VerifyCommand.cs`
- **코드 초안**:

```csharp
abstract record WireType;
sealed record WInt(BigInteger Offset, int Bits) : WireType;
sealed record WStruct(IReadOnlyList<(string Name, WireType Type, int Tag)> Fields) : WireType;
sealed record WChoice(BigInteger Offset, int Bits, IReadOnlyDictionary<int, (string Name, WireType Type)> Cases) : WireType;
// WArray, WBlob, WBitArray, WOptional, WBool, WFourCC, WNull, WReal32, WReal64

static int BitsFor(BigInteger min, BigInteger max) => (int)(max - min).GetBitLength();   // 대조로 확인됨

// py: ('_int',[(0,7)]) 같은 리터럴을 재귀 하강으로 파싱 → 같은 WireType 트리로 변환
bool Equal(WireType a, WireType b, string path, List<string> errors);
```

`__parent` 필드: json `parents`를 py와 같이 `('__parent', typeid, -1)` 형태 필드로 정규화해 비교한다.

py 대조로 확정한 유도 규칙(93개 버전 전부 일치):

- int/blob/bitarray/array/choice: `(min, bitlen(max - min))`, bounds는 `evalue`·`inclusive` 기준. 고정 길이는 `(n, 0)`.
- `StringType` bounds는 문자 수다. 와이어 길이는 UTF-8 바이트라 min·max에 4를 곱한다(`AsciiStringType`·`BlobType`은 그대로).
- bounds가 null인 enum은 멤버 값의, choice는 경우 태그의 최소~최대 범위를 쓴다(null bounds인 `IntType`은 ConstDecl에만 있고 도달하지 않음).
- 태그 없는 struct 필드의 태그는 `k - n`(k: 실제 필드 순번, n: `ConstDecl` 멤버까지 센 전체 수). 이벤트 struct의 `ConstDecl` 멤버(`EEVENTID`, `Synchronous`, `Synthesized`, `Debug`, `Senders`)는 와이어에 없다.
- 15405의 `('__parent', 40, -1)` 단일 부모 struct도 위 규칙으로 일치한다.

- **검증**: `verify` 실행 시 93개 버전 모두 대조 통과. 실패 버전이 있으면 경로·차이를 보고하고 유도 규칙을 고친다(같은 실패 2회면 중단·보고)
- **선행 단위**: 8

## 단위 10. 버전 코드 출력(1개 버전)

- **대상**: `tools/.../Emit/TypeEmitter.cs`, `CodecEmitter.cs`, 출력 `src/.../Generated/Versions/Protocol98310/*.g.cs`
- **코드 초안** (생성 결과 모양):

```csharp
// <auto-generated/>
namespace Biz.Bizadm.S2Protocol.NET.Versions.Protocol98310.Replay;

public sealed partial class SHeader
{
    [S2Field("m_signature")] public byte[] Signature { get; set; } = [];
    [S2Field("m_version")] public SVersion Version { get; set; } = new();
    // ...

    internal static SHeader ReadVersioned(VersionedDecoder d) { /* 단위 5 패턴 */ }
    internal static void WriteVersioned(VersionedEncoder e, SHeader v) { /* 역순 */ }
}

public abstract partial class SCmdData
{
    public sealed partial class None : SCmdData { }
    public sealed partial class TargetPoint : SCmdData { public SMapCoord3D Value { get; set; } = new(); }

    internal static SCmdData ReadBitPacked(BitPackedDecoder d) => d.ReadInt(0, 2) switch
    {
        0 => new None(),
        1 => new TargetPoint { Value = SMapCoord3D.ReadBitPacked(d) },
        var t => throw new CorruptedReplayException($"choice tag {t}"),
    };
}
```

메서드는 타입이 도달하는 코덱(bitpacked/versioned)만 출력한다.

구현에서 정한 세부(98310 출력 176개 타입, 5,431줄):

- 배열·optional은 디코더·인코더에 추가한 제네릭 도우미(`ReadArray`, `ReadOptional`/`ReadOptionalValue`, `WriteArray`, `WriteOptional`/`WriteOptionalValue`)로 식 하나에 출력한다.
- 정수 C# 타입은 와이어 범위(`offset ~ offset + 2^bits - 1`)로 int/long/ulong을 고른다. 이름 있는 EnumType/InumType decl은 enum, 인라인 enum은 정수.
- 클래스는 이름 있는 StructType/ChoiceType decl만 만들고, 별칭 decl(정수·blob·배열)은 대상 타입으로 펼친다. 인라인 struct/choice(98310은 `m_eventData` 1건)는 소유 클래스의 중첩 타입 `{속성}Struct`/`{속성}Choice`.
- choice 경우 클래스에 `[S2Field("원본 경우 이름")]`을 붙인다(`S2FieldAttribute`를 클래스에도 허용). 모든 choice에 `UnknownCase(Tag)`를 두어 versioned에서 모르는 태그를 Python처럼 건너뛴 결과로 쓰고, 인코딩 시 예외를 던진다.
- `__parent` 필드는 설계의 기반 클래스 대신 `Parent` 속성으로 출력한다(와이어 순서 동일). 해당 버전은 15405의 단일 부모 1건이다.
- `emit`은 `verify`와 같은 py 대조를 먼저 수행하고 실패한 버전은 출력하지 않는다(`ProtocolModel`로 공통화).
- versioned 정수를 int·enum으로 좁힐 때는 `checked` 캐스트를 쓴다(vint는 bounds를 강제하지 않으므로 잘림 대신 예외). ulong 코덱은 offset 0인 범위에만 쓴다.
- versioned에서 빠진 필드: 설계 D10으로 결정(2026-10-31). 필드 존재 기록은 단위 16에서 생성기에 추가한다.

- **검증**: `emit --build 98310` 후 `dotnet build` 성공. 단위 테스트에서 98310 타입으로 손 인코딩한 값 → 디코딩 왕복. 추가로 모든 bitpacked 생성 타입에 무작위 바이트 디코딩 → 인코딩 안정성(정렬 패딩 비트는 원본과 직접 비교하지 않음)
- **선행 단위**: 6, 9

## 단위 11. 이벤트 스트림·IS2Protocol

- **대상**: `Events/ReplayEvent.cs`, `Protocols/IS2Protocol.cs`, 생성 `Protocol98310/Protocol.g.cs`
- **코드 초안** (Python `_decode_event_stream` 대응):

```csharp
public abstract record ReplayEvent(int EventId, string EventName, long GameLoop, int? UserId, long Bits, object Body);

// Protocol.g.cs
public IEnumerable<GameEvent> DecodeGameEvents(ReadOnlyMemory<byte> data)
{
    var d = new BitPackedDecoder(data);
    long gameLoop = 0;
    while (!d.Done)
    {
        long start = d.UsedBits;
        gameLoop += SVarUint32.ReadBitPacked(d).GetValue();   // choice → 생성 헬퍼(Python _varuint32_value 대응)
        int userId = SGameUserId.ReadBitPacked(d).UserId;
        int eventId = (int)d.ReadInt(0, 7);
        object body = eventId switch
        {
            5 => SUserFinishedLoadingSyncEvent.ReadBitPacked(d),
            // ... py game_event_types 표
            _ => throw new CorruptedReplayException($"eventid {eventId}"),
        };
        d.ByteAlign();
        yield return new GameEvent(eventId, Names[eventId], gameLoop, userId, d.UsedBits - start, body);
    }
}
```

tracker events는 `VersionedDecoder`, userid 없음. header는 user data 바이트를 versioned로 디코딩.

py에서 userid/eventid 루트가 `None`인 버전(단위 8 참고)은 해당 스트림의 Decode/Encode가 `NotSupportedException`을 던진다(Python과 동일하게 디코딩 불가).

구현에서 정한 세부:

- 생성 클래스 이름은 `Versions.ProtocolNNNNN.Protocol`(네임스페이스 마지막 이름과 같은 클래스명을 피함).
- 이벤트 레코드: `ReplayEvent(EventId, EventName, GameLoop, Bits, Body)`를 기반으로 `GameEvent`·`MessageEvent`는 `UserId(int)`, `TrackerEvent`는 UserId 없음(설계의 공통 `UserId?` 대신 스트림별).
- `IS2Protocol`의 header/details/initdata는 이번 단위에서 `object`로 다루고 단위 12에서 공통 인터페이스로 좁힌다.
- Python tracker 스트림은 gameloop 차이(`SVarUint32`)도 versioned로 읽으므로, tracker가 있는 버전은 `SVarUint32`에 versioned 코덱도 출력한다.
- 생성기는 `SVarUint32`가 offset 0 정수 경우들의 choice인지, `SGameUserId`가 정수 필드 하나인 struct인지 확인하고 아니면 출력을 실패시킨다. 값 추출은 `VarUInt32Value`, 인코딩 경우 선택은 `VarUInt32`(가장 작은 경우)로 `Protocol`에 private 출력한다.
- 이벤트 본문 인코딩은 eventid별 `case id when ev.Body is T b`로 타입을 확인하고, 다르면 `ArgumentException`.

`SVarUint32`는 choice(`m_uint6`/`m_uint14`/`m_uint22`/`m_uint32`)이므로 생성기가 `public long GetValue()`(각 경우의 값 반환)를 함께 출력한다. 인코딩은 값이 들어가는 가장 작은 경우를 고른다. 단위 17 왕복에서 원본 바이트와 다르면 원래 경우를 이벤트 메타에 보존하는 방식으로 바꾼다.

이벤트 스트림 인코딩(원본에 없음, 디코딩의 역):

```csharp
public byte[] EncodeGameEvents(IEnumerable<GameEvent> events)
{
    var e = new BitPackedEncoder();
    long gameLoop = 0;
    foreach (var ev in events)
    {
        SVarUint32.WriteBitPacked(e, SVarUint32.FromValue(ev.GameLoop - gameLoop));   // 가장 작은 경우 선택
        gameLoop = ev.GameLoop;
        SGameUserId.WriteBitPacked(e, new SGameUserId { UserId = ev.UserId!.Value });
        e.WriteInt(ev.EventId, offset: 0, bits: 7);
        WriteGameEventBody(e, ev.EventId, ev.Body);   // eventId switch → 버전 타입 Write, 타입 불일치면 ArgumentException
        e.ByteAlign();
    }
    return e.ToArray();
}
```

- **검증**(추가): 손으로 만든 이벤트 2건 → 인코드 → 디코드 결과 동일

- **검증**: 손으로 인코딩한 이벤트 2건 스트림 → gameloop 누적·bits·eventid 확인
- **선행 단위**: 10

## 단위 12. 공통 인터페이스·레지스트리

- **대상**: `tools/.../Emit/CommonInterfaceEmitter.cs`, `RegistryEmitter.cs`, `Protocols/S2ProtocolRegistry.cs`, `Protocols/ProtocolResolveOptions.cs`
- **코드 초안**:

```csharp
// 교집합: 해당 fullname을 정의한 모든 버전에서 (원본 이름, C# 타입)이 같은 필드만
var common = versions.Where(v => v.Types.ContainsKey(fullName))
    .Select(v => v.Types[fullName].Fields.Select(f => (f.WireName, f.CommonClrType)).ToHashSet())
    .Aggregate((a, b) => { a.IntersectWith(b); return a; });

// 출력
namespace Biz.Bizadm.S2Protocol.NET.Common.Replay;
public partial interface IHeader { long ElapsedGameLoops { get; } IVersion Version { get; } }
// 버전 타입: IVersion IHeader.Version => Version;  (명시적 구현)

public enum ProtocolFallback { None, NearestLower, Latest }
public static IS2Protocol Get(int baseBuild, ProtocolResolveOptions? options = null)
{
    if (s_protocols.TryGetValue(baseBuild, out var p)) return p();
    return (options?.Fallback ?? ProtocolFallback.None) switch
    {
        ProtocolFallback.NearestLower when s_builds.LastOrDefault(b => b < baseBuild) is > 0 and var b => s_protocols[b](),
        ProtocolFallback.Latest => Latest,
        _ => throw new UnsupportedProtocolException(baseBuild),
    };
}
```

중첩 타입 필드의 공통 C# 타입은 해당 타입의 공통 인터페이스(배열은 `IReadOnlyList<I...>`)로 본다. enum 필드는 기반 정수형으로 비교·노출하고(`int IFoo.Result => (int)Result;`), choice 필드는 교집합에서 제외한다.

이름·생략 규칙(설계 문서): 인터페이스명은 `S` 접두를 뗀 `I{Name}`(`SHeader` → `IHeader`). 같은 fullname이 버전마다 종류가 다르면 인터페이스를 만들지 않고 경고한다. 이름 충돌은 단위 10 출력 단계에서 실패시킨다. 버전 프로토콜 인스턴스는 지연 생성·캐시한다.

구현에서 정한 세부:

- 교집합은 93개 버전 전체로 계산한다. `emit`은 `--build`가 있어도 전 버전을 대조·요약한 뒤 요청 빌드만 출력하고, `Generated/Common/Common.g.cs`(공통 인터페이스)와 `Generated/Versions/S2ProtocolRegistry.g.cs`(출력된 빌드 목록)를 매번 다시 쓴다.
- 필드 키: 정수는 CLR 타입, enum은 `E:기반형`, struct는 `I:fullname`, 배열 `L(k)`, optional `O(k)`. enum이 들어간 배열은 C# 변환이 없어 뺀다. 인터페이스가 없는 struct를 가리키는 필드도 뺀다.
- 한 버전이라도 struct가 아닌 종류로 정의된 fullname은 인터페이스를 만들지 않는다(현재 `NNet.Game.SSelectionMask`: 15405는 struct, 이후 choice).
- 클래스는 공통 인터페이스를 선언하고 교집합 필드마다 명시적 구현을 둔다. `IS2Protocol`의 header/details/initdata는 `IHeader`/`IDetails`/`IInitData`(단위 11의 `object`를 좁힘).
- `emit --build N`은 이미 출력된 빌드도 함께 다시 만든다(공통 인터페이스가 바뀌면 옛 명시적 구현이 컴파일을 깨므로). 한 빌드라도 실패하면 아무것도 쓰지 않는다. header/details/initdata 루트가 별칭으로 다른 decl을 가리키면 출력을 실패시킨다.
- 인터페이스 필드 순서는 최신 빌드 기준이라, 빌드가 추가되면 `Common.g.cs`에 동작과 무관한 순서 diff가 생길 수 있다.
- 결과(98310 출력 시점): 공통 인터페이스 155개. `IHeader.Version.BaseBuild`, `IDetails.PlayerList`가 교집합에 남는다.

- **검증**: 98310만 출력한 상태로 `Get(98310)` 성공, 없는 빌드 예외, 폴백 옵션 2종
- **선행 단위**: 11

## 단위 13. 전 버전 출력·크기 측정

- **대상**: `emit`(전체), `src/.../Generated/**`
- **코드 초안**:

```powershell
dotnet run --project tools/Biz.Bizadm.S2Protocol.NET.Generator -- emit
dotnet build Biz.Bizadm.S2Protocol.NET.slnx -c Release
(Get-Item src/Biz.Bizadm.S2Protocol.NET/bin/Release/net10.0/Biz.Bizadm.S2Protocol.NET.dll).Length
```

측정 결과(2026-10-31):

| 항목 | 값 |
|---|---|
| 생성 소스 | `.g.cs` 95개(버전 93 + Common + Registry), 26.1MB. 버전당 타입 117(15405)~176(98310) |
| `emit`(전체) | 약 8초 |
| Release 빌드(`--no-incremental`, 솔루션 전체) | 28.7초, 경고 0 |
| DLL 크기 | Release 15.82MB, Debug 17.01MB |
| 공통 인터페이스 | 155개(`SSelectionMask`는 종류가 달라 제외) |

단일 어셈블리(설계 D5)를 유지할 수 있는 크기다(nuget.org 패키지 한도 250MB).

- **검증**: 빌드 성공, `S2ProtocolRegistry.Builds.Count == 93`, `IHeader`에 `Version.BaseBuild`와 `IDetails`에 플레이어 목록이 교집합으로 남아 있음(단위 15 전제), DLL 크기·빌드 시간을 기록해 사용자에게 보고(설계 D5 재검토 판단 근거)
- **선행 단위**: 12

## 단위 14. Attributes

- **대상**: `Attributes/ReplayAttributes.cs`, `Attributes/ReplayAttribute.cs`
- **코드 초안** (Python `decode_replay_attributes_events`, 리틀엔디언):

```csharp
var buffer = new BitPackedBuffer(data, bigEndian: false);
var result = new ReplayAttributes();
if (!buffer.Done)
{
    result.Source = (int)buffer.ReadBits(8);
    result.MapNamespace = (int)buffer.ReadBits(32);
    int count = (int)buffer.ReadBits(32);
    while (!buffer.Done)
    {
        int ns = (int)buffer.ReadBits(32), attrId = (int)buffer.ReadBits(32), scope = (int)buffer.ReadBits(8);
        byte[] value = buffer.ReadAlignedBytes(4).Reverse().ToArray();   // 이후 0 바이트 제거
        result.Add(scope, attrId, new ReplayAttribute(ns, attrId, scope, TrimZero(value)));
    }
}
```

구현에서 정한 세부:

- `decode_replay_attributes_events` 본문은 93개 py에서 동일하다(확인). 그래서 `IS2Protocol`에 넣지 않고 버전 무관 정적 API `ReplayAttributes.Decode`/`Encode`로 둔다.
- Python `scopes[scope][attrid]` 묶음은 원래 순서를 잃는다. 그래서 파일 순서 목록 `Attributes`를 보관하고, `Scopes`는 그 위의 묶음 보기로 둔다. 인코딩은 목록으로 한다.
- Python `value`(4바이트 역순 후 양쪽 0 제거)는 되돌릴 수 없다. 그래서 `ReplayAttribute.RawValue`(원본 4바이트)를 보관하고 `Value`는 계산 속성으로 둔다.
- 빈 입력은 Python `{}`처럼 `Source`/`MapNamespace`가 null이다. count는 읽고 무시하며, 인코딩은 실제 개수를 쓴다. 원본 count가 실제와 다르면 단위 17 왕복에서 드러난다.
- `attributes.py`의 attribute ID 상수 60개는 `AttributeIds`로 옮겼다.

- **검증**: 손으로 만든 attributes 바이트 → scope/attrId별 값 확인. 인코딩(`EncodeAttributes`) 왕복
- **선행 단위**: 3, 6

## 단위 15. S2Replay

- **대상**: `S2Replay.cs`
- **코드 초안**:

```csharp
public static S2Replay Open(string path, ProtocolResolveOptions? options = null)
{
    var archive = MpqArchive.Open(path);
    var header = S2ProtocolRegistry.Latest.DecodeHeader(archive.UserData!.Content);
    var protocol = S2ProtocolRegistry.Get(header.Version.BaseBuild, options);
    return new S2Replay(archive, header, protocol);
}

public IDetails ReadDetails() => Protocol.DecodeDetails(ReadFile("replay.details"));
private byte[] ReadFile(string name)
{
    using var s = _archive.OpenFile(name);
    using var ms = new MemoryStream();
    s.CopyTo(ms);
    return ms.ToArray();
}
```

`MpqUserData`의 실제 내용 속성 이름은 MPQ.NET 소스로 확인한다.

구현에서 정한 세부:

- `MpqUserData.Content`(MPQ.NET)가 직렬화된 헤더다. user data가 없으면 `CorruptedReplayException`.
- `replay.details`/`replay.initData`가 없으면 `FileNotFoundException`, 이벤트 3종·attributes 파일이 없으면 빈 결과(tracker 파일이 없는 구버전 리플레이).
- 합성 리플레이 테스트: `MpqArchive.Create`로 만든 아카이브 앞에 user data 블록(16바이트 + 헤더 내용, 헤더 오프셋 0x400)을 붙인다.
- 실제 샘플은 `tests/Biz.Bizadm.S2Protocol.NET.Tests/TestData/Replays/*.SC2Replay`에 두면 출력 폴더로 복사되어 끝까지 디코딩된다(없으면 Inconclusive).
- 샘플(2026-10-31): 사용자 지정 `G:\StarcraftAI\sessions`의 151개가 모두 base build 97563(AI 대전, 2인)이다. 크기·길이가 다른 4개를 복사했다. 네 파일 모두 끝까지 디코딩되고(game 이벤트 최대 165,025건), `replay.attributes.events`는 0바이트다. 실제 샘플로 검증한 빌드는 97563 하나이고, attributes 실데이터는 없다.

- **검증**: 샘플 리플레이로 Open → Header.BaseBuild, Details 플레이어 수 확인(샘플 없으면 Inconclusive)
- **선행 단위**: 13, 14

## 단위 16. 골든 비교

- **대상**: `tools/golden/make_golden.py`, `tests/.../TestData/<replay>.golden/*.ndjson`, `tests/.../Golden/GoldenSerializer.cs`, `GoldenTests.cs`
- **코드 초안**:

```python
# 포터블 Python 3.11, .upstream 커밋의 s2protocol을 sys.path에 둔다
def default(o):
    if isinstance(o, bytes): return {"$b64": base64.b64encode(o).decode()}
    raise TypeError(type(o))
for name, fn in [("details", protocol.decode_replay_details), ...]:
    out.write(json.dumps(fn(archive.read_file(f"replay.{name}")), default=default, sort_keys=True))
```

```csharp
// .NET 객체 → Python과 같은 모양의 JsonNode: 속성은 [S2Field] 원본 이름, byte[]는 {"$b64":...},
// S2BitArray는 코덱별: bitpacked → [length, 정수](Data를 빅엔디언 정수로), versioned → [length, {"$b64":...}],
// choice는 {caseName: value}, 이벤트는 _event/_eventid/_gameloop/_userid/_bits 추가
JsonNode ToGolden(object? value);
```

먼저 각 샘플의 header base build가 `S2ProtocolRegistry.Builds`에 있는지 확인한다. 없는 샘플은 골든 비교·왕복 대상에서 빼고 빌드 번호와 함께 보고한다.

설계 D10(필드 존재 기록) 구현을 이 단위에 포함한다. 순서: 골든 생성 → 샘플에서 빠진 versioned 필드 실측·보고 → 생성기에 존재 기록 추가 → 직렬화기·비교.

```csharp
// 생성 versioned struct (개념)
internal global::System.Collections.Generic.HashSet<long>? PresentTags { get; set; }   // null = 모든 필드 있음

// ReadVersioned: 읽은 태그를 기록
r.PresentTags ??= [];
switch (tag) { case 0: r.Flags = ...; r.PresentTags.Add(0); break; ... }

// WriteVersioned: 기록된 필드만
bool Has(long t) => v.PresentTags is null || v.PresentTags.Contains(t);
e.WriteStructFieldCount((Has(0) ? 1 : 0) + (Has(1) ? 1 : 0) + ...);
if (Has(0)) { e.WriteVInt(0); ... }
```

골든 직렬화기는 `PresentTags`에 없는 필드를 내보내지 않는다(Python dict와 같은 모양).

결과(2026-10-31):

- 포터블 Python 3.11.18(PBS 20261026)을 `H:\.agents\tools\python-3.11`에 설치·등록하고 mpyq 0.2.5를 설치했다.
- 골든은 gzip으로 `tests/.../TestData/Golden/<리플레이>/`에 둔다(긴 리플레이 원문 81MB → 1.24MB, 4개 합계 1.35MB). 재생성: `python-3.11 -I tools/golden/make_golden.py <.upstream> <TestData/Replays> <TestData/Golden>`.
- 실측: 네 샘플 모두 versioned 누락 필드가 4개(`m_dataDeprecated` 2, `m_name` 2)다. 그래서 D10이 실제로 필요하다. Python 이벤트 수는 .NET과 같다.
- 생성기: versioned 코덱이 있는 struct에 `PresentTags`, `[S2Field(..., Tag = n)]`을 둔다.
- 비교: 네 샘플 모두 header/details/initdata/attributes와 game/message/tracker 전 이벤트(최대 165,025건)가 골든과 일치한다.

- **검증**: 지원 빌드 샘플 전체에서 header/details/initdata/3종 이벤트/attributes가 골든과 JSON 동등
- **선행 단위**: 15

## 단위 17. 인코더 왕복

- **대상**: `IS2Protocol.Encode*` 생성 메서드, `RoundTripTests.cs`
- **코드 초안**:

```csharp
var bytes = ReadFile("replay.game.events");
var events = protocol.DecodeGameEvents(bytes).ToList();
CollectionAssert.AreEqual(bytes, protocol.EncodeGameEvents(events));
```

- **검증**: 지원 빌드 샘플 전체에서 header/details/initdata/3종 이벤트/attributes 바이트 일치. 왕복 보장은 정확 일치 프로토콜에 한한다(폴백 프로토콜은 versioned에서 모르는 태그를 skip해 정보가 빠질 수 있음). XML 문서에 이 제약을 적는다
- **선행 단위**: 16

## 단위 18. 패키징

- **대상**: `src/.../Biz.Bizadm.S2Protocol.NET.csproj`, `src/.../README.md`, `README.md`, `THIRD-PARTY-NOTICES.md`(Blizzard s2protocol MIT 고지)
- **코드 초안** (MPQ.NET csproj와 같은 패턴):

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <Version>1.0.0</Version>
  <Authors>SeongSikChae</Authors>
  <PackageLicenseFile>LICENSE</PackageLicenseFile>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <PackageTags>sc2;starcraft2;replay;s2protocol</PackageTags>
</PropertyGroup>
<ItemGroup>
  <None Include="..\..\LICENSE" Pack="true" PackagePath="" />
  <None Include="..\..\THIRD-PARTY-NOTICES.md" Pack="true" PackagePath="" />
  <None Include="README.md" Pack="true" PackagePath="\" />
</ItemGroup>
```

생성 파일은 `<auto-generated/>` 표시로 XML 문서 경고 대상에서 빠진다.

- **검증**: `dotnet-nuget-pack` 규칙대로 `--no-incremental` 빌드 후 `dotnet pack --no-build`, `.nupkg`에 README·LICENSE·NOTICES 포함, 경고 0
- **선행 단위**: 17
