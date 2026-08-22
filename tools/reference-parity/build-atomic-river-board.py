#!/usr/bin/env python3
"""Build the deterministic G.3 atomic river-kit audit board."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, __version__ as pillow_version


ASSETS = [
    ("R01", "river-water-neutral-b.png", "water_tile"),
    ("R02", "river-water-heat-a.png", "water_tile"),
    ("R03", "river-water-flood-a.png", "water_tile"),
    ("R04", "river-bank-left-straight-a.png", "bank_object"),
    ("R05", "river-bank-right-straight-a.png", "bank_object"),
    ("R06", "river-bank-left-inner-a.png", "bank_object"),
    ("R07", "river-bank-left-outer-a.png", "bank_object"),
    ("R08", "river-bank-right-inner-a.png", "bank_object"),
    ("R09", "river-bank-right-outer-a.png", "bank_object"),
    ("R10", "river-bridge-abutment-a.png", "bank_object"),
    ("R11", "river-rock-soil-transition-a.png", "bank_object"),
    ("R12", "river-flood-ripple-a.png", "effect_object"),
]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("asset_directory", type=Path)
    parser.add_argument("normal_capture", type=Path)
    parser.add_argument("heat_capture", type=Path)
    parser.add_argument("flood_capture", type=Path)
    parser.add_argument("output_board", type=Path)
    parser.add_argument("output_recipe", type=Path)
    return parser.parse_args()


def fit_image(source: Image.Image, max_width: int, max_height: int) -> Image.Image:
    scale = min(max_width / source.width, max_height / source.height)
    size = (max(1, round(source.width * scale)), max(1, round(source.height * scale)))
    return source.resize(size, Image.Resampling.LANCZOS)


def main() -> None:
    args = parse_args()
    if not args.asset_directory.is_dir():
        raise SystemExit(f"missing asset directory: {args.asset_directory}")
    captures = {
        "normal": args.normal_capture,
        "heat": args.heat_capture,
        "flood": args.flood_capture,
    }
    for state, capture in captures.items():
        if not capture.is_file():
            raise SystemExit(f"missing {state} capture: {capture}")

    width, height = 1600, 1080
    margin, header, columns, rows, gap = 28, 72, 4, 3, 14
    cell_width = (width - (2 * margin) - ((columns - 1) * gap)) // columns
    cell_height = (height - header - margin - ((rows - 1) * gap)) // rows
    board = Image.new("RGBA", (width, height), (9, 13, 14, 255))
    draw = ImageDraw.Draw(board)
    font = ImageFont.load_default(size=24)
    small_font = ImageFont.load_default(size=18)
    draw.text(
        (margin, 20),
        "PAIR-KIT-RIVER / ATOMIC UNIT AUDIT / R01-R12",
        fill=(225, 230, 222, 255),
        font=font,
    )
    draw.text(
        (width - 455, 24),
        "one runtime PNG per cell",
        fill=(149, 160, 153, 255),
        font=small_font,
    )

    cells: list[dict[str, object]] = []
    for index, (cell_id, filename, asset_type) in enumerate(ASSETS):
        row, column = divmod(index, columns)
        left = margin + column * (cell_width + gap)
        top = header + row * (cell_height + gap)
        right = left + cell_width
        bottom = top + cell_height
        draw.rectangle(
            (left, top, right, bottom),
            fill=(14, 19, 19, 255),
            outline=(76, 87, 80, 255),
            width=2,
        )
        draw.text((left + 12, top + 10), cell_id, fill=(226, 187, 102, 255), font=font)

        source_path = args.asset_directory / filename
        if not source_path.is_file():
            raise SystemExit(f"missing atomic river asset: {source_path}")
        source = Image.open(source_path).convert("RGBA")
        alpha_bounds = source.getchannel("A").getbbox()
        if alpha_bounds is None:
            raise SystemExit(f"fully transparent atomic river asset: {source_path}")

        content_top = top + 52
        content_height = cell_height - 70
        if asset_type == "water_tile":
            visible = fit_image(source, cell_width - 42, content_height - 30)
            paste_x = left + ((cell_width - visible.width) // 2)
            paste_y = content_top + ((content_height - visible.height) // 2)
            draw.rectangle(
                (paste_x - 2, paste_y - 2, paste_x + visible.width + 1, paste_y + visible.height + 1),
                outline=(93, 116, 116, 255),
                width=2,
            )
            board.alpha_composite(visible, (paste_x, paste_y))
        else:
            visible_source = source.crop(alpha_bounds)
            visible = fit_image(visible_source, cell_width - 42, content_height - 18)
            paste_x = left + ((cell_width - visible.width) // 2)
            paste_y = content_top + ((content_height - visible.height) // 2)
            diamond_center_x = left + (cell_width // 2)
            diamond_center_y = bottom - 64
            diamond = [
                (diamond_center_x, diamond_center_y - 62),
                (diamond_center_x + 155, diamond_center_y),
                (diamond_center_x, diamond_center_y + 62),
                (diamond_center_x - 155, diamond_center_y),
            ]
            draw.polygon(diamond, fill=(27, 34, 33, 255), outline=(69, 81, 76, 255))
            board.alpha_composite(visible, (paste_x, paste_y))

        cells.append(
            {
                "cellId": cell_id,
                "assetType": asset_type,
                "runtimePath": f"g3/river/{filename}",
                "runtimeSha256": sha256(source_path),
                "sourceSize": [source.width, source.height],
                "alphaBounds": list(alpha_bounds),
                "boardCellRect": [left, top, cell_width, cell_height],
                "boardPasteRect": [paste_x, paste_y, visible.width, visible.height],
                "compositionUnitCountExpected": 1,
            }
        )

    args.output_board.parent.mkdir(parents=True, exist_ok=True)
    args.output_recipe.parent.mkdir(parents=True, exist_ok=True)
    board.convert("RGB").save(args.output_board, format="PNG", optimize=False, compress_level=9)
    recipe = {
        "protocol": "G3-ATOMIC-RIVER-AUDIT-v1",
        "pairId": "PAIR-KIT-RIVER",
        "boardSize": [width, height],
        "layout": {"columns": columns, "rows": rows, "cellCount": len(cells)},
        "rendering": {
            "pillowVersion": pillow_version,
            "resampling": "LANCZOS",
            "background": "#090d0e",
            "retouch": False,
            "colorCorrection": False,
        },
        "boardSha256": sha256(args.output_board),
        "captureSha256": {state: sha256(path) for state, path in captures.items()},
        "cells": cells,
    }
    args.output_recipe.write_text(
        json.dumps(recipe, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
