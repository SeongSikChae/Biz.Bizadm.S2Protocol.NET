"""Blizzard s2protocol(Python)로 리플레이를 디코딩해 골든 JSON을 만든다.

포터블 Python 3.11로 실행한다(s2protocol versions/__init__.py가 3.12에서 제거된 imp를 쓴다).

    python -I tools/golden/make_golden.py <upstream_dir> <replay_dir> <output_dir>

- upstream_dir: 생성기 fetch가 내려받은 .upstream (s2protocol 패키지가 있는 폴더)
- 리플레이마다 <output_dir>/<이름>/ 아래 header.json.gz, details.json.gz, initdata.json.gz,
  attributes.json.gz, game.ndjson.gz, message.ndjson.gz, tracker.ndjson.gz 를 쓴다(gzip, 긴 리플레이는 80MB 이상).
- bytes는 {"$b64": ...}. versioned struct에서 typeinfo에 있지만 데이터에 없던 필드는 stats.json에 센다.
"""

import base64
import collections
import gzip
import io
import json
import pathlib
import sys


def main(upstream_dir, replay_dir, output_dir):
    sys.path.insert(0, str(pathlib.Path(upstream_dir).resolve()))
    import mpyq
    from s2protocol import decoders, versions

    missing = collections.Counter()
    original_struct = decoders.VersionedDecoder._struct

    def counting_struct(self, fields):
        result = original_struct(self, fields)
        if isinstance(result, dict):
            for field in fields:
                if field[0] != '__parent' and field[0] not in result:
                    missing[field[0]] += 1
        return result

    decoders.VersionedDecoder._struct = counting_struct

    def default(value):
        if isinstance(value, bytes):
            return {"$b64": base64.b64encode(value).decode("ascii")}
        raise TypeError(type(value).__name__)

    # mtime=0: 같은 입력이면 같은 바이트(커밋된 골든이 재생성만으로 바뀌지 않게).
    def open_gzip(path):
        return io.TextIOWrapper(gzip.GzipFile(path, "wb", compresslevel=9, mtime=0), encoding="utf-8", newline="\n")

    def dump(path, value):
        with open_gzip(path) as f:
            json.dump(value, f, default=default, sort_keys=True, separators=(",", ":"))

    def dump_lines(path, values):
        count = 0
        with open_gzip(path) as f:
            for value in values:
                f.write(json.dumps(value, default=default, sort_keys=True, separators=(",", ":")))
                f.write("\n")
                count += 1
        return count

    for replay in sorted(pathlib.Path(replay_dir).glob("*.SC2Replay")):
        missing.clear()
        archive = mpyq.MPQArchive(str(replay))
        header = versions.latest().decode_replay_header(archive.header["user_data_header"]["content"])
        build = header["m_version"]["m_baseBuild"]
        protocol = versions.build(build)
        out = pathlib.Path(output_dir) / replay.stem
        out.mkdir(parents=True, exist_ok=True)

        dump(out / "header.json.gz", header)
        dump(out / "details.json.gz", protocol.decode_replay_details(archive.read_file("replay.details")))
        dump(out / "initdata.json.gz", protocol.decode_replay_initdata(archive.read_file("replay.initData")))
        dump(out / "attributes.json.gz",
             protocol.decode_replay_attributes_events(archive.read_file("replay.attributes.events") or b""))
        counts = {
            "game": dump_lines(out / "game.ndjson.gz",
                               protocol.decode_replay_game_events(archive.read_file("replay.game.events"))),
            "message": dump_lines(out / "message.ndjson.gz",
                                  protocol.decode_replay_message_events(archive.read_file("replay.message.events"))),
            "tracker": dump_lines(out / "tracker.ndjson.gz",
                                  protocol.decode_replay_tracker_events(archive.read_file("replay.tracker.events") or b"")),
        }
        with open(out / "stats.json", "w", encoding="utf-8", newline="\n") as f:
            json.dump({"build": build, "events": counts, "missing_versioned_fields": dict(missing)}, f, sort_keys=True)
        print(f"{replay.name}: build={build} {counts} missing_versioned_fields={sum(missing.values())}")


if __name__ == "__main__":
    if len(sys.argv) != 4:
        sys.exit(__doc__)
    main(*sys.argv[1:])
