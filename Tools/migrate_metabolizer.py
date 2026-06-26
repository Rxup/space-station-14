#!/usr/bin/env python3
"""Migrate MetabolizerComponent YAML from groups API to stages API."""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "Resources" / "Prototypes"

GROUP_TO_STAGE = {
    "Food": "Digestion",
    "Drink": "Digestion",
    "Alcohol": "Digestion",
    "Medicine": "Bloodstream",
    "Poison": "Bloodstream",
    "Narcotic": "Bloodstream",
    "Gas": "Respiration",
}


def split_component_block(lines: list[str], start: int) -> tuple[int, list[str]]:
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


def migrate_metabolizer_block(block: list[str]) -> list[str]:
    text = "".join(block)
    if "groups:" not in text and "removeEmpty" not in text and "solution:" not in text:
        return block

    lines = block[:]
    field_indent = re.match(r"^(\s*)- type:", lines[0]).group(1) + "  "

    stages: list[str] = []
    out: list[str] = [lines[0]]
    i = 1
    skip_until = -1
    while i < len(lines):
        if i < skip_until:
            i += 1
            continue
        line = lines[i]
        if re.match(rf"^{re.escape(field_indent)}(removeEmpty|solutionOnBody|solution):", line):
            i += 1
            continue
        if re.match(rf"^{re.escape(field_indent)}groups:\s*$", line):
            j = i + 1
            group_indent = field_indent + "  "
            while j < len(lines):
                id_m = re.match(rf"^{re.escape(group_indent)}- id:\s*(\w+)", lines[j])
                if id_m:
                    stage = GROUP_TO_STAGE.get(id_m.group(1))
                    if stage and stage not in stages:
                        stages.append(stage)
                    j += 1
                    continue
                if lines[j].strip() == "":
                    j += 1
                    continue
                if not lines[j].startswith(group_indent):
                    break
                j += 1
            i = j
            continue
        out.append(line)
        i += 1

    if stages:
        out.insert(1, f"{field_indent}stages: [ {', '.join(stages)} ]\n")
    return out


def migrate_file(path: Path) -> bool:
    lines = path.read_text(encoding="utf-8").splitlines(keepends=True)
    out: list[str] = []
    i = 0
    changed = False
    while i < len(lines):
        if re.match(r"^\s*- type: Metabolizer\s*$", lines[i]):
            end, block = split_component_block(lines, i)
            new_block = migrate_metabolizer_block(block)
            if new_block != block:
                changed = True
            out.extend(new_block)
            i = end
            continue
        out.append(lines[i])
        i += 1
    if changed:
        path.write_text("".join(out), encoding="utf-8")
    return changed


def main() -> int:
    changed = 0
    for path in ROOT.rglob("*.yml"):
        if migrate_file(path):
            changed += 1
            print(path.relative_to(ROOT.parent.parent))
    print(f"Migrated metabolizer in {changed} files")
    return 0


if __name__ == "__main__":
    sys.exit(main())
