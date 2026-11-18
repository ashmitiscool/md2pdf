"""Generate the multi-resolution Windows icon (app.ico) for md2pdf."""

from __future__ import annotations

from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ICON_SIZES = [(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]


def find_serif_font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    candidates = [
        r"C:\Windows\Fonts\timesbi.ttf",
        r"C:\Windows\Fonts\timesi.ttf",
        r"C:\Windows\Fonts\timesbd.ttf",
        r"C:\Windows\Fonts\georgia.ttf",
        r"C:\Windows\Fonts\seguisb.ttf",
    ]
    for path in candidates:
        if Path(path).is_file():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def build_icon_image(size: int = 256) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    pad = int(size * 0.05)
    radius = int(size * 0.22)

    # Outer border glow / bevel
    draw.rounded_rectangle(
        [pad, pad, size - pad - 1, size - pad - 1],
        radius=radius,
        fill=(23, 37, 84, 255),      # #172554
        outline=(30, 64, 175, 255),  # #1E40AF
        width=max(2, int(size * 0.035)),
    )

    # Inner subtle top highlight
    inset = pad + max(3, int(size * 0.04))
    draw.rounded_rectangle(
        [inset, inset, size - inset - 1, size - inset - 1],
        radius=max(4, radius - int(size * 0.04)),
        outline=(59, 130, 246, 75),
        width=max(1, int(size * 0.012)),
    )

    # Sigma emblem
    font = find_serif_font(int(size * 0.62))
    glyph = "\u03a3"
    bbox = draw.textbbox((0, 0), glyph, font=font)
    tw = bbox[2] - bbox[0]
    th = bbox[3] - bbox[1]
    tx = (size - tw) / 2 - bbox[0]
    ty = (size - th) / 2 - bbox[1] - size * 0.02

    # Subtle shadow
    draw.text((tx, ty + size * 0.02), glyph, font=font, fill=(9, 14, 34, 180))
    draw.text((tx, ty), glyph, font=font, fill=(147, 197, 253, 255))  # #93C5FD

    # Small crisp PDF accent dot / badge in bottom-right
    badge_r = int(size * 0.14)
    bx = size - pad - badge_r - int(size * 0.06)
    by = size - pad - badge_r - int(size * 0.06)
    draw.ellipse(
        [bx - badge_r, by - badge_r, bx + badge_r, by + badge_r],
        fill=(59, 130, 246, 255),    # #3B82F6
        outline=(11, 13, 17, 255),
        width=max(1, int(size * 0.018)),
    )
    pdf_font = find_serif_font(int(size * 0.11))
    p_glyph = "P"
    pb = draw.textbbox((0, 0), p_glyph, font=pdf_font)
    pw = pb[2] - pb[0]
    ph = pb[3] - pb[1]
    draw.text(
        (bx - pw / 2 - pb[0], by - ph / 2 - pb[1]),
        p_glyph,
        font=pdf_font,
        fill=(255, 255, 255, 255),
    )

    return img


def main() -> None:
    out_path = Path(__file__).resolve().parent / "app.ico"
    base = build_icon_image(256)
    base.save(out_path, format="ICO", sizes=ICON_SIZES)
    print(f"Generated icon: {out_path} ({out_path.stat().st_size:,} bytes)")


if __name__ == "__main__":
    main()
