#!/usr/bin/env python3
"""Rebuilds the app icon assets from the two SVG sources in this folder.

Outputs (into src/NxtHUD/Assets):
  NxtHUD.ico  multi-size icon: 16-64 px as 32-bit bitmaps, 256 px as PNG
  Logo.png       256 px logo used inside the settings window

Requires: rsvg-convert (librsvg) and Pillow.   Usage: python3 build_icon.py
"""
import io
import struct
import subprocess
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
ASSETS = HERE.parent.parent / "src" / "NxtHUD" / "Assets"

# Small sizes use the simplified drawing so the tray icon stays legible.
SMALL_SIZES = [16, 20, 24, 32, 40]
LARGE_SIZES = [48, 64, 256]


def render(svg: Path, size: int) -> Image.Image:
    png = subprocess.run(
        ["rsvg-convert", "-w", str(size), "-h", str(size), str(svg)],
        check=True,
        capture_output=True,
    ).stdout
    return Image.open(io.BytesIO(png)).convert("RGBA")


def bmp_frame(image: Image.Image) -> bytes:
    """A 32-bit icon bitmap: BITMAPINFOHEADER, bottom-up BGRA pixels, then a 1-bit AND mask."""
    width, height = image.size
    header = struct.pack("<IiiHHIIiiII", 40, width, height * 2, 1, 32, 0, 0, 0, 0, 0, 0)
    pixels = image.transpose(Image.FLIP_TOP_BOTTOM).tobytes("raw", "BGRA")
    mask_row = b"\x00" * (((width + 31) // 32) * 4)
    return header + pixels + mask_row * height


def png_frame(image: Image.Image) -> bytes:
    buffer = io.BytesIO()
    image.save(buffer, format="PNG", optimize=True)
    return buffer.getvalue()


def main() -> None:
    ASSETS.mkdir(parents=True, exist_ok=True)

    frames = []
    for size in SMALL_SIZES:
        frames.append((size, bmp_frame(render(HERE / "icon-small.svg", size))))
    for size in LARGE_SIZES:
        image = render(HERE / "icon-large.svg", size)
        frames.append((size, png_frame(image) if size >= 256 else bmp_frame(image)))

    directory = struct.pack("<HHH", 0, 1, len(frames))
    offset = 6 + 16 * len(frames)
    body = b""
    for size, data in frames:
        dimension = 0 if size >= 256 else size  # 0 means 256 in an icon directory
        directory += struct.pack("<BBBBHHII", dimension, dimension, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
        body += data

    (ASSETS / "NxtHUD.ico").write_bytes(directory + body)
    render(HERE / "icon-large.svg", 256).save(ASSETS / "Logo.png", optimize=True)
    print(f"Wrote {ASSETS / 'NxtHUD.ico'} ({len(frames)} sizes) and Logo.png")


if __name__ == "__main__":
    main()
