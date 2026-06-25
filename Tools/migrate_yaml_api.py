#!/usr/bin/env python3
"""Migrate fork YAML for upstream/stable API changes."""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "Resources" / "Prototypes"

TEMP_DAMAGE_FIELDS = {
    "heatDamage",
    "coldDamage",
    "coldDamageThreshold",
    "heatDamageThreshold",
    "damageCap",
}

METABOLISM_CATEGORIES = {
    "Medicine": "Medicine",
    "Poison": "Poison",
    "Narcotic": "Narcotic",
}


def split_component_block(lines: list[str], start: int) -> tuple[int, list[str]]:
    """Return (end_index_exclusive, block_lines) for component at start."""
    base_indent = len(lines[start]) - len(lines[start].lstrip())
    end = start + 1
    while end < len(lines):
        line = lines[end]
        if line.strip() == "":
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


def migrate_damageable(block: list[str]) -> list[str]:
    comp_type, fields = parse_fields(block)
    if comp_type != "Damageable" or "damageContainer" not in fields:
        return block

    container_lines = fields.pop("damageContainer")
    indent = re.match(r"^(\s*)", block[0]).group(1)
    injurable_header = f"{indent}- type: Injurable"
    injurable_fields = {"damageContainer": [f"{indent}    damageContainer: {container_lines[0].split(':',1)[1].strip()}\n"]}
    return rebuild_block(block[0], fields) + rebuild_block(injurable_header, injurable_fields)


def migrate_temperature(block: list[str]) -> list[str]:
    comp_type, fields = parse_fields(block)
    if comp_type != "Temperature":
        return block

    move = {k: fields.pop(k) for k in list(fields) if k in TEMP_DAMAGE_FIELDS}
    if not move:
        return block

    indent = re.match(r"^(\s*)", block[0]).group(1)
    td_header = f"{indent}- type: TemperatureDamage"
    return rebuild_block(block[0], fields) + rebuild_block(td_header, move)


def migrate_metabolisms(text: str) -> str:
    for old, new in METABOLISM_CATEGORIES.items():
        text = re.sub(
            rf"^(\s+){old}:$",
            rf"\1{new}:",
            text,
            flags=re.MULTILINE,
        )
    return text


def migrate_file(path: Path) -> bool:
    original = path.read_text(encoding="utf-8")
    text = original

    # Hypospray -> Injector rename
    text = text.replace("- type: Hypospray", "- type: Injector")

    text = migrate_metabolisms(text)

    lines = text.splitlines(keepends=True)
    out: list[str] = []
    i = 0
    changed = False
    while i < len(lines):
        if re.match(r"^\s*- type: Damageable\s*$", lines[i]) or re.match(
            r"^\s*- type: Damageable\n", lines[i]
        ):
            end, block = split_component_block(lines, i)
            new_block = migrate_damageable(block)
            if new_block != block:
                changed = True
            out.extend(new_block)
            i = end
            continue
        if re.match(r"^\s*- type: Temperature\s*$", lines[i]):
            end, block = split_component_block(lines, i)
            new_block = migrate_temperature(block)
            if new_block != block:
                changed = True
            out.extend(new_block)
            i = end
            continue
        out.append(lines[i])
        i += 1

    new_text = "".join(out)
    if new_text != original:
        path.write_text(new_text, encoding="utf-8")
        return True
    return False


def main() -> int:
    changed_files = 0
    for path in ROOT.rglob("*.yml"):
        if migrate_file(path):
            changed_files += 1
            print(path.relative_to(ROOT.parent.parent))
    print(f"Migrated {changed_files} files")
    return 0


if __name__ == "__main__":
    sys.exit(main())
