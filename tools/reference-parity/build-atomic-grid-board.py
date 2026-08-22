#!/usr/bin/env python3
"""Build the deterministic G.3 atomic grid/facility audit board."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, __version__ as pillow_version


ASSETS = [
    ("G01", "plant-main-hall-a.png"),
    ("G02", "plant-smokestack-a.png"),
    ("G03", "plant-turbine-hall-a.png"),
    ("G04", "switchyard-breaker-bay-a.png"),
    ("G05", "substation-transformer-a.png"),
    ("G06", "pole-standard-a.png"),
    ("G07", "pole-reinforced-a.png"),
    ("G08", "bridge-foundation-a.png"),
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
    parser.add_argument("runtime_map", type=Path)
    parser.add_argument("runtime_draft", type=Path)
    parser.add_argument("output_board", type=Path)
    parser.add_argument("output_recipe", type=Path)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    for required in (args.asset_directory, args.runtime_map, args.runtime_draft):
        if not required.exists():
            raise SystemExit(f"missing input: {required}")

    width, height = 1600, 900
    margin, header, columns, rows, gap = 28, 72, 4, 2, 14
    cell_width = (width - (2 * margin) - ((columns - 1) * gap)) // columns
    cell_height = (height - header - margin - ((rows - 1) * gap)) // rows
    board = Image.new("RGBA", (width, height), (9, 13, 14, 255))
    draw = ImageDraw.Draw(board)
    font = ImageFont.load_default(size=24)
    small_font = ImageFont.load_default(size=18)
    draw.text(
        (margin, 20),
        "PAIR-KIT-GRID / ATOMIC UNIT AUDIT / G01-G08",
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
    for index, (cell_id, filename) in enumerate(ASSETS):
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
            raise SystemExit(f"missing atomic grid asset: {source_path}")
        source = Image.open(source_path).convert("RGBA")
        alpha_bounds = source.getchannel("A").getbbox()
        if alpha_bounds is None:
            raise SystemExit(f"fully transparent atomic grid asset: {source_path}")
        visible = source.crop(alpha_bounds)
        max_width, max_height = cell_width - 42, cell_height - 78
        scale = min(max_width / visible.width, max_height / visible.height)
        size = (max(1, round(visible.width * scale)), max(1, round(visible.height * scale)))
        visible = visible.resize(size, Image.Resampling.LANCZOS)
        anchor_x, anchor_y = left + (cell_width // 2), bottom - 55
        diamond = [
            (anchor_x, anchor_y - 62),
            (anchor_x + 155, anchor_y),
            (anchor_x, anchor_y + 62),
            (anchor_x - 155, anchor_y),
        ]
        draw.polygon(diamond, fill=(27, 34, 33, 255), outline=(69, 81, 76, 255))
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
                "runtimePath": f"g3/grid/{filename}",
                "runtimeSha256": sha256(source_path),
                "sourceSize": [source.width, source.height],
                "alphaBounds": list(alpha_bounds),
                "boardCellRect": [left, top, cell_width, cell_height],
                "boardPasteRect": [paste_x, paste_y, size[0], size[1]],
                "compositionUnitCountExpected": 1,
            }
        )

    args.output_board.parent.mkdir(parents=True, exist_ok=True)
    args.output_recipe.parent.mkdir(parents=True, exist_ok=True)
    board.convert("RGB").save(args.output_board, format="PNG", optimize=False, compress_level=9)
    recipe = {
        "protocol": "G3-ATOMIC-GRID-AUDIT-v1",
        "pairId": "PAIR-KIT-GRID",
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
        "mapSha256": sha256(args.runtime_map),
        "draftSha256": sha256(args.runtime_draft),
        "cells": cells,
    }
    args.output_recipe.write_text(
        json.dumps(recipe, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
