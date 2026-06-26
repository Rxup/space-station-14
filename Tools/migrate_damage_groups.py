#!/usr/bin/env python3
"""Expand DamageSpecifier groups: entries to types: (even split across group members)."""
from __future__ import annotations

import re
import sys
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "Resources" / "Prototypes"

DAMAGE_GROUPS: dict[str, list[str]] = {
    "Brute": ["Blunt", "Slash", "Piercing", "ArmorPiercing"],
    "Burn": ["Heat", "Shock", "Cold", "Caustic"],
    "Airloss": ["Asphyxiation", "Bloodloss"],
    "Toxin": ["Poison", "Radiation"],
    "Genetic": ["Cellular"],
    "Metaphysical": ["Holy"],
}

DAMAGE_PARENT_KEYS = ("damage", "healing", "damagePerIntensity", "leech", "healAmount")


def split_group_value(value: str, count: int) -> str:
    if count <= 0:
        return value
    d = Decimal(value) / Decimal(count)
    q = d.quantize(Decimal("0.0001"), rounding=ROUND_HALF_UP)
    text = format(q.normalize(), "f")
    if "." in text:
        text = text.rstrip("0").rstrip(".")
    return text


def expand_groups(types_entries: dict[str, str], groups_entries: list[tuple[str, str]]) -> dict[str, str]:
    result = dict(types_entries)
    for group_id, value in groups_entries:
        dtypes = DAMAGE_GROUPS.get(group_id, [group_id])
        per_type = split_group_value(value, len(dtypes))
        for dtype in dtypes:
            if dtype not in result:
                result[dtype] = per_type
    return result


def migrate_text(text: str) -> str:
    lines = text.splitlines(keepends=True)
    out: list[str] = []
    i = 0
    while i < len(lines):
        line = lines[i]
        parent_match = None
        for key in DAMAGE_PARENT_KEYS:
            m = re.match(rf"^(\s+){key}:\s*$", line)
            if m:
                parent_match = m.group(1)
                break
        if parent_match is None:
            out.append(line)
            i += 1
            continue

        parent_indent = parent_match
        types_indent = parent_indent + "  "
        entry_indent = types_indent + "  "
        j = i + 1
        has_types = False
        types_entries: dict[str, str] = {}
        groups_entries: list[tuple[str, str]] = []
        while j < len(lines):
            cur = lines[j]
            if cur.strip() == "":
                j += 1
                continue
            cur_indent = len(cur) - len(cur.lstrip())
            if cur_indent <= len(parent_indent) and not cur.startswith(types_indent):
                break
            if re.match(rf"^{re.escape(types_indent)}types:\s*$", cur):
                has_types = True
                j += 1
                continue
            if re.match(rf"^{re.escape(types_indent)}groups:\s*$", cur):
                j += 1
                while j < len(lines):
                    gm = re.match(rf"^{re.escape(entry_indent)}(\w+):\s*(-?[\d.]+)", lines[j])
                    if gm:
                        groups_entries.append((gm.group(1), gm.group(2)))
                        j += 1
                        continue
                    if lines[j].strip() == "":
                        j += 1
                        continue
                    break
                continue
            tm = re.match(rf"^{re.escape(entry_indent)}(\w+):\s*(-?[\d.]+)", cur)
            if has_types and tm:
                types_entries[tm.group(1)] = tm.group(2)
                j += 1
                continue
            break
        if groups_entries:
            out.append(line)
            merged = expand_groups(types_entries, groups_entries)
            out.append(f"{types_indent}types:\n")
            for dtype, value in merged.items():
                out.append(f"{entry_indent}{dtype}: {value}\n")
            i = j
            continue
        out.append(line)
        i += 1
    return "".join(out)


def main() -> int:
    changed = 0
    for path in ROOT.rglob("*.yml"):
        original = path.read_text(encoding="utf-8")
        new = migrate_text(original)
        if new != original:
            path.write_text(new, encoding="utf-8")
            changed += 1
            print(path.relative_to(ROOT.parent.parent))
    print(f"Expanded groups in {changed} files")
    return 0


if __name__ == "__main__":
    sys.exit(main())
