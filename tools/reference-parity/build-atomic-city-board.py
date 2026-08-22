#!/usr/bin/env python3
"""Build the deterministic PAIR-KIT-CITY atomic-object audit board."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, __version__ as pillow_version


ASSETS = [
    ("C01", "worker-house-a.png"),
    ("C02", "worker-house-b.png"),
    ("C03", "worker-house-c.png"),
    ("C04", "row-shop-a.png"),
    ("C05", "workshop-a.png"),
    ("C06", "small-warehouse-a.png"),
    ("C07", "hospital-main-a.png"),
    ("C08", "hospital-service-a.png"),
    ("C09", "pump-house-a.png"),
    ("C10", "water-tank-a.png"),
    ("C11", "retaining-wall-a.png"),
    ("C12", "street-lamp-a.png"),
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
    parser.add_argument("map_capture", type=Path)
    parser.add_argument("output_board", type=Path)
    parser.add_argument("output_recipe", type=Path)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    if not args.asset_directory.is_dir():
        raise SystemExit(f"missing asset directory: {args.asset_directory}")
    if not args.map_capture.is_file():
        raise SystemExit(f"missing map capture: {args.map_capture}")

    width, height = 1600, 1080
    margin, header = 28, 72
    columns, rows = 4, 3
    gap = 14
    cell_width = (width - (2 * margin) - ((columns - 1) * gap)) // columns
    cell_height = (height - header - margin - ((rows - 1) * gap)) // rows
    board = Image.new("RGBA", (width, height), (9, 13, 14, 255))
    draw = ImageDraw.Draw(board)
    font = ImageFont.load_default(size=24)
    small_font = ImageFont.load_default(size=18)
    draw.text((margin, 20), "PAIR-KIT-CITY / ATOMIC UNIT AUDIT / C01-C12", fill=(225, 230, 222, 255), font=font)
    draw.text((width - 420, 24), "one runtime PNG per cell", fill=(149, 160, 153, 255), font=small_font)

    cells: list[dict[str, object]] = []
    for index, (cell_id, filename) in enumerate(ASSETS):
        row, column = divmod(index, columns)
        left = margin + column * (cell_width + gap)
        top = header + row * (cell_height + gap)
        right = left + cell_width
        bottom = top + cell_height
        draw.rectangle((left, top, right, bottom), fill=(14, 19, 19, 255), outline=(76, 87, 80, 255), width=2)
        draw.text((left + 12, top + 10), cell_id, fill=(226, 187, 102, 255), font=font)

        anchor_x = left + (cell_width // 2)
        anchor_y = bottom - 48
        diamond = [
            (anchor_x, anchor_y - 53),
            (anchor_x + 142, anchor_y),
            (anchor_x, anchor_y + 53),
            (anchor_x - 142, anchor_y),
        ]
        draw.polygon(diamond, fill=(29, 36, 34, 255), outline=(81, 93, 86, 255))
        draw.line((anchor_x - 142, anchor_y, anchor_x + 142, anchor_y), fill=(52, 63, 58, 255), width=1)

        source_path = args.asset_directory / filename
        if not source_path.is_file():
            raise SystemExit(f"missing atomic asset: {source_path}")
        source = Image.open(source_path).convert("RGBA")
        alpha_bounds = source.getchannel("A").getbbox()
        if alpha_bounds is None:
            raise SystemExit(f"fully transparent atomic asset: {source_path}")
        visible = source.crop(alpha_bounds)
        max_width = cell_width - 42
        max_height = cell_height - 78
        scale = min(max_width / visible.width, max_height / visible.height)
        resized_size = (
            max(1, round(visible.width * scale)),
            max(1, round(visible.height * scale)),
        )
        visible = visible.resize(resized_size, Image.Resampling.LANCZOS)

        source_pivot = (source.width * 0.5, source.height * 0.78)
        visible_pivot = (
            (source_pivot[0] - alpha_bounds[0]) * scale,
            (source_pivot[1] - alpha_bounds[1]) * scale,
        )
        paste_x = round(anchor_x - visible_pivot[0])
        paste_y = round(anchor_y - visible_pivot[1])
        board.alpha_composite(visible, (paste_x, paste_y))

        cells.append(
            {
                "cellId": cell_id,
                "runtimePath": f"g3/atomic/{filename}",
                "runtimeSha256": sha256(source_path),
                "sourceSize": [source.width, source.height],
                "alphaBounds": list(alpha_bounds),
                "runtimeGroundPivot": [source_pivot[0], source_pivot[1]],
                "scale": round(scale, 8),
                "boardCellRect": [left, top, cell_width, cell_height],
                "boardPasteRect": [paste_x, paste_y, resized_size[0], resized_size[1]],
                "boardGroundAnchor": [anchor_x, anchor_y],
                "compositionUnitCountExpected": 1,
            }
        )

    args.output_board.parent.mkdir(parents=True, exist_ok=True)
    args.output_recipe.parent.mkdir(parents=True, exist_ok=True)
    board.convert("RGB").save(args.output_board, format="PNG", optimize=False, compress_level=9)
    recipe = {
        "protocol": "G3-ATOMIC-CITY-AUDIT-v1",
        "pairId": "PAIR-KIT-CITY",
        "boardSize": [width, height],
        "layout": {"columns": columns, "rows": rows, "cellCount": len(cells)},
        "rendering": {
            "pillowVersion": pillow_version,
            "resampling": "LANCZOS",
            "background": "#090d0e",
            "neutralDiamond": "#1d2422",
            "runtimeGroundPivot": [0.5, 0.78],
            "retouch": False,
            "colorCorrection": False,
        },
        "boardSha256": sha256(args.output_board),
        "mapCaptureSha256": sha256(args.map_capture),
        "cells": cells,
    }
    args.output_recipe.write_text(
        json.dumps(recipe, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
