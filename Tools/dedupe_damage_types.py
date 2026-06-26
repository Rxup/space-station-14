#!/usr/bin/env python3
"""Merge duplicate types: keys inside damage parent blocks."""
from __future__ import annotations

import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(REPO / "Tools"))

from fix_damage_groups_migration import (  # noqa: E402
    DAMAGE_PARENT_KEYS,
    dedupe_damage_blocks,
    expand_groups,
    parse_damage_parent_block,
    render_damage_parent_block,
)

def has_duplicate_types(text: str) -> bool:
    lines = text.splitlines(keepends=True)
    i = 0
    while i < len(lines):
        parsed = parse_damage_parent_block(lines, i)
        if parsed is None:
            i += 1
            continue
        end, _, _, _ = parsed
        if "".join(lines[i:end]).count("types:") > 1:
            return True
        i = end
    return False


def main() -> int:
    targets: list[Path] = []
    for path in (REPO / "Resources" / "Prototypes").rglob("*.yml"):
        text = path.read_text(encoding="utf-8")
        if "damage:" not in text:
            continue
        if has_duplicate_types(text):
            targets.append(path)

    changed = 0
    for path in targets:
        text = path.read_text(encoding="utf-8")
        new_text, fixes = dedupe_damage_blocks(text)
        if fixes and new_text != text:
            path.write_text(new_text, encoding="utf-8")
            print(f"{path.relative_to(REPO).as_posix()}: {fixes}")
            changed += 1

    print(f"Fixed {changed} files")
    return 0


if __name__ == "__main__":
    sys.exit(main())
