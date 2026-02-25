#!/usr/bin/env python3
"""assemble-strip.py — Assemble individual comic panels into a strip layout.

Takes generated panel images and composites them into a comic strip with:
- Panel borders
- Optional gutters (spacing between panels)
- Optional speech bubbles
- Halftone post-processing filter

Usage:
    # 3-panel horizontal strip
    python assemble-strip.py \
        --panels panel_1.png panel_2.png panel_3.png \
        --layout 1x3 \
        --output comic_strip.png

    # 2x3 grid layout with speech bubbles
    python assemble-strip.py \
        --panels p1.png p2.png p3.png p4.png p5.png p6.png \
        --layout 2x3 \
        --output comic_page.png \
        --bubbles bubbles.yaml

    # With halftone filter
    python assemble-strip.py \
        --panels panel_1.png panel_2.png panel_3.png \
        --layout 1x3 \
        --output comic_strip.png \
        --halftone
"""

import argparse
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont


def parse_args():
    parser = argparse.ArgumentParser(
        description="Assemble comic panels into strip layouts"
    )
    parser.add_argument(
        "--panels",
        nargs="+",
        required=True,
        help="Paths to panel image files (in reading order)",
    )
    parser.add_argument(
        "--layout",
        type=str,
        default="1x3",
        help="Grid layout as ROWSxCOLS (default: 1x3)",
    )
    parser.add_argument(
        "--output",
        type=str,
        default="comic_strip.png",
        help="Output file path (default: comic_strip.png)",
    )
    parser.add_argument(
        "--gutter",
        type=int,
        default=12,
        help="Spacing between panels in pixels (default: 12)",
    )
    parser.add_argument(
        "--border",
        type=int,
        default=3,
        help="Panel border thickness in pixels (default: 3)",
    )
    parser.add_argument(
        "--margin",
        type=int,
        default=20,
        help="Outer margin in pixels (default: 20)",
    )
    parser.add_argument(
        "--bg-color",
        type=str,
        default="white",
        help="Background color (default: white)",
    )
    parser.add_argument(
        "--border-color",
        type=str,
        default="black",
        help="Panel border color (default: black)",
    )
    parser.add_argument(
        "--halftone",
        action="store_true",
        help="Apply halftone dot shading effect to panels",
    )
    parser.add_argument(
        "--halftone-size",
        type=int,
        default=4,
        help="Halftone dot size (default: 4)",
    )
    parser.add_argument(
        "--bubbles",
        type=str,
        default=None,
        help="Path to YAML file defining speech bubbles",
    )
    parser.add_argument(
        "--panel-size",
        type=str,
        default=None,
        help="Force panel size as WIDTHxHEIGHT (default: use input sizes)",
    )
    return parser.parse_args()


def apply_halftone(image: Image.Image, dot_size: int = 4) -> Image.Image:
    """Apply a halftone dot pattern to simulate comic print shading.

    Converts the image to grayscale, then replaces continuous tones with
    dots whose size varies with the local brightness.
    """
    gray = image.convert("L")
    arr = np.array(gray, dtype=np.float32)

    width, height = image.size
    result = Image.new("L", (width, height), 255)
    draw = ImageDraw.Draw(result)

    step = dot_size * 2
    for y in range(0, height, step):
        for x in range(0, width, step):
            # Sample the average brightness in this cell
            cell = arr[y : y + step, x : x + step]
            if cell.size == 0:
                continue
            brightness = np.mean(cell) / 255.0

            # Darker areas get larger dots
            radius = int((1.0 - brightness) * dot_size)
            if radius > 0:
                cx = x + step // 2
                cy = y + step // 2
                draw.ellipse(
                    [cx - radius, cy - radius, cx + radius, cy + radius],
                    fill=0,
                )

    return result.convert("RGB")


def draw_speech_bubble(
    draw: ImageDraw.Draw,
    x: int,
    y: int,
    text: str,
    max_width: int = 200,
    font_size: int = 14,
):
    """Draw a speech bubble with text at the given position.

    Creates an elliptical bubble with a small triangular tail pointing
    downward.
    """
    try:
        font = ImageFont.truetype("arial.ttf", font_size)
    except OSError:
        font = ImageFont.load_default()

    # Measure text
    lines = []
    words = text.split()
    current_line = ""
    for word in words:
        test = f"{current_line} {word}".strip()
        bbox = draw.textbbox((0, 0), test, font=font)
        if bbox[2] - bbox[0] > max_width - 20:
            if current_line:
                lines.append(current_line)
            current_line = word
        else:
            current_line = test
    if current_line:
        lines.append(current_line)

    # Calculate bubble dimensions
    line_height = font_size + 4
    text_height = len(lines) * line_height
    text_width = max_width
    padding = 12

    bx = x - text_width // 2
    by = y - text_height - padding * 2
    bw = text_width + padding * 2
    bh = text_height + padding * 2

    # Draw bubble background
    draw.ellipse([bx, by, bx + bw, by + bh], fill="white", outline="black", width=2)

    # Draw tail (triangle pointing down)
    tail_x = x
    tail_y = by + bh - 2
    draw.polygon(
        [(tail_x - 8, tail_y), (tail_x + 8, tail_y), (tail_x, tail_y + 18)],
        fill="white",
        outline="black",
    )
    # Cover the outline where tail meets bubble
    draw.line([(tail_x - 6, tail_y), (tail_x + 6, tail_y)], fill="white", width=3)

    # Draw text
    ty = by + padding
    for line in lines:
        bbox = draw.textbbox((0, 0), line, font=font)
        lw = bbox[2] - bbox[0]
        tx = bx + (bw - lw) // 2
        draw.text((tx, ty), line, fill="black", font=font)
        ty += line_height


