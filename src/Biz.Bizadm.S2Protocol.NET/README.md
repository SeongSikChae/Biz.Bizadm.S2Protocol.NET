# Biz.Bizadm.S2Protocol.NET

Pure .NET port of [Blizzard s2protocol](https://github.com/Blizzard/s2protocol) for reading and writing StarCraft II replay data.

## Requirements

- .NET 10 (`net10.0`)

## Install

```bash
dotnet add package Biz.Bizadm.S2Protocol.NET
```

## Quick start

```csharp
using Biz.Bizadm.S2Protocol.NET;

using var replay = S2Replay.Open("replay.SC2Replay");
Console.WriteLine(replay.Header.Version.BaseBuild);

var details = replay.ReadDetails();
foreach (var player in details.PlayerList ?? [])
    Console.WriteLine(System.Text.Encoding.UTF8.GetString(player.Name));

foreach (var ev in replay.ReadTrackerEvents())
    Console.WriteLine($"{ev.GameLoop} {ev.EventName}");
```

`Header`, `ReadDetails()` and `ReadInitData()` return version-common interfaces (`IHeader`, `IDetails`, `IInitData`).
The objects are the generated types of the replay's build (`Versions.ProtocolNNNNN...`); cast to them for fields that
are not common to every build. Event bodies (`ReplayEvent.Body`) are those generated types as well.

## Capabilities

- 93 protocol builds (15405–98310), generated from s2protocol's protocol definitions and checked against its Python
  type tables
- Header, details, init data, game/message/tracker events and attributes, decoded lazily for event streams
- Encoding back to bytes; decoding then encoding reproduces the original bytes for a replay's own build
- Protocol selection by base build with optional fallback (`ProtocolResolveOptions`)

Game and message events of builds up to 23260 and tracker events up to 24944 cannot be decoded, as in s2protocol.

## License

MIT. See `LICENSE` and `THIRD-PARTY-NOTICES.md` in the repository root.
