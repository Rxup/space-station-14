"""Generate door open-unlit overlays from each RSI's own opening_unlit animation."""
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

from PIL import Image

DOORS_ROOT = Path("Resources/Textures/Structures/Doors")

DEFAULT_RSIS = [
    DOORS_ROOT / "Airlocks/Standard/xenoborg.rsi",
    DOORS_ROOT / "Airlocks/Standard/xeno.rsi",
    DOORS_ROOT / "Airlocks/Glass/xeno.rsi",
    DOORS_ROOT / "Airlocks/Standard/shuttle_xenoborg.rsi",
    DOORS_ROOT / "Airlocks/Standard/firelock.rsi",
    DOORS_ROOT / "Airlocks/Glass/firelock.rsi",
    DOORS_ROOT / "Airlocks/Standard/hatch_syndicate.rsi",
]


def is_glow_pixel(r: int, g: int, b: int, a: int, threshold: int = 70) -> bool:
    return a > 20 and max(r, g, b) > threshold


def extract_frame(path: Path, frame_size: tuple[int, int], frame_index: int) -> Image.Image:
    img = Image.open(path).convert("RGBA")
    fw, fh = frame_size
    cols = img.width // fw
    if cols <= 0:
        return img
    x = (frame_index % cols) * fw
    y = (frame_index // cols) * fh
    return img.crop((x, y, x + fw, y + fh))


def get_state_config(meta: dict, name: str) -> dict | None:
    for state in meta["states"]:
        if state["name"] == name:
            return state
    return None


def directional_offsets(directions: int, frame_size: tuple[int, int]) -> list[tuple[int, int]]:
    fw, fh = frame_size
    if directions == 4:
        return [(0, 0), (fw, 0), (0, fh), (fw, fh)]
    if directions == 8:
        return [(0, 0), (fw, 0), (0, fh), (fw, fh), (fw * 2, 0), (0, fh * 2), (fw * 2, fh), (fw, fh * 2)]
    return [(0, 0)]


def split_directional_sheet(sheet: Image.Image, directions: int, frame_size: tuple[int, int]) -> list[Image.Image]:
    if directions <= 1:
        return [sheet.convert("RGBA")]
    offsets = directional_offsets(directions, frame_size)
    return [sheet.crop((x, y, x + frame_size[0], y + frame_size[1])) for x, y in offsets]


def assemble_directional(frames: list[Image.Image], directions: int, frame_size: tuple[int, int]) -> Image.Image:
    if directions <= 1:
        return frames[0]
    offsets = directional_offsets(directions, frame_size)
    fw, fh = frame_size
    cols = 2 if directions in (4, 8) else 1
    rows = 2 if directions == 4 else (3 if directions == 8 else 1)
    out = Image.new("RGBA", (fw * cols, fh * rows), (0, 0, 0, 0))
    for frame, (x, y) in zip(frames, offsets):
        out.paste(frame, (x, y))
    return out


def extract_last_opening_frames(
    meta: dict,
    opening_path: Path,
    frame_size: tuple[int, int],
) -> tuple[list[Image.Image], int]:
    state = get_state_config(meta, "opening_unlit")
    if state is None:
        raise ValueError("opening_unlit missing from meta")

    directions = state.get("directions", 1)
    delays = state.get("delays", [[1.0]])
    frames_per_dir = len(delays[0])

    opening = Image.open(opening_path).convert("RGBA")
    fw, fh = frame_size
    cols = max(1, opening.width // fw)

    frames: list[Image.Image] = []
    for direction in range(directions):
        index = direction * frames_per_dir + (frames_per_dir - 1)
        x = (index % cols) * fw
        y = (index // cols) * fh
        frames.append(opening.crop((x, y, x + fw, y + fh)))
    return frames, directions


def extract_last_opening_frame(meta_path: Path, opening_path: Path, frame_size: tuple[int, int]) -> Image.Image:
    meta = json.loads(meta_path.read_text(encoding="utf-8"))
    frames, directions = extract_last_opening_frames(meta, opening_path, frame_size)
    return assemble_directional(frames, directions, frame_size)


def recolor_glows(
    source: Image.Image,
    palette_from: Image.Image,
    palette_to: Image.Image,
) -> Image.Image:
    """Recolor glowing pixels in source using nearest glow color mapping closed->open variants."""
    src = source.convert("RGBA")
    pf = palette_from.convert("RGBA")
    pt = palette_to.convert("RGBA")

    from_glows: list[tuple[int, int, int, int, int, int, int]] = []
    to_glows: list[tuple[int, int, int, int, int, int, int]] = []

    for y in range(pf.height):
        for x in range(pf.width):
            r, g, b, a = pf.getpixel((x, y))
            if is_glow_pixel(r, g, b, a):
                from_glows.append((x, y, r, g, b, a, 0))

    for y in range(pt.height):
        for x in range(pt.width):
            r, g, b, a = pt.getpixel((x, y))
            if is_glow_pixel(r, g, b, a):
                to_glows.append((x, y, r, g, b, a, 0))

    if not from_glows or not to_glows:
        return src

    out = Image.new("RGBA", src.size, (0, 0, 0, 0))
    src_glows: list[tuple[int, int, int, int, int, int, int]] = []

    for y in range(src.height):
        for x in range(src.width):
            r, g, b, a = src.getpixel((x, y))
            if is_glow_pixel(r, g, b, a):
                src_glows.append((x, y, r, g, b, a, 0))

    if not src_glows:
        return src

    # Match closed glow clusters to open glow clusters by sorted relative position
    def norm(points: list[tuple[int, int, int, int, int, int, int]], w: int, h: int):
        cx = sum(p[0] for p in points) / len(points)
        cy = sum(p[1] for p in points) / len(points)
        return [(p[0] - cx, p[1] - cy, p[2], p[3], p[4], p[5]) for p in points]

    # Simple per-pixel nearest closed glow -> target color
    to_by_pos = {(g[0], g[1]): (g[2], g[3], g[4], g[5]) for g in to_glows}
    from_by_pos = {(g[0], g[1]): (g[2], g[3], g[4], g[5]) for g in from_glows}

    for sx, sy, sr, sg, sb, sa, _ in src_glows:
        # find nearest from glow pixel
        best = min(from_glows, key=lambda g: (g[0] - sx) ** 2 + (g[1] - sy) ** 2)
        fx, fy = best[0], best[1]
        if (fx, fy) in to_by_pos:
            tr, tg, tb, ta = to_by_pos[(fx, fy)]
            out.putpixel((sx, sy), (tr, tg, tb, ta))
        else:
            out.putpixel((sx, sy), (sr, sg, sb, sa))

    return out


def meta_indent(meta_path: Path) -> int:
    for line in meta_path.read_text(encoding="utf-8").splitlines():
        match = re.match(r"^(\s+)\"version\"", line)
        if match:
            return len(match.group(1))
    return 4


def ensure_meta_states(meta_path: Path, needed: list[str]) -> bool:
    meta = json.loads(meta_path.read_text(encoding="utf-8"))
    states = {s["name"] for s in meta["states"]}
    changed = False
    insert_after = {
        "open_unlit": "closed_unlit",
        "bolted_open_unlit": "bolted_unlit",
        "emergency_open_unlit": "emergency_unlit",
    }
    for state in needed:
        if state in states:
            continue
        anchor = insert_after[state]
        idx = next(i for i, s in enumerate(meta["states"]) if s["name"] == anchor)
        meta["states"].insert(idx + 1, {"name": state})
        states.add(state)
        changed = True
    if changed:
        indent = meta_indent(meta_path)
        meta_path.write_text(json.dumps(meta, indent=indent) + "\n", encoding="utf-8")
    return changed


def process_rsi(rsi_dir: Path) -> list[str]:
    meta_path = rsi_dir / "meta.json"
    if not meta_path.exists():
        return []

    meta = json.loads(meta_path.read_text(encoding="utf-8"))
    states = {s["name"] for s in meta["states"]}
    size = meta.get("size", {"x": 32, "y": 32})
    frame_size = (size["x"], size["y"])

    if "opening_unlit" not in states or "closed_unlit" not in states:
        return []

    needed = []
    if "open_unlit" in states or True:
        needed.append("open_unlit")
    if "bolted_unlit" in states:
        needed.append("bolted_open_unlit")
    if "emergency_unlit" in states:
        needed.append("emergency_open_unlit")

    ensure_meta_states(meta_path, needed)

    actions: list[str] = []
    opening = rsi_dir / "opening_unlit.png"
    if not opening.exists():
        return actions

    closed_unlit = Image.open(rsi_dir / "closed_unlit.png")
    opening_state = get_state_config(meta, "opening_unlit") or {}
    directions = opening_state.get("directions", 1)

    open_frames, _ = extract_last_opening_frames(meta, opening, frame_size)
    closed_frames = split_directional_sheet(closed_unlit, directions, frame_size)
    recolored_open = [
        recolor_glows(open_frame, closed_frame, closed_frame)
        for open_frame, closed_frame in zip(open_frames, closed_frames)
    ]
    open_unlit = assemble_directional(recolored_open, directions, frame_size)
    open_unlit.save(rsi_dir / "open_unlit.png")
    actions.append("open_unlit")

    if (rsi_dir / "bolted_unlit.png").exists() and "bolted_open_unlit" in needed:
        bolted = Image.open(rsi_dir / "bolted_unlit.png")
        bolted_frames = split_directional_sheet(bolted, directions, frame_size)
        bolted_open = assemble_directional(
            [
                recolor_glows(open_frame, closed_frame, bolted_frame)
                for open_frame, closed_frame, bolted_frame in zip(recolored_open, closed_frames, bolted_frames)
            ],
            directions,
            frame_size,
        )
        bolted_open.save(rsi_dir / "bolted_open_unlit.png")
        actions.append("bolted_open_unlit")

    if (rsi_dir / "emergency_unlit.png").exists() and "emergency_open_unlit" in needed:
        emergency = Image.open(rsi_dir / "emergency_unlit.png")
        emergency_frames = split_directional_sheet(emergency, directions, frame_size)
        assemble_directional(
            [
                recolor_glows(open_frame, closed_frame, emergency_frame)
                for open_frame, closed_frame, emergency_frame in zip(
                    recolored_open, closed_frames, emergency_frames
                )
            ],
            directions,
            frame_size,
        ).save(rsi_dir / "emergency_open_unlit.png")
        actions.append("emergency_open_unlit")

    return actions


def iter_rsi_dirs(explicit: list[Path] | None) -> list[Path]:
    if explicit:
        return explicit
    dirs: list[Path] = []
    for meta_path in sorted(DOORS_ROOT.rglob("meta.json")):
        rsi_dir = meta_path.parent
        text = meta_path.read_text(encoding="utf-8")
        if "closed_unlit" not in text:
            continue
        if not (rsi_dir / "opening_unlit.png").exists():
            continue
        dirs.append(rsi_dir)
    return dirs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "rsi",
        nargs="*",
        help="Specific RSI directories to process (default: curated resprite set)",
    )
    parser.add_argument(
        "--all",
        action="store_true",
        help="Process every door RSI with opening_unlit + closed_unlit",
    )
    args = parser.parse_args()

    if args.all:
        rsi_dirs = iter_rsi_dirs(None)
    elif args.rsi:
        rsi_dirs = [Path(p) for p in args.rsi]
    else:
        rsi_dirs = DEFAULT_RSIS

    processed = 0
    for rsi_dir in rsi_dirs:
        if not (rsi_dir / "meta.json").exists():
            print(f"skip (no meta): {rsi_dir}")
            continue
        actions = process_rsi(rsi_dir)
        if actions:
            try:
                rel = rsi_dir.relative_to(DOORS_ROOT)
            except ValueError:
                rel = rsi_dir
            print(f"{rel}: {', '.join(actions)}")
            processed += 1
    print(f"done, {processed} RSIs")


if __name__ == "__main__":
    main()
