#!/usr/bin/env python3
"""Export Guidebook rules and contraband prototypes to Wiki.js markdown."""

from __future__ import annotations

import json
import re
import shutil
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
PROTOTYPES = REPO / "Resources" / "Prototypes"
SERVER_INFO = REPO / "Resources" / "ServerInfo"
LOCALE_RU = REPO / "Resources" / "Locale" / "ru-RU"
OUT = REPO / "docs" / "wiki" / "ss14"
TEXTURES = REPO / "Resources" / "Textures"
ICON_OUT = OUT / "contraband-icons"
ICON_WIKI_PATH = "/ss14/contraband-icons"

SEVERITY_ORDER = [
    "Restricted",
    "Minor",
    "Major",
    "GrandTheft",
    "HighlyIllegal",
    "Syndicate",
    "Magical",
    "Psionic",
]

SEVERITY_TITLES = {
    "Restricted": "Ограниченная (отдел / должность)",
    "Minor": "Мелкая контрабанда",
    "Major": "Крупная контрабанда",
    "GrandTheft": "Гранд-кража",
    "HighlyIllegal": "Крайне незаконная",
    "Syndicate": "Контрабанда Синдиката",
    "Magical": "Магическая контрабанда",
    "Psionic": "Псионическая контрабанда",
}

RULE_FILES = [
    ("RulesBackmen.xml", "Правила сервера BACKMEN", None),
    ("Cat1.xml", "Напутствующие слова", "backmen-rules-header-2"),
    ("Cat2.xml", "Не быть мудаком", "backmen-rules-header-3"),
    ("Cat3.xml", "Выход из роли", "backmen-rules-header-4"),
    ("Cat4.xml", "Нарушение игровой атмосферы", "backmen-rules-header-5"),
    ("Cat5.xml", "Метагейм", "backmen-rules-header-6"),
    ("Cat6.xml", "Ответственная игра за антагониста", "backmen-rules-header-7"),
    ("Cat7.xml", "White-List роли", "backmen-rules-header-8"),
    ("Cat8.xml", "Нечестная игра", "backmen-rules-header-9"),
    ("Cat9.xml", "ERP — нет", "backmen-rules-header-10"),
]

RULES_DIR = SERVER_INFO / "_Backmen" / "Guidebook" / "ServerRules"


def load_ftl_names() -> dict[str, str]:
    names: dict[str, str] = {}
    pattern = re.compile(r"^ent-([A-Za-z0-9]+)\s*=\s*(.+)$")
    for path in LOCALE_RU.rglob("*.ftl"):
        try:
            text = path.read_text(encoding="utf-8")
        except OSError:
            continue
        for line in text.splitlines():
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            match = pattern.match(line)
            if match:
                names[match.group(1)] = match.group(2).strip()
    return names


def load_department_names() -> dict[str, str]:
    path = LOCALE_RU / "job" / "department.ftl"
    names: dict[str, str] = {}
    if not path.exists():
        return names
    for line in path.read_text(encoding="utf-8").splitlines():
        match = re.match(r"^department-(\w+)\s*=\s*(.+)$", line.strip())
        if match:
            names[match.group(1)] = match.group(2).strip()
    return names


CAT_ANCHORS = {
    "BackmenCat1": "напутствующие-слова",
    "BackmenCat2": "не-быть-мудаком",
    "BackmenCat3": "выход-из-роли",
    "BackmenCat4": "нарушение-игровой-атмосферы",
    "BackmenCat5": "метагейм",
    "BackmenCat6": "ответственная-игра-за-антагониста",
    "BackmenCat7": "white-list-роли",
    "BackmenCat8": "нечестная-игра",
    "BackmenCat9": "erp-нет",
}


