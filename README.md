# Biz.Bizadm.S2Protocol.NET

Pure .NET port of Blizzard s2protocol for StarCraft II replays.

Layout: `src/` (library and generated protocol code), `tests/`, `tools/` (protocol code generator, golden script), `docs/`.

Consumer docs (install, quick start): [src/Biz.Bizadm.S2Protocol.NET/README.md](src/Biz.Bizadm.S2Protocol.NET/README.md)

## Build

```bash
dotnet build Biz.Bizadm.S2Protocol.NET.slnx
```

## Test

```bash
dotnet test tests/Biz.Bizadm.S2Protocol.NET.Tests
```

## Regenerate protocol code

```bash
dotnet run --project tools/Biz.Bizadm.S2Protocol.NET.Generator -- fetch
dotnet run --project tools/Biz.Bizadm.S2Protocol.NET.Generator -- verify
dotnet run --project tools/Biz.Bizadm.S2Protocol.NET.Generator -- emit
```

`fetch` downloads the pinned s2protocol commit (`tools/Biz.Bizadm.S2Protocol.NET.Generator/upstream.json`) into the
gitignored `.upstream` folder.

## License

MIT. See `LICENSE` and `THIRD-PARTY-NOTICES.md`.
