"""Move temperature damage fields from Temperature to TemperatureDamage component."""
from __future__ import annotations

import re
import sys
from pathlib import Path

DAMAGE_FIELDS = {
    "heatDamageThreshold",
    "coldDamageThreshold",
    "heatDamage",
    "coldDamage",
    "damageCap",
    "parentHeatDamageThreshold",
    "parentColdDamageThreshold",
}


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


def parse_fields(block: list[str]) -> dict[str, list[str]]:
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
    return fields


def rebuild_block(header: str, fields: dict[str, list[str]], key_order: list[str] | None = None) -> list[str]:
    out = [header]
    keys = key_order or list(fields.keys())
    for key in keys:
        if key in fields:
            out.extend(fields[key])
    return out


def migrate_file(path: Path) -> bool:
    lines = path.read_text(encoding="utf-8").splitlines(keepends=True)
    out: list[str] = []
    i = 0
    changed = False
    while i < len(lines):
        if re.match(r"^\s*- type: Temperature\s*$", lines[i]):
            end, block = split_component_block(lines, i)
            fields = parse_fields(block)
            damage_keys = [k for k in fields if k in DAMAGE_FIELDS]
            if damage_keys:
                indent = re.match(r"^(\s*)", block[0]).group(1)
                temp_fields = {k: v for k, v in fields.items() if k not in DAMAGE_FIELDS}
                damage_fields = {k: fields[k] for k in damage_keys}
                new_blocks = rebuild_block(block[0], temp_fields)
                damage_header = f"{indent}- type: TemperatureDamage\n"
                new_blocks.extend(rebuild_block(damage_header, damage_fields))
                if new_blocks != block:
                    changed = True
                out.extend(new_blocks)
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
        if path.is_dir():
            files = list(path.rglob("*.yml"))
        else:
            files = [path]
        for file in files:
            if migrate_file(file):
                changed += 1
                print(file)
    print(f"Migrated {changed} files")
    return 0


if __name__ == "__main__":
    targets = [Path(p) for p in sys.argv[1:]] if len(sys.argv) > 1 else [
        Path(__file__).resolve().parent.parent / "Resources/Prototypes"
    ]
    raise SystemExit(main(targets))