def xml_to_markdown(xml_path: Path, *, intro_only: bool = False) -> str:
    raw = xml_path.read_text(encoding="utf-8")
    # Guidebook uses custom tags inside <Document>text</Document>
    match = re.search(r"<Document>(.*)</Document>", raw, re.DOTALL)
    if not match:
        return raw.strip()

    text = match.group(1).strip()
    text = text.replace("\\n", "\n")

    # BBCode-style tags used in guidebook
    replacements = [
        (r"\[color=([^\]]+)\](.*?)\[/color\]", r'<span style="color:\1">\2</span>'),
        (r'\[bold\](.*?)\[/bold\]', r"**\1**"),
        (r'\[italic\](.*?)\[/italic\]', r"*\1*"),
        (r'\[textlink="([^"]+)" link="([^"]+)"\]', _replace_textlink),
        (r"\[bullet\]", "- "),
    ]
    for pattern, repl in replacements:
        if callable(repl):
            text = re.sub(pattern, repl, text, flags=re.DOTALL)
        else:
            text = re.sub(pattern, repl, text, flags=re.DOTALL)

    if intro_only:
        # Drop duplicate section bodies linked from the intro TOC.
        return text.strip()

    # Collapse excessive blank lines
    text = re.sub(r"\n{3,}", "\n\n", text)
    return text.strip()


def _replace_textlink(match: re.Match[str]) -> str:
    label = match.group(1)
    link = match.group(2)
    if link in CAT_ANCHORS:
        return f"[{label}](#{CAT_ANCHORS[link]})"
    return f"[{label}]({link})"


def export_rules() -> None:
    parts: list[str] = [
        "# 📜 Правила сервера BACKMEN",
        "",
        "> Источник: игровой Guidebook (`RulesBackmen`).",
        "> Актуальная версия на сервере может отличаться — при расхождении ориентируйтесь на игру.",
        "",
        "[← На главную](/ss14)",
        "",
    ]

    intro = xml_to_markdown(RULES_DIR / "RulesBackmen.xml", intro_only=True)
    parts.append(intro)
    parts.append("")

    for filename, title, _anchor in RULE_FILES[1:]:
        path = RULES_DIR / filename
        if not path.exists():
            continue
        body = xml_to_markdown(path)
        parts.append(f"## {title}")
        parts.append("")
        parts.append(body)
        parts.append("")

    parts.append("---")
    parts.append("")
    parts.append("См. также: [Корпоративный закон](/ss14) (раздел на вики), [Контрабанда](/ss14/contraband).")
    parts.append("")
    parts.append("[← На главную](/ss14)")

    out = OUT / "rules.md"
    out.write_text("\n".join(parts), encoding="utf-8")
    print(f"Wrote {out}")


def parse_yaml_blocks(paths: list[Path]) -> list[dict]:
    blocks: list[dict] = []
    current: dict | None = None
    in_components = False
    contraband: dict | None = None
    sprite: dict | None = None
    in_sprite = False
    in_layers = False

    for path in paths:
        try:
            lines = path.read_text(encoding="utf-8").splitlines()
        except OSError:
            continue

        for line in lines:
            if line.startswith("- type: entity"):
                if current is not None:
                    if contraband:
                        current["contraband"] = contraband
                    if sprite:
                        current["sprite"] = sprite
                    blocks.append(current)
                current = {"id": None, "abstract": False, "parents": [], "file": str(path)}
                contraband = None
                sprite = None
                in_components = False
                in_sprite = False
                in_layers = False
                continue

            if current is None:
                continue

            stripped = line.strip()
            if stripped.startswith("id:"):
                current["id"] = stripped.split(":", 1)[1].strip()
            elif stripped == "abstract: true":
                current["abstract"] = True
            elif stripped.startswith("parent:"):
                parent = stripped.split(":", 1)[1].strip()
                if parent.startswith("["):
                    current["parents"] = re.findall(r"\b[A-Za-z0-9]+\b", parent)
                else:
                    current["parents"] = [parent]
            elif stripped == "components:":
                in_components = True
            elif in_components and stripped.startswith("- type:"):
                component_type = stripped.split(":", 1)[1].strip()
                in_sprite = False
                in_layers = False
                if component_type == "Contraband":
                    contraband = {"severity": "Restricted", "departments": [], "jobs": []}
                elif component_type == "Sprite":
                    sprite = {"path": None, "state": None, "layer_state": None}
                    in_sprite = True
            elif contraband is not None and stripped.startswith("severity:"):
                contraband["severity"] = stripped.split(":", 1)[1].strip()
            elif contraband is not None and stripped.startswith("allowedDepartments:"):
                contraband["departments"] = bracket_values(stripped)
            elif contraband is not None and stripped.startswith("allowedJobs:"):
                contraband["jobs"] = bracket_values(stripped)
            elif in_sprite and sprite is not None and stripped.startswith("sprite:"):
                sprite["path"] = stripped.split(":", 1)[1].strip()
            elif in_sprite and sprite is not None and stripped.startswith("state:") and not in_layers:
                sprite["state"] = stripped.split(":", 1)[1].strip()
            elif in_sprite and sprite is not None and stripped == "layers:":
                in_layers = True
            elif (
                in_sprite
                and sprite is not None
                and in_layers
                and stripped.startswith("- state:")
                and sprite["layer_state"] is None
            ):
                sprite["layer_state"] = stripped.split(":", 1)[1].strip()

    if current is not None:
        if contraband:
            current["contraband"] = contraband
        if sprite:
            current["sprite"] = sprite
        blocks.append(current)

    return blocks