def load_bubbles(yaml_path: str) -> list:
    """Load speech bubble definitions from YAML.

    Expected format:
        - panel: 1
          x: 256
          y: 80
          text: "Hello world!"
        - panel: 2
          x: 200
          y: 60
          text: "What's happening?"
    """
    import yaml

    with open(yaml_path, "r") as f:
        return yaml.safe_load(f) or []


def assemble_strip(args):
    """Main assembly logic."""
    # Parse layout
    parts = args.layout.lower().split("x")
    if len(parts) != 2:
        print(f"ERROR: Invalid layout '{args.layout}'. Use ROWSxCOLS (e.g., 2x3)")
        sys.exit(1)
    rows, cols = int(parts[0]), int(parts[1])

    # Load panels
    panels = []
    for path in args.panels:
        p = Path(path)
        if not p.exists():
            print(f"ERROR: Panel not found: {path}")
            sys.exit(1)
        panels.append(Image.open(str(p)).convert("RGB"))

    total_slots = rows * cols
    if len(panels) > total_slots:
        print(
            f"WARNING: {len(panels)} panels for {rows}x{cols} grid. "
            f"Truncating to {total_slots}."
        )
        panels = panels[:total_slots]

    # Determine panel size
    if args.panel_size:
        pw, ph = map(int, args.panel_size.split("x"))
    else:
        pw = panels[0].width
        ph = panels[0].height

    # Resize panels to uniform size
    resized = []
    for panel in panels:
        if panel.size != (pw, ph):
            panel = panel.resize((pw, ph), Image.Resampling.LANCZOS)
        resized.append(panel)
    panels = resized

    # Apply halftone if requested
    if args.halftone:
        print("Applying halftone filter...")
        panels = [apply_halftone(p, args.halftone_size) for p in panels]

    # Load speech bubbles
    bubbles = []
    if args.bubbles:
        bubbles = load_bubbles(args.bubbles)

    # Calculate canvas size
    canvas_w = (
        args.margin * 2
        + cols * pw
        + cols * args.border * 2
        + (cols - 1) * args.gutter
    )
    canvas_h = (
        args.margin * 2
        + rows * ph
        + rows * args.border * 2
        + (rows - 1) * args.gutter
    )

    # Create canvas
    canvas = Image.new("RGB", (canvas_w, canvas_h), args.bg_color)
    draw = ImageDraw.Draw(canvas)

    # Place panels
    panel_idx = 0
    for row in range(rows):
        for col in range(cols):
            if panel_idx >= len(panels):
                break

            x = args.margin + col * (pw + args.border * 2 + args.gutter)
            y = args.margin + row * (ph + args.border * 2 + args.gutter)

            # Draw border rectangle
            draw.rectangle(
                [x, y, x + pw + args.border * 2, y + ph + args.border * 2],
                outline=args.border_color,
                width=args.border,
            )

            # Paste panel
            canvas.paste(panels[panel_idx], (x + args.border, y + args.border))

            # Draw speech bubbles for this panel
            for bubble in bubbles:
                if bubble.get("panel") == panel_idx + 1:
                    bx = x + args.border + bubble.get("x", pw // 2)
                    by = y + args.border + bubble.get("y", 80)
                    draw_speech_bubble(
                        draw,
                        bx,
                        by,
                        bubble.get("text", ""),
                        max_width=bubble.get("max_width", 180),
                    )

            panel_idx += 1

    # Save
    output_path = Path(args.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(str(output_path), quality=95)

    print(f"Comic strip saved: {output_path}")
    print(f"  Layout: {rows} rows x {cols} cols")
    print(f"  Panels: {len(panels)}")
    print(f"  Canvas: {canvas_w}x{canvas_h}")
    print(f"  Panel size: {pw}x{ph}")
    if args.halftone:
        print(f"  Halftone: dot size {args.halftone_size}")
    if bubbles:
        print(f"  Bubbles: {len(bubbles)}")


def main():
    args = parse_args()
    assemble_strip(args)


if __name__ == "__main__":
    main()
