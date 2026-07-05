"""Generate canister pressure indicators from can-o-template.png with gradients."""
from __future__ import annotations

from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
RSI = ROOT / "Resources/Textures/Structures/Storage/canister.rsi"
TEMPLATE = Path(__file__).resolve().parent / "can-o-template.png"

# Template is a full (100%) gauge arc; each stage keeps this fraction of pixels.
FILL_FRACTION = {
    "can-o0-off": 1.0,
    "can-o0-on": 1.0,
    "can-o1": 0.25,
    "can-o2": 0.50,
    "can-o3": 1.0,
}


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def lerp_rgb(
    top: tuple[int, int, int],
    bottom: tuple[int, int, int],
    t: float,
) -> tuple[int, int, int]:
    return (
        int(lerp(top[0], bottom[0], t)),
        int(lerp(top[1], bottom[1], t)),
        int(lerp(top[2], bottom[2], t)),
    )


def template_pixels(template: Image.Image) -> list[tuple[int, int, int]]:
    template = template.convert("RGBA")
    pixels: list[tuple[int, int, int]] = []
    for y in range(template.height):
        for x in range(template.width):
            _, _, _, a = template.getpixel((x, y))
            if a > 0:
                pixels.append((x, y, a))
    return pixels


def fill_order(pixels: list[tuple[int, int, int]]) -> list[tuple[int, int, int]]:
    """Fill gauge left-to-right, bottom row before top within each column."""
    return sorted(pixels, key=lambda p: (p[0], -p[1]))


def crop_template(template: Image.Image, fill_fraction: float) -> Image.Image:
    """Keep the first `fill_fraction` of arc pixels (100% template = full arc)."""
    template = template.convert("RGBA")
    ordered = fill_order(template_pixels(template))
    if not ordered or fill_fraction <= 0:
        return Image.new("RGBA", template.size, (0, 0, 0, 0))

    count = max(1, round(len(ordered) * fill_fraction)) if fill_fraction < 1 else len(ordered)
    if fill_fraction >= 1:
        count = len(ordered)

    out = Image.new("RGBA", template.size, (0, 0, 0, 0))
    for x, y, a in ordered[:count]:
        out.putpixel((x, y), (255, 255, 255, a))
    return out


def gradient_at(
    x: int,
    y: int,
    bbox: tuple[int, int, int, int],
    top: tuple[int, int, int],
    bottom: tuple[int, int, int],
    *,
    horizontal_mix: float = 0.35,
) -> tuple[int, int, int]:
    """Blend vertical and slight horizontal gradients for a soft gauge look."""
    x0, y0, x1, y1 = bbox
    v_t = (y - y0) / max(1, y1 - y0)
    h_t = (x - x0) / max(1, x1 - x0)
    vertical = lerp_rgb(top, bottom, v_t)
    center_bias = 1.0 - abs(h_t - 0.5) * 2 * horizontal_mix
    return tuple(min(255, int(c * center_bias)) for c in vertical)


def render_from_template(
    template: Image.Image,
    top: tuple[int, int, int],
    bottom: tuple[int, int, int],
    *,
    alpha: int = 255,
    intensity: float = 1.0,
    glow: bool = True,
) -> Image.Image:
    template = template.convert("RGBA")
    out = Image.new("RGBA", template.size, (0, 0, 0, 0))
    bbox = template.getbbox()
    if bbox is None:
        return out

    mask_pixels: list[tuple[int, int, int]] = []
    for y in range(template.height):
        for x in range(template.width):
            _, _, _, a = template.getpixel((x, y))
            if a > 0:
                mask_pixels.append((x, y, a))

    if glow:
        glow_layer = Image.new("RGBA", template.size, (0, 0, 0, 0))
        for x, y, a in mask_pixels:
            color = gradient_at(x, y, bbox, top, bottom)
            bright = tuple(min(255, int(c * 1.15)) for c in color)
            for dx, dy in (
                (-1, 0),
                (1, 0),
                (0, -1),
                (0, 1),
                (-1, -1),
                (1, -1),
                (-1, 1),
                (1, 1),
            ):
                nx, ny = x + dx, y + dy
                if 0 <= nx < template.width and 0 <= ny < template.height and template.getpixel((nx, ny))[3] > 0:
                    continue
                glow_layer.putpixel(
                    (nx, ny),
                    bright + (min(255, int(a * 0.35 * intensity)),),
                )
        out = Image.alpha_composite(out, glow_layer)

    for x, y, a in mask_pixels:
        color = gradient_at(x, y, bbox, top, bottom)
        scaled = tuple(min(255, int(c * intensity)) for c in color)
        out.putpixel((x, y), scaled + (min(255, int(a * alpha / 255)),))

    return out


def save_sheet(path: Path, frames: list[Image.Image]) -> None:
    if len(frames) == 1:
        frames[0].save(path)
        return
    w, h = frames[0].size
    sheet = Image.new("RGBA", (w * len(frames), h), (0, 0, 0, 0))
    for index, frame in enumerate(frames):
        sheet.paste(frame, (index * w, 0))
    sheet.save(path)


def main() -> None:
    if not TEMPLATE.exists():
        raise SystemExit(f"Missing template: {TEMPLATE}")

    full_template = Image.open(TEMPLATE)

    presets = {
        "can-o0": (
            (
                (90, 45, 45),
                (45, 18, 18),
            ),
            (
                (255, 110, 110),
                (190, 35, 35),
            ),
        ),
        "can-o1": (
            (255, 130, 130),
            (175, 25, 25),
        ),
        "can-o2": (
            (255, 245, 140),
            (205, 145, 25),
        ),
        "can-o3": (
            (150, 255, 170),
            (35, 175, 55),
        ),
    }

    off_top, off_bottom = presets["can-o0"][0]
    on_top, on_bottom = presets["can-o0"][1]
    off_frame = render_from_template(
        crop_template(full_template, FILL_FRACTION["can-o0-off"]),
        off_top,
        off_bottom,
        alpha=170,
        intensity=0.75,
        glow=False,
    )
    on_frame = render_from_template(
        crop_template(full_template, FILL_FRACTION["can-o0-on"]),
        on_top,
        on_bottom,
        alpha=255,
        intensity=1.0,
        glow=True,
    )
    save_sheet(RSI / "can-o0.png", [off_frame, on_frame])

    stage_keys = {
        "can-o1": "can-o1",
        "can-o2": "can-o2",
        "can-o3": "can-o3",
    }
    for name, key in stage_keys.items():
        top, bottom = presets[name]
        mask = crop_template(full_template, FILL_FRACTION[key])
        frame = render_from_template(mask, top, bottom)
        save_sheet(RSI / f"{name}.png", [frame])

    total = len(template_pixels(full_template))
    print("wrote can-o0..can-o3 from can-o-template.png")
    for key, frac in FILL_FRACTION.items():
        if frac <= 0:
            px = 0
        elif frac >= 1:
            px = total
        else:
            px = max(1, round(total * frac))
        print(f"  {key}: {frac:.0%} ({px}/{total} px)")


if __name__ == "__main__":
    main()
