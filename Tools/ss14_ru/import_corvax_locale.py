#!/usr/bin/env python3
"""Import ru-RU translations from Corvax (space-syndicate) key-by-key."""

from __future__ import annotations

import argparse
import json
import logging
import os
import re
import subprocess
import sys
from pathlib import Path

from fluent.syntax import FluentParser, FluentSerializer, ast

from find_untranslated import (
    extract_text_from_pattern,
    get_message_value,
    get_term_value,
    has_cyrillic,
    is_identical_value,
    is_partially_untranslated,
    normalize_text,
)
from file import FluentFile

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

SKIP_PATH_PREFIXES = (
    "_backmen/",
    "_Backmen/",
    "_lavaland/",
    "_Lavaland/",
    "_goobstation/",
    "_Goobstation/",
    "_shitmed/",
    "_Shitmed/",
    "_deadspace/",
    "_DeadSpace/",
)

PLACEHOLDER_RE = re.compile(r"\{\$[^}]+\}|\{ \$[^}]+\}")


def should_skip_path(rel_path: str) -> bool:
    normalized = rel_path.replace("\\", "/")
    return any(normalized.startswith(p) for p in SKIP_PATH_PREFIXES)


def get_placeholders(text: str) -> set[str]:
    return set(PLACEHOLDER_RE.findall(text))


def resolve_corvax_root(arg: str | None) -> Path:
    if arg:
        return Path(arg)
    env = os.environ.get("CORVAX_LOCALE_ROOT")
    if env:
        return Path(env)
    # try git remote
    project_root = Path(__file__).resolve().parent.parent.parent
    try:
        out = subprocess.check_output(
            ["git", "-C", str(project_root), "rev-parse", "--git-dir"],
            text=True,
        ).strip()
        git_dir = Path(out)
        worktree = git_dir.parent if git_dir.name == ".git" else git_dir
        candidate = worktree / "Resources" / "Locale" / "ru-RU"
        if candidate.exists():
            return candidate.parent.parent.parent  # repo root
    except subprocess.CalledProcessError:
        pass
    temp = Path(os.environ.get("TEMP", "/tmp")) / "corvax-ss14-locale" / "Resources" / "Locale" / "ru-RU"
    if temp.exists():
        return temp.parent.parent.parent
    raise SystemExit(
        "Corvax root not found. Pass --corvax-root or set CORVAX_LOCALE_ROOT."
    )


def ensure_corvax_clone(project_root: Path, corvax_root: Path | None) -> Path:
    if corvax_root and (corvax_root / "Resources" / "Locale" / "ru-RU").exists():
        return corvax_root
    temp = Path(os.environ.get("TEMP", "/tmp")) / "corvax-ss14-locale"
    ru = temp / "Resources" / "Locale" / "ru-RU"
    if not ru.exists():
        logger.info("Cloning corvax shallow into %s", temp)
        if temp.exists():
            import shutil
            shutil.rmtree(temp)
        subprocess.check_call(
            [
                "git",
                "clone",
                "--depth",
                "1",
                "--filter=blob:none",
                "--sparse",
                "https://github.com/space-syndicate/space-station-14.git",
                str(temp),
            ]
        )
        subprocess.check_call(
            ["git", "sparse-checkout", "set", "Resources/Locale/ru-RU"],
            cwd=temp,
        )
    return temp


def load_delta_paths(delta_file: Path, project_root: Path) -> set[str]:
    if not delta_file.exists():
        return set()
    paths: set[str] = set()
    for line in delta_file.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line:
            continue
        if line.startswith("Resources/Locale/en-US/"):
            rel = line.replace("Resources/Locale/en-US/", "", 1)
        elif line.startswith("Resources/Locale/ru-RU/"):
            rel = line.replace("Resources/Locale/ru-RU/", "", 1)
        else:
            rel = line
        paths.add(rel.replace("\\", "/"))
    return paths


def en_path_for_ru_rel(project_root: Path, ru_rel: str) -> Path | None:
    if ru_rel.startswith("ss14-ru/"):
        return project_root / "Resources/Locale/en-US" / ru_rel
    return project_root / "Resources/Locale/en-US" / ru_rel


