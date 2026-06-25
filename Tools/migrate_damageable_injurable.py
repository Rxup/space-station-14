"""Migrate damageContainer from Damageable to Injurable in YAML files."""
from __future__ import annotations

import re
import sys
from pathlib import Path


def split_component_block(lines: list[str], start: int) -> tuple[int, list[str]]:
    base_indent = len(lines[start]) - len(lines[start].lstrip())
    end = start + 1
    while end < len(lines):
        line = lines[end]
        if not line.strip():
            end += 1
            continue
        indent = len(line) - len(line.lstrip())
        if indent <= base_indent and line.lstrip().startswith("- type:"):
            break
        end += 1
    return end, lines[start:end]


def parse_fields(block: list[str]) -> tuple[str, dict[str, list[str]]]:
    comp_type = ""
    fields: dict[str, list[str]] = {}
    current_key = None
    for line in block[1:]:
        m = re.match(r"^(\s+)(\w+):\s*(.*)$", line)
        if m:
            current_key = m.group(2)
            fields[current_key] = [line]
            continue
        if current_key is not None:
            fields[current_key].append(line)
    m = re.search(r"- type:\s*(\S+)", block[0])
    if m:
        comp_type = m.group(1)
    return comp_type, fields


def rebuild_block(header: str, fields: dict[str, list[str]]) -> list[str]:
    out = [header]
    for key in fields:
        out.extend(fields[key])
    return out


def migrate_file(path: Path) -> bool:
    original = path.read_text(encoding="utf-8")
    lines = original.splitlines(keepends=True)
    out: list[str] = []
    i = 0
    changed = False
    while i < len(lines):
        if re.match(r"^\s*- type: Damageable\s*$", lines[i]):
            end, block = split_component_block(lines, i)
            comp_type, fields = parse_fields(block)
            if comp_type == "Damageable" and "damageContainer" in fields:
                container = fields.pop("damageContainer")
                indent = re.match(r"^(\s*)", block[0]).group(1)
                inj_header = f"{indent}- type: Injurable\n"
                new_block = rebuild_block(block[0], fields) + rebuild_block(inj_header, {"damageContainer": container})
                if new_block != block:
                    changed = True
                out.extend(new_block)
                i = end
                continue
        out.append(lines[i])
        i += 1

    if not changed:
        return False
    path.write_text("".join(out), encoding="utf-8")
    return True


def main(paths: list[Path]) -> int:
    changed = 0
    for path in paths:
        if migrate_file(path):
            changed += 1
            print(path)
    print(f"Migrated {changed} files")
    return 0


if __name__ == "__main__":
    targets = [Path(p) for p in sys.argv[1:]] if len(sys.argv) > 1 else list(
        (Path(__file__).resolve().parent.parent / "Resources/Prototypes/_Backmen/Body").rglob("*.yml")
    )
    raise SystemExit(main(targets))
