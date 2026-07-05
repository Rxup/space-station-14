"""Generate socks displacement maps from species leg/foot body parts."""
from __future__ import annotations

import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
PARTS_RSI = ROOT / "Resources/Textures/Mobs/Species"
DISPLACEMENT_RSI = ROOT / "Resources/Textures/Backmen/Mobs/Species/displacement.rsi"

# 2x2 layout used by existing displacement RSIs (S, N / E, W).
DIR_OFFSETS = [(0, 0), (32, 0), (0, 32), (32, 32)]
ALPHA_THRESHOLD = 20
NEUTRAL_RGB = (128, 128, 128)
THIGH_EXTENSION = 10
DILATE_RADIUS = 1

SPECIES = (
    ("Human", "socks-human"),
    ("Moth", "socks-moth"),
    ("Vulpkanin", "socks-vulpkanin"),
    ("Vox", "socks-vox"),
)


def load_quad(path: Path, offset: tuple[int, int]) -> Image.Image:
    ox, oy = offset
    return Image.open(path).convert("RGBA").crop((ox, oy, ox + 32, oy + 32))


def collect_opaque_pixels(image: Image.Image) -> set[tuple[int, int]]:
    pixels: set[tuple[int, int]] = set()
    for y in range(image.height):
        for x in range(image.width):
            if image.getpixel((x, y))[3] > ALPHA_THRESHOLD:
                pixels.add((x, y))
    return pixels


def dilate(pixels: set[tuple[int, int]], radius: int) -> set[tuple[int, int]]:
    if radius <= 0:
        return set(pixels)

    dilated = set(pixels)
    for x, y in pixels:
        for dx in range(-radius, radius + 1):
            for dy in range(-radius, radius + 1):
                nx, ny = x + dx, y + dy
                if 0 <= nx < 32 and 0 <= ny < 32:
                    dilated.add((nx, ny))
    return dilated


def remove_leg_gap_columns(
    visible: set[tuple[int, int]],
    leg_parts: tuple[set[tuple[int, int]], set[tuple[int, int]]],
) -> set[tuple[int, int]]:
    legs = [leg for leg in leg_parts if leg]
    if len(legs) < 2:
        return visible

    original_legs = legs[0] | legs[1]
    x_ranges = sorted((min(x for x, _ in leg), max(x for x, _ in leg)) for leg in legs)

    if x_ranges[1][0] <= x_ranges[0][1]:
        return visible

    gap_columns = set(range(x_ranges[0][1] + 1, x_ranges[1][0]))
    return {
        (x, y)
        for x, y in visible
        if x not in gap_columns or (x, y) in original_legs
    }


def build_visible_pixels(parts_dir: Path, offset: tuple[int, int]) -> set[tuple[int, int]]:
    leg_parts = (
        collect_opaque_pixels(load_quad(parts_dir / "l_leg.png", offset)),
        collect_opaque_pixels(load_quad(parts_dir / "r_leg.png", offset)),
    )
    feet: set[tuple[int, int]] = set()

    for part in ("l_foot", "r_foot"):
        feet |= collect_opaque_pixels(load_quad(parts_dir / f"{part}.png", offset))

    visible: set[tuple[int, int]] = set()
    for leg in leg_parts:
        visible |= dilate(leg, DILATE_RADIUS)

    visible -= feet
    visible = remove_leg_gap_columns(visible, leg_parts)

    if feet:
        min_foot_y = min(y for _, y in feet)
        visible = {(x, y) for x, y in visible if y < min_foot_y - 1}

    for leg in leg_parts:
        if not leg:
            continue

        min_leg_y = min(y for _, y in leg)
        leg_columns = {x for x, _ in leg}
        for y in range(max(0, min_leg_y - THIGH_EXTENSION), min_leg_y):
            for x in leg_columns:
                visible.add((x, y))

    return remove_leg_gap_columns(visible, leg_parts)


def render_direction(parts_dir: Path, offset: tuple[int, int]) -> Image.Image:
    visible = build_visible_pixels(parts_dir, offset)
    image = Image.new("RGBA", (32, 32), (0, 0, 0, 0))

    for x, y in visible:
        image.putpixel((x, y), (*NEUTRAL_RGB, 255))

    return image


def build_socks_displacement(species: str) -> Image.Image:
    parts_dir = PARTS_RSI / species / "parts.rsi"
    if not parts_dir.is_dir():
        raise FileNotFoundError(parts_dir)

    sheet = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    for offset in DIR_OFFSETS:
        direction = render_direction(parts_dir, offset)
        sheet.paste(direction, offset)
    return sheet


def sync_meta_json() -> None:
    meta_path = DISPLACEMENT_RSI / "meta.json"
    meta = {
        "version": 1,
        "license": "CC-BY-SA-3.0",
        "copyright": "Generated from species leg parts",
        "size": {"x": 32, "y": 32},
        "load": {"srgb": False},
        "states": [{"name": state_name, "directions": 4} for _, state_name in SPECIES],
    }
    meta_path.write_text(json.dumps(meta, indent=4) + "\n", encoding="utf-8")


def write_socks_displacement(species: str, state_name: str) -> None:
    DISPLACEMENT_RSI.mkdir(parents=True, exist_ok=True)
    sheet = build_socks_displacement(species)
    sheet.save(DISPLACEMENT_RSI / f"{state_name}.png")
    print(f"Wrote {DISPLACEMENT_RSI / f'{state_name}.png'}")


def main() -> None:
    for species, state_name in SPECIES:
        write_socks_displacement(species, state_name)

    sync_meta_json()
    print(f"Updated {DISPLACEMENT_RSI / 'meta.json'}")


if __name__ == "__main__":
    main()