def parse_resource(path: Path) -> ast.Resource | None:
    if not path.exists():
        return None
    try:
        return FluentFile(str(path)).read_parsed_data()
    except Exception as e:
        logger.warning("Failed to parse %s: %s", path, e)
        return None


def collect_entries(parsed: ast.Resource) -> dict[str, tuple[str, ast.Message | ast.Term, str | None]]:
    """key -> (serialized_value, node, attribute_name or None for message value)"""
    result: dict[str, tuple[str, ast.Message | ast.Term, str | None]] = {}
    for element in parsed.body:
        if isinstance(element, ast.Message):
            key = element.id.name
            if element.value:
                result[key] = (get_message_value(element), element, None)
            if element.attributes:
                for attr in element.attributes:
                    attr_key = f"{key}.{attr.id.name}"
                    val = extract_text_from_pattern(attr.value) if attr.value else ""
                    result[attr_key] = (val, element, attr.id.name)
        elif isinstance(element, ast.Term):
            key = f"-{element.id.name}"
            result[key] = (get_term_value(element), element, None)
    return result


def is_untranslated(en_val: str, ru_val: str) -> bool:
    if not en_val or en_val.startswith("{"):
        return False
    if is_identical_value(en_val, ru_val):
        return True
    if is_partially_untranslated(ru_val):
        return True
    if not has_cyrillic(ru_val) and ru_val == en_val:
        return True
    return False


def replace_message_value(node: ast.Message | ast.Term, attr_name: str | None, new_value: str):
    pattern = ast.Pattern([ast.TextElement(new_value)])
    if attr_name is None:
        node.value = pattern
    else:
        for attr in node.attributes or []:
            if attr.id.name == attr_name:
                attr.value = pattern
                return
        if node.attributes is None:
            node.attributes = []
        node.attributes.append(
            ast.Attribute(id=ast.Identifier(attr_name), value=pattern)
        )


def import_file(
    project_root: Path,
    corvax_ru_root: Path,
    ru_rel: str,
    dry_run: bool,
) -> dict[str, list]:
    report: dict[str, list] = {
        "imported": [],
        "skipped": [],
        "manual": [],
    }
    if should_skip_path(ru_rel):
        return report

    ru_path = project_root / "Resources/Locale/ru-RU" / ru_rel.replace("/", os.sep)
    en_path = en_path_for_ru_rel(project_root, ru_rel)
    corvax_path = corvax_ru_root / "ru-RU" / ru_rel.replace("/", os.sep)

    if not ru_path.exists() or not en_path or not en_path.exists():
        return report

    en_parsed = parse_resource(en_path)
    ru_parsed = parse_resource(ru_path)
    corvax_parsed = parse_resource(corvax_path)
    if not en_parsed or not ru_parsed:
        return report

    en_entries = collect_entries(en_parsed)
    ru_entries = collect_entries(ru_parsed)
    corvax_entries = collect_entries(corvax_parsed) if corvax_parsed else {}

    changed = False
    for key, (en_val, _, _) in en_entries.items():
        if key not in ru_entries:
            continue
        ru_val = ru_entries[key][0]
        if not is_untranslated(en_val, ru_val):
            continue
        if key not in corvax_entries:
            report["manual"].append({"file": ru_rel, "key": key, "reason": "no_corvax_key"})
            continue
        corvax_val = corvax_entries[key][0]
        if not has_cyrillic(corvax_val) or is_identical_value(en_val, corvax_val):
            report["manual"].append({"file": ru_rel, "key": key, "reason": "corvax_untranslated"})
            continue
        if get_placeholders(en_val) != get_placeholders(corvax_val):
            report["manual"].append({"file": ru_rel, "key": key, "reason": "placeholder_mismatch"})
            continue

        _, ru_node, attr_name = ru_entries[key]
        if not dry_run:
            replace_message_value(ru_node, attr_name, corvax_val)
            changed = True
        report["imported"].append(
            {"file": ru_rel, "key": key, "en": en_val[:80], "corvax": corvax_val[:80]}
        )

    # Import missing keys from corvax when present in en+ru structure
    for key, (en_val, en_node, attr_name) in en_entries.items():
        if key in ru_entries:
            continue
        if key not in corvax_entries:
            continue
        corvax_val = corvax_entries[key][0]
        if not has_cyrillic(corvax_val):
            continue
        if get_placeholders(en_val) != get_placeholders(corvax_val):
            continue
        if isinstance(en_node, ast.Message) and attr_name is None:
            if not dry_run:
                ru_parsed.body.append(ast.Message(id=ast.Identifier(key), value=ast.Pattern([ast.TextElement(corvax_val)])))
                changed = True
            report["imported"].append({"file": ru_rel, "key": key, "reason": "new_key"})

    if changed and not dry_run:
        serializer = FluentSerializer(with_junk=True)
        FluentFile(str(ru_path)).save_data(serializer.serialize(ru_parsed))

    return report


