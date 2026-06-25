#!/usr/bin/env python3
"""Merge duplicate metabolism stage keys within reagent prototypes in YAML files."""

from __future__ import annotations

import re
import sys
from pathlib import Path


def merge_metabolisms(content: str) -> tuple[str, int]:
    """Merge duplicate stage blocks under metabolisms: for each reagent document."""
    lines = content.splitlines(keepends=True)
    out: list[str] = []
    i = 0
    merges = 0

    while i < len(lines):
        line = lines[i]
        out.append(line)

        # Start of metabolisms block at reagent level (4 spaces before metabolisms:)
        if re.match(r"^  metabolisms:\s*$", line):
            stage_blocks: dict[str, list[str]] = {}
            stage_order: list[str] = []
            i += 1

            while i < len(lines):
                stage_match = re.match(r"^    (\w+):\s*$", lines[i])
                if stage_match:
                    stage = stage_match.group(1)
                    block_lines = [lines[i]]
                    i += 1
                    while i < len(lines):
                        if re.match(r"^    \w+:\s*$", lines[i]):
                            break
                        if re.match(r"^- type: reagent\s*$", lines[i]):
                            break
                        block_lines.append(lines[i])
                        i += 1

                    if stage in stage_blocks:
                        merges += 1
                        existing = stage_blocks[stage]
                        # Append effects from duplicate (skip stage header and metabolismRate if duplicate)
                        new_body = block_lines[1:]
                        # Find effects: in new block
                        effects_idx = None
                        for j, bl in enumerate(new_body):
                            if re.match(r"^      effects:\s*$", bl):
                                effects_idx = j
                                break
                        if effects_idx is not None:
                            existing_effects_idx = None
                            for j, bl in enumerate(existing):
                                if re.match(r"^      effects:\s*$", bl):
                                    existing_effects_idx = j
                                    break
                            if existing_effects_idx is not None:
                                existing.extend(new_body[effects_idx + 1 :])
                            else:
                                existing.extend(new_body[effects_idx:])
                        else:
                            existing.extend(new_body)
                    else:
                        stage_order.append(stage)
                        stage_blocks[stage] = block_lines
                    continue

                if re.match(r"^- type: reagent\s*$", lines[i]) or re.match(
                    r"^  \w", lines[i]
                ):
                    break

                # Unexpected line at wrong indent - end metabolisms
                if lines[i].strip() and not lines[i].startswith("    "):
                    break

                stage_blocks.setdefault("_orphan", []).append(lines[i])
                i += 1

            for stage in stage_order:
                out.extend(stage_blocks[stage])
            if "_orphan" in stage_blocks:
                out.extend(stage_blocks["_orphan"])
            continue

        i += 1

    return "".join(out), merges


def main(paths: list[str]) -> int:
    total = 0
    for path_str in paths:
        path = Path(path_str)
        text = path.read_text(encoding="utf-8")
        merged, count = merge_metabolisms(text)
        if count:
            path.write_text(merged, encoding="utf-8", newline="\n")
            print(f"{path}: merged {count} duplicate stage(s)")
            total += count
    print(f"Done. Total merges: {total}")
    return 0


if __name__ == "__main__":
    targets = sys.argv[1:] or [
        "Resources/Prototypes/_Backmen/Reagents/narcotics.yml",
        "Resources/Prototypes/_Backmen/Reagents/medicine.yml",
        "Resources/Prototypes/_Backmen/Reagents/toxins.yml",
        "Resources/Prototypes/_Backmen/Reagents/psionic.yml",
        "Resources/Prototypes/_Backmen/Reagents/dry_hands.yml",
        "Resources/Prototypes/_Backmen/Reagents/Consumable/Drink/alcohol.yml",
    ]
    raise SystemExit(main(targets))