def bracket_values(line: str) -> list[str]:
    match = re.search(r"\[(.*)\]", line)
    if not match:
        return []
    return [part.strip() for part in match.group(1).split(",") if part.strip()]


def resolve_inherited(blocks: list[dict], entity_id: str, key: str) -> dict | None:
    by_id = {b["id"]: b for b in blocks if b.get("id")}

    def resolve(current_id: str, seen: set[str] | None = None) -> dict | None:
        if seen is None:
            seen = set()
        if current_id in seen or current_id not in by_id:
            return None
        seen.add(current_id)
        ent = by_id[current_id]
        if key in ent:
            return ent[key].copy()
        for parent in ent.get("parents", []):
            resolved = resolve(parent, seen)
            if resolved:
                return resolved
        return None

    return resolve(entity_id)


def resolve_contraband(blocks: list[dict]) -> dict[str, dict]:
    resolved: dict[str, dict] = {}
    for ent in blocks:
        if ent.get("abstract") or not ent.get("id"):
            continue
        contra = resolve_inherited(blocks, ent["id"], "contraband")
        if contra:
            resolved[ent["id"]] = contra
    return resolved


def pick_icon_state(sprite: dict, rsi_dir: Path) -> str | None:
    for state in (sprite.get("state"), sprite.get("layer_state"), "icon", "base"):
        if state and (rsi_dir / f"{state}.png").exists():
            return state

    meta_path = rsi_dir / "meta.json"
    if meta_path.exists():
        try:
            meta = json.loads(meta_path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            return None
        for entry in meta.get("states", []):
            name = entry["name"] if isinstance(entry, dict) else entry
            if (rsi_dir / f"{name}.png").exists():
                return name
    return None


def resolve_icon_path(blocks: list[dict], entity_id: str) -> Path | None:
    sprite = resolve_inherited(blocks, entity_id, "sprite")
    if not sprite or not sprite.get("path"):
        return None

    rsi_dir = TEXTURES / sprite["path"]
    if not rsi_dir.is_dir():
        return None

    state = pick_icon_state(sprite, rsi_dir)
    if not state:
        return None

    return rsi_dir / f"{state}.png"


def export_icon(source: Path, entity_id: str, icons_dir: Path) -> str | None:
    target = icons_dir / f"{entity_id}.png"
    if target.exists() and target.stat().st_mtime >= source.stat().st_mtime:
        return target.name
    shutil.copy2(source, target)
    return target.name


def icon_markdown(entity_id: str, icon_file: str | None) -> str:
    if not icon_file:
        return ""
    wiki_name = icon_file.lower()
    return (
        f'<img src="{ICON_WIKI_PATH}/{wiki_name}" '
        f'width="32" height="32" alt="{entity_id}" style="image-rendering:pixelated;">'
    )


def export_contraband(*, with_icons: bool = True) -> None:
    yaml_files = list(PROTOTYPES.rglob("*.yml"))
    blocks = parse_yaml_blocks(yaml_files)
    contraband_map = resolve_contraband(blocks)
    names = load_ftl_names()
    departments = load_department_names()

    icons_dir: Path | None = None
    icon_count = 0
    if with_icons:
        ICON_OUT.mkdir(parents=True, exist_ok=True)
        icons_dir = ICON_OUT

    grouped: dict[str, list[tuple[str, str, dict, str | None]]] = defaultdict(list)
    for ent_id, contra in sorted(contraband_map.items()):
        severity = contra.get("severity", "Restricted")
        display = names.get(ent_id, ent_id)
        icon_file: str | None = None
        if icons_dir is not None:
            source = resolve_icon_path(blocks, ent_id)
            if source is not None:
                icon_file = export_icon(source, ent_id, icons_dir)
                if icon_file:
                    icon_count += 1
        grouped[severity].append((ent_id, display, contra, icon_file))

    parts = [
        "# 🚫 Контрабанда",
        "",
        "> Автоматически собрано из прототипов игры (`ContrabandComponent`).",
        f"> Всего предметов: **{len(contraband_map)}**.",
    ]
    if with_icons:
        parts.append(f"> Иконок из игры: **{icon_count}**.")
    parts.extend(
        [
        "",
        "[← На главную](/ss14)",
        "",
        "## Уровни контрабанды",
        "",
        "| Уровень | Описание |",
        "|---------|----------|",
        "| **Restricted** | Разрешено только указанным отделам/должностям |",
        "| **Minor** | Мелкая контрабанда (самодельное оружие и т.п.) |",
        "| **Major** | Крупная контрабанда |",
        "| **GrandTheft** | Предметы гранд-кражи (диск ядра, шмот капитана…) |",
        "| **HighlyIllegal** | Крайне незаконная |",
        "| **Syndicate** | Синдикат |",
        "| **Magical** | Магическая |",
        "| **Psionic** | Псионическая |",
        "",
        "> В игре при осмотре предмета отображается точный статус. СБ может конфисковать контрабанду при обыске.",
        "",
        ]
    )

    for severity in SEVERITY_ORDER:
        items = grouped.get(severity)
        if not items:
            continue
        title = SEVERITY_TITLES.get(severity, severity)
        parts.append(f"## {title} ({len(items)})")
        parts.append("")
        if with_icons:
            parts.append("| | Предмет | ID | Ограничения |")
            parts.append("|:--:|---------|-----|-------------|")
        else:
            parts.append("| Предмет | ID | Ограничения |")
            parts.append("|---------|-----|-------------|")
        for ent_id, display, contra, icon_file in sorted(items, key=lambda x: x[1].lower()):
            depts = ", ".join(departments.get(d, d) for d in contra.get("departments", []))
            jobs = ", ".join(contra.get("jobs", []))
            restriction = depts or jobs or "—"
            if depts and jobs:
                restriction = f"{depts}; должности: {jobs}"
            if with_icons:
                parts.append(
                    f"| {icon_markdown(ent_id, icon_file)} | {display} | `{ent_id}` | {restriction} |"
                )
            else:
                parts.append(f"| {display} | `{ent_id}` | {restriction} |")
        parts.append("")

    parts.append("---")
    parts.append("")
    parts.append("Пересобрать: `python Tools/export_wiki_from_game.py`")
    parts.append("")
    parts.append("[← На главную](/ss14)")

    out = OUT / "contraband.md"
    out.write_text("\n".join(parts), encoding="utf-8")
    icon_msg = f", {icon_count} icons" if with_icons else ""
    print(f"Wrote {out} ({len(contraband_map)} items{icon_msg})")


def main() -> None:
    import argparse

    parser = argparse.ArgumentParser(description="Export game data to Wiki.js markdown.")
    parser.add_argument(
        "--no-icons",
        action="store_true",
        help="Skip copying item icons for the contraband page.",
    )
    args = parser.parse_args()

    OUT.mkdir(parents=True, exist_ok=True)
    export_rules()
    export_contraband(with_icons=not args.no_icons)


if __name__ == "__main__":
    main()