def iter_ru_files(project_root: Path, delta: set[str] | None) -> list[str]:
    ru_root = project_root / "Resources/Locale/ru-RU"
    files: list[str] = []
    for path in ru_root.rglob("*.ftl"):
        rel = path.relative_to(ru_root).as_posix()
        if should_skip_path(rel):
            continue
        if delta is not None:
            in_delta = rel in delta
            if not in_delta and not rel.startswith("ss14-ru/"):
                # UI delta list uses en-US paths without ss14-ru prefix
                in_delta = rel in {d for d in delta if not d.startswith("ss14-ru/")}
            if not in_delta and rel.startswith("ss14-ru/"):
                proto_rel = rel  # delta en paths include ss14-ru/prototypes/...
                in_delta = proto_rel in delta or rel.replace("ss14-ru/", "") in delta
            if not in_delta:
                continue
        files.append(rel)
    return sorted(files)


def main() -> int:
    parser = argparse.ArgumentParser(description="Import ru-RU from Corvax locale")
    parser.add_argument("--corvax-root", help="Path to corvax repo root")
    parser.add_argument("--delta-list", default="upstream_delta_en.txt")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--all", action="store_true", help="Ignore delta filter")
    args = parser.parse_args()

    if not args.dry_run and not args.apply:
        parser.error("Specify --dry-run or --apply")

    project_root = Path(__file__).resolve().parent.parent.parent
    tools_dir = Path(__file__).resolve().parent

    corvax_repo = ensure_corvax_clone(project_root, Path(args.corvax_root) if args.corvax_root else None)
    corvax_ru_root = corvax_repo / "Resources" / "Locale"

    delta: set[str] | None = None
    if not args.all:
        delta_file = tools_dir / args.delta_list
        delta = load_delta_paths(delta_file, project_root)
        # Also map en-US paths to ru-RU relative
        mapped: set[str] = set()
        for p in delta:
            mapped.add(p.replace("\\", "/"))
            if p.startswith("Resources/Locale/en-US/"):
                mapped.add(p.replace("Resources/Locale/en-US/", ""))
        delta = mapped
        logger.info("Delta filter: %d paths", len(delta))

    # Add corvax remote if missing
    try:
        remotes = subprocess.check_output(
            ["git", "-C", str(project_root), "remote"],
            text=True,
        )
        if "corvax" not in remotes.split():
            subprocess.check_call(
                [
                    "git",
                    "-C",
                    str(project_root),
                    "remote",
                    "add",
                    "corvax",
                    "https://github.com/space-syndicate/space-station-14.git",
                ]
            )
            logger.info("Added git remote corvax")
    except subprocess.CalledProcessError:
        pass

    total = {"imported": [], "skipped": [], "manual": []}
    ru_files = iter_ru_files(project_root, delta)
    logger.info("Processing %d ru-RU files", len(ru_files))

    for ru_rel in ru_files:
        file_report = import_file(project_root, corvax_ru_root, ru_rel, dry_run=args.dry_run)
        for k in total:
            total[k].extend(file_report[k])

    summary = {
        "imported_count": len(total["imported"]),
        "manual_count": len(total["manual"]),
        "dry_run": args.dry_run,
        "details": total,
    }
    report_path = tools_dir / "corvax_import_report.json"
    report_path.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")

    print("\n" + "=" * 60)
    print(f"Imported: {summary['imported_count']}")
    print(f"Manual/skipped: {summary['manual_count']}")
    print(f"Report: {report_path}")
    print("=" * 60)
    return 0


if __name__ == "__main__":
    sys.exit(main())
