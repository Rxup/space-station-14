#!/usr/bin/env python3
"""Mirror ru-only FTL keys/files into en-US so localize --sync keeps them.

Copies structure/values from ru-RU. Prefer English where ru is already Latin;
Cyrillic values are copied as-is so LocIds exist (polish en-US later if needed).
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))

from localize import FtlAttribute, FtlEntry, FtlParser  # noqa: E402


def main() -> None:
    data = json.loads(
        Path(
            r"C:\Users\backm\.cursor\projects\d-src-space-station-14\agent-tools\en_us_missing.json"
        ).read_text(encoding="utf-8")
    )
    files: list[str] = data["files"]
    key_records: list[dict] = data["keys"]

    ru_dir = ROOT / "Resources" / "Locale" / "ru-RU"
    en_dir = ROOT / "Resources" / "Locale" / "en-US"

    print(f"Mirroring {len(files)} whole files...")
    for i, rel in enumerate(files, 1):
        ru_path = ru_dir / rel
        en_path = en_dir / rel
        if not ru_path.exists():
            continue
        text = ru_path.read_text(encoding="utf-8-sig")
        en_path.parent.mkdir(parents=True, exist_ok=True)
        en_path.write_text(text, encoding="utf-8")
        if i % 25 == 0 or i == len(files):
            print(f"  {i}/{len(files)}")

    by_file: dict[str, list[dict]] = {}
    for rec in key_records:
        by_file.setdefault(rec["rel"], []).append(rec)

    print(f"Patching {len(by_file)} shared files ({len(key_records)} records)...")
    patched = 0
    for rel, recs in by_file.items():
        en_path = en_dir / rel
        ru_path = ru_dir / rel
        if not en_path.exists() or not ru_path.exists():
            continue
        en_entries, trailing, ends = FtlParser.parse_file(en_path)
        ru_entries, _, _ = FtlParser.parse_file(ru_path)
        changed = False
        for rec in recs:
            key = rec["key"]
            attr = rec.get("attr")
            if key not in ru_entries:
                continue
            ru_e = ru_entries[key]
            if attr:
                if key not in en_entries:
                    en_entries[key] = ru_e
                    changed = True
                    continue
                if attr not in en_entries[key].attributes and attr in ru_e.attributes:
                    en_entries[key].attributes[attr] = ru_e.attributes[attr]
                    changed = True
            elif key not in en_entries:
                en_entries[key] = ru_e
                changed = True
        if changed:
            FtlParser.write_file(en_path, en_entries, trailing, ends)
            patched += 1

    print(f"Done. whole_files={len(files)} shared_files_patched={patched}")


if __name__ == "__main__":
    main()
