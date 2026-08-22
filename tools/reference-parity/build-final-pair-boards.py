#!/usr/bin/env python3
"""Build the pinned G.3 reference ROI boards and remaining runtime-kit boards.

Reference pixels are only cropped and placed at their original scale. Candidate
kit boards are rebuilt from the individual runtime PNGs named below; no whole
map, district, facility, or UI screenshot is used as an asset source.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, __version__ as pillow_version


BOARD_SIZE = (1600, 1080)
BACKGROUND = (9, 13, 14, 255)
CELL_FILL = (14, 19, 19, 255)
CELL_EDGE = (76, 87, 80, 255)
AMBER = (226, 187, 102, 255)
TEXT = (225, 230, 222, 255)


@dataclass(frozen=True)
class Roi:
    cell_id: str
    reference_name: str
    box: tuple[int, int, int, int]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def font(size: int) -> ImageFont.ImageFont:
    return ImageFont.load_default(size=size)


def board(title: str, subtitle: str) -> tuple[Image.Image, ImageDraw.ImageDraw]:
    image = Image.new("RGBA", BOARD_SIZE, BACKGROUND)
    draw = ImageDraw.Draw(image)
    draw.text((28, 20), title, fill=TEXT, font=font(24))
    draw.text((1160, 24), subtitle, fill=(149, 160, 153, 255), font=font(18))
    return image, draw


def grid_rects(columns: int, rows: int, top: int = 72, bottom: int = 28) -> list[tuple[int, int, int, int]]:
    margin, gap = 28, 14
    width = (BOARD_SIZE[0] - 2 * margin - (columns - 1) * gap) // columns
    height = (BOARD_SIZE[1] - top - bottom - (rows - 1) * gap) // rows
    return [
        (
            margin + column * (width + gap),
            top + row * (height + gap),
            width,
            height,
        )
        for row in range(rows)
        for column in range(columns)
    ]


def place_original_crop(
    output: Image.Image,
    draw: ImageDraw.ImageDraw,
    source: Image.Image,
    roi: Roi,
    rect: tuple[int, int, int, int],
) -> dict[str, object]:
    left, top, width, height = rect
    draw.rectangle((left, top, left + width, top + height), fill=CELL_FILL, outline=CELL_EDGE, width=2)
    draw.text((left + 12, top + 10), roi.cell_id, fill=AMBER, font=font(22))
    crop = source.crop(roi.box).convert("RGBA")
    if crop.width > width - 24 or crop.height > height - 48:
        raise SystemExit(f"reference ROI {roi.cell_id} does not fit its board cell at original scale")
    paste = (
        left + (width - crop.width) // 2,
        top + 38 + (height - 42 - crop.height) // 2,
    )
    output.alpha_composite(crop, paste)
    return {
        "cellId": roi.cell_id,
        "reference": roi.reference_name,
        "sourceBox": list(roi.box),
        "boardPasteRect": [paste[0], paste[1], crop.width, crop.height],
        "resized": False,
        "retouched": False,
    }


def build_reference_board(
    references: dict[str, tuple[Path, Image.Image]],
    output: Path,
    recipe_path: Path,
    pair_id: str,
    rois: list[Roi],
    columns: int,
    rows: int,
) -> None:
    image, draw = board(f"{pair_id} / PINNED REFERENCE ROI", "original pixels / crop only")
    rects = grid_rects(columns, rows)
    if len(rois) != len(rects):
        raise SystemExit(f"{pair_id}: expected {len(rects)} ROIs, got {len(rois)}")
    cells = [
        place_original_crop(image, draw, references[roi.reference_name][1], roi, rect)
        for roi, rect in zip(rois, rects)
    ]
    output.parent.mkdir(parents=True, exist_ok=True)
    image.convert("RGB").save(output, format="PNG", optimize=False, compress_level=9)
    recipe = {
        "protocol": "G3-REFERENCE-PARITY-v2",
        "pairId": pair_id,
        "boardSize": list(BOARD_SIZE),
        "referenceOriginalSha256": {
            name: sha256(path) for name, (path, _) in references.items()
        },
        "rendering": {
            "pillowVersion": pillow_version,
            "sourcePixelResize": False,
            "retouch": False,
            "colorCorrection": False,
        },
        "cells": cells,
        "boardSha256": sha256(output),
    }
    recipe_path.write_text(json.dumps(recipe, indent=2, sort_keys=True) + "\n", encoding="utf-8")


GROUND_ASSETS = [
    ("D01", "tiles/ground-asphalt-v1.png", "ground"),
    ("D02", "tiles/ground-scrub-v1.png", "ground"),
    ("D03", "tiles/ground-concrete-v1.png", "ground"),
    ("D04", "tiles/ground-gravel-v1.png", "ground"),
    ("D05", "g3/tiles/ground-rubble-mix-b.png", "ground"),
    ("D06", "g3/tiles/ground-rubble-relief-c.png", "ground"),
    ("D07", "g3/roads/road-straight-ne-sw-a.png", "road"),
    ("D08", "g3/roads/road-straight-nw-se-a.png", "road"),
    ("D09", "g3/roads/road-corner-n-e-a.png", "road"),
    ("D10", "g3/roads/road-t-junction-a.png", "road"),
    ("D11", "g3/roads/road-cross-junction-a.png", "road"),
    ("D12", "g3/roads/service-yard-tile-a.png", "road"),
]


def diamond_patch(source: Image.Image, size: tuple[int, int]) -> Image.Image:
    width, height = size
    square = min(source.width, source.height)
    crop = source.crop((
        (source.width - square) // 2,
        (source.height - square) // 2,
        (source.width + square) // 2,
        (source.height + square) // 2,
    )).resize((width, height), Image.Resampling.LANCZOS).convert("RGBA")
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).polygon(
        [(width // 2, 0), (width - 1, height // 2), (width // 2, height - 1), (0, height // 2)],
        fill=255,
    )
    crop.putalpha(mask)
    return crop


def build_ground_candidate(asset_root: Path, output: Path, recipe_path: Path) -> None:
    image, draw = board("PAIR-KIT-GROUND / RUNTIME ATOMIC TILES / D01-D12", "one runtime PNG per cell")
    rects = grid_rects(4, 3)
    cells: list[dict[str, object]] = []
    for (cell_id, relative, kind), (left, top, width, height) in zip(GROUND_ASSETS, rects):
        path = asset_root / relative
        source = Image.open(path).convert("RGBA")
        draw.rectangle((left, top, left + width, top + height), fill=CELL_FILL, outline=CELL_EDGE, width=2)
        draw.text((left + 12, top + 10), cell_id, fill=AMBER, font=font(22))
        if kind == "ground":
            patch = diamond_patch(source, (132, 66))
            origins = [(left + 66 + col * 94, top + 96 + row * 54) for row in range(3) for col in range(3)]
            for origin in origins:
                image.alpha_composite(patch, origin)
            paste_rect = [origins[0][0], origins[0][1], 320, 174]
        else:
            bounds = source.getchannel("A").getbbox()
            visible = source.crop(bounds) if bounds else source
            scale = min((width - 28) / visible.width, (height - 60) / visible.height)
            visible = visible.resize(
                (max(1, round(visible.width * scale)), max(1, round(visible.height * scale))),
                Image.Resampling.LANCZOS,
            )
            paste = (left + (width - visible.width) // 2, top + 42 + (height - 46 - visible.height) // 2)
            image.alpha_composite(visible, paste)
            paste_rect = [paste[0], paste[1], visible.width, visible.height]
        cells.append({
            "cellId": cell_id,
            "runtimePath": str(path.relative_to(asset_root.parent.parent)).replace("\\", "/"),
            "runtimeSha256": sha256(path),
            "kind": kind,
            "boardPasteRect": paste_rect,
            "retouch": False,
            "colorCorrection": False,
        })
    output.parent.mkdir(parents=True, exist_ok=True)
    image.convert("RGB").save(output, format="PNG", optimize=False, compress_level=9)
    recipe_path.write_text(json.dumps({
        "protocol": "G3-KIT-GROUND-v1",
        "pairId": "PAIR-KIT-GROUND",
        "boardSize": list(BOARD_SIZE),
        "rendering": {"pillowVersion": pillow_version, "retouch": False, "colorCorrection": False},
        "cells": cells,
        "boardSha256": sha256(output),
    }, indent=2, sort_keys=True) + "\n", encoding="utf-8")


UI_ASSETS = [
    ("U01", "top-metric-plate-a.png"),
    ("U02", "inspector-frame-a.png"),
    ("U03", "tool-slot-a.png"),
    ("U04", "button-default-a.png"),
    ("U05", "button-cyan-a.png"),
    ("U06", "button-amber-a.png"),
]


CITY_ASSETS = [
    ("C01", "worker-house-a.png", "object"),
    ("C02", "worker-house-b.png", "object"),
    ("C03", "worker-house-c.png", "object"),
    ("C04", "row-shop-a.png", "object"),
    ("C05", "workshop-a.png", "object"),
    ("C06", "small-warehouse-a.png", "object"),
    ("C07", "hospital-main-a.png", "object"),
    ("C08", "hospital-service-a.png", "object"),
    ("C09", "pump-house-a.png", "object"),
    ("C10", "water-tank-a.png", "object"),
    ("C11", "retaining-wall-a.png", "object"),
    ("C12", "street-lamp-a.png", "object"),
]

GRID_ASSETS = [
    ("G01", "plant-main-hall-a.png", "object"),
    ("G02", "plant-smokestack-a.png", "object"),
    ("G03", "plant-turbine-hall-a.png", "object"),
    ("G04", "switchyard-breaker-bay-a.png", "object"),
    ("G05", "substation-transformer-a.png", "object"),
    ("G06", "pole-standard-a.png", "object"),
    ("G07", "pole-reinforced-a.png", "object"),
    ("G08", "bridge-foundation-a.png", "object"),
]

RIVER_ASSETS = [
    ("R01", "river-water-neutral-b.png", "water"),
    ("R02", "river-water-heat-a.png", "water"),
    ("R03", "river-water-flood-a.png", "water"),
    ("R04", "river-bank-left-straight-a.png", "object"),
    ("R05", "river-bank-right-straight-a.png", "object"),
    ("R06", "river-bank-left-inner-a.png", "object"),
    ("R07", "river-bank-left-outer-a.png", "object"),
    ("R08", "river-bank-right-inner-a.png", "object"),
    ("R09", "river-bank-right-outer-a.png", "object"),
    ("R10", "river-bridge-abutment-a.png", "object"),
    ("R11", "river-rock-soil-transition-a.png", "object"),
    ("R12", "river-flood-ripple-a.png", "object"),
]


def build_context_candidate(
    pair_id: str,
    asset_root: Path,
    assets: list[tuple[str, str, str]],
    rows: int,
    contexts: list[tuple[str, Path, tuple[int, int, int, int]]],
    output: Path,
    recipe_path: Path,
) -> None:
    image, draw = board(
        f"{pair_id} / RUNTIME ATOMIC KIT + ASSEMBLED CONTEXT",
        "individual PNG cells / actual runtime ROI",
    )
    margin, gap, columns = 28, 14, 4
    cell_width = (BOARD_SIZE[0] - 2 * margin - (columns - 1) * gap) // columns
    cell_area_bottom = 770
    cell_height = (cell_area_bottom - 72 - (rows - 1) * gap) // rows
    records: list[dict[str, object]] = []
    for index, (cell_id, filename, kind) in enumerate(assets):
        row, column = divmod(index, columns)
        left = margin + column * (cell_width + gap)
        top = 72 + row * (cell_height + gap)
        path = asset_root / filename
        source = Image.open(path).convert("RGBA")
        draw.rectangle(
            (left, top, left + cell_width, top + cell_height),
            fill=CELL_FILL,
            outline=CELL_EDGE,
            width=2,
        )
        draw.text((left + 10, top + 7), cell_id, fill=AMBER, font=font(19))
        content_width, content_height = cell_width - 28, cell_height - 38
        if kind == "ground":
            visible = diamond_patch(source, (min(260, content_width), min(130, content_height)))
        elif kind == "water":
            visible = source.resize(
                (min(280, content_width), min(140, content_height)),
                Image.Resampling.LANCZOS,
            )
        else:
            bounds = source.getchannel("A").getbbox()
            visible = source.crop(bounds) if bounds else source
            scale = min(content_width / visible.width, content_height / visible.height)
            visible = visible.resize(
                (max(1, round(visible.width * scale)), max(1, round(visible.height * scale))),
                Image.Resampling.LANCZOS,
            )
            anchor_x = left + cell_width // 2
            anchor_y = top + cell_height - 22
            draw.polygon(
                [
                    (anchor_x, anchor_y - 35),
                    (anchor_x + 105, anchor_y),
                    (anchor_x, anchor_y + 35),
                    (anchor_x - 105, anchor_y),
                ],
                fill=(27, 34, 33, 255),
                outline=(69, 81, 76, 255),
            )
        paste = (
            left + (cell_width - visible.width) // 2,
            top + 28 + (cell_height - 30 - visible.height) // 2,
        )
        image.alpha_composite(visible, paste)
        records.append({
            "cellId": cell_id,
            "runtimePath": filename,
            "runtimeSha256": sha256(path),
            "kind": kind,
            "boardPasteRect": [paste[0], paste[1], visible.width, visible.height],
        })

    draw.text((28, 786), "ASSEMBLED RUNTIME CONTEXT / ORIGINAL PIXELS", fill=AMBER, font=font(19))
    context_records: list[dict[str, object]] = []
    context_width, context_height, context_gap = 480, 220, 52
    for index, (context_id, path, source_box) in enumerate(contexts):
        source = Image.open(path).convert("RGBA")
        crop = source.crop(source_box)
        if crop.size != (context_width, context_height):
            raise SystemExit(f"{pair_id} context {context_id} must be 480x220 original pixels")
        left = 28 + index * (context_width + context_gap)
        top = 824
        draw.rectangle(
            (left - 2, top - 2, left + context_width + 1, top + context_height + 1),
            outline=CELL_EDGE,
            width=2,
        )
        draw.text((left + 8, top + 8), context_id, fill=AMBER, font=font(18))
        image.alpha_composite(crop, (left, top))
        context_records.append({
            "contextId": context_id,
            "runtimeCapture": str(path),
            "runtimeCaptureSha256": sha256(path),
            "sourceBox": list(source_box),
            "resized": False,
            "retouched": False,
        })

    output.parent.mkdir(parents=True, exist_ok=True)
    image.convert("RGB").save(output, format="PNG", optimize=False, compress_level=9)
    recipe_path.write_text(json.dumps({
        "protocol": "G3-REFERENCE-PARITY-v2",
        "pairId": pair_id,
        "boardSize": list(BOARD_SIZE),
        "rendering": {
            "pillowVersion": pillow_version,
            "assetCellResampling": "LANCZOS",
            "runtimeContextResize": False,
            "retouch": False,
            "colorCorrection": False,
        },
        "cells": records,
        "contexts": context_records,
        "boardSha256": sha256(output),
    }, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def build_runtime_roi_candidate(
    pair_id: str,
    contexts: list[tuple[str, Path, tuple[int, int, int, int]]],
    atomic_root: Path,
    atomic_assets: list[tuple[str, str, str]],
    columns: int,
    rows: int,
    output: Path,
    recipe_path: Path,
) -> None:
    """Build the parity board from the actual runtime assembly.

    The isolated-object board remains the structural atomicity audit.  A visual
    parity judge must see the objects after the game has composed them, in the
    same crop-grid form as the pinned reference ROIs; otherwise it would score
    an evidence-board layout instead of the game.  Crops stay at original pixels
    and the recipe links every assembly board back to its atomic runtime files.
    """
    image, draw = board(
        f"{pair_id} / ACTUAL RUNTIME ASSEMBLY ROI",
        "original pixels / atomic bindings pinned",
    )
    rects = grid_rects(columns, rows)
    if len(contexts) != len(rects):
        raise SystemExit(f"{pair_id}: expected {len(rects)} runtime ROIs, got {len(contexts)}")
    records: list[dict[str, object]] = []
    for (cell_id, path, source_box), (left, top, width, height) in zip(contexts, rects):
        source = Image.open(path).convert("RGBA")
        crop = source.crop(source_box)
        if crop.width > width - 24 or crop.height > height - 48:
            raise SystemExit(f"{pair_id} runtime ROI {cell_id} does not fit at original scale")
        draw.rectangle(
            (left, top, left + width, top + height),
            fill=CELL_FILL,
            outline=CELL_EDGE,
            width=2,
        )
        draw.text((left + 12, top + 10), cell_id, fill=AMBER, font=font(22))
        paste = (
            left + (width - crop.width) // 2,
            top + 38 + (height - 42 - crop.height) // 2,
        )
        image.alpha_composite(crop, paste)
        records.append({
            "cellId": cell_id,
            "runtimeCapture": str(path),
            "runtimeCaptureSha256": sha256(path),
            "sourceBox": list(source_box),
            "boardPasteRect": [paste[0], paste[1], crop.width, crop.height],
            "resized": False,
            "retouched": False,
            "colorCorrection": False,
        })

    bindings = []
    for cell_id, filename, kind in atomic_assets:
        path = atomic_root / filename
        bindings.append({
            "cellId": cell_id,
            "runtimePath": str(path),
            "runtimeSha256": sha256(path),
            "kind": kind,
        })
    output.parent.mkdir(parents=True, exist_ok=True)
    image.convert("RGB").save(output, format="PNG", optimize=False, compress_level=9)
    recipe_path.write_text(json.dumps({
        "protocol": "G3-REFERENCE-PARITY-v2",
        "pairId": pair_id,
        "boardSize": list(BOARD_SIZE),
        "rendering": {
            "pillowVersion": pillow_version,
            "runtimeContextResize": False,
            "retouch": False,
            "colorCorrection": False,
        },
        "cells": records,
        "atomicAssetBindings": bindings,
        "boardSha256": sha256(output),
    }, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def build_ui_candidate(ui_root: Path, runtime_capture: Path, output: Path, recipe_path: Path) -> None:
    image, draw = board("PAIR-KIT-UI / ACTUAL RUNTIME HUD / U01-U07", "original pixels / atomic chrome pinned")
    cells = grid_rects(3, 2, top=72, bottom=240)
    heat_capture = runtime_capture.parent / "pair-heat.png"
    route_capture = runtime_capture.parent / "pair-route.png"
    siting_capture = runtime_capture.parent / "pair-siting.png"
    contexts = [
        ("U01", runtime_capture, (90, 0, 550, 190)),
        ("U02", runtime_capture, (1580, 80, 1870, 360)),
        ("U03", runtime_capture, (0, 250, 280, 530)),
        ("U04", heat_capture, (720, 0, 1180, 190)),
        ("U05", route_capture, (1580, 100, 1870, 380)),
        ("U06", siting_capture, (1580, 70, 1870, 350)),
    ]
    records: list[dict[str, object]] = []
    for (cell_id, path, source_box), (left, top, width, height) in zip(contexts, cells):
        source = Image.open(path).convert("RGBA")
        visible = source.crop(source_box)
        draw.rectangle((left, top, left + width, top + height), fill=CELL_FILL, outline=CELL_EDGE, width=2)
        draw.text((left + 12, top + 8), cell_id, fill=AMBER, font=font(22))
        if visible.width > width - 24 or visible.height > height - 54:
            raise SystemExit(f"PAIR-KIT-UI {cell_id} original runtime ROI does not fit")
        paste = (left + (width - visible.width) // 2, top + 38 + (height - 42 - visible.height) // 2)
        image.alpha_composite(visible, paste)
        records.append({
            "cellId": cell_id,
            "runtimeCapture": str(path),
            "runtimeCaptureSha256": sha256(path),
            "sourceBox": list(source_box),
            "resized": False,
            "retouched": False,
        })

    runtime = Image.open(runtime_capture).convert("RGBA")
    timeline_box = (540, 970, 1200, 1080)
    timeline = runtime.crop(timeline_box)
    timeline_paste = ((BOARD_SIZE[0] - timeline.width) // 2, 930)
    draw.rectangle((timeline_paste[0] - 8, 914, timeline_paste[0] + timeline.width + 8, 1058), fill=CELL_FILL, outline=CELL_EDGE, width=2)
    draw.text((timeline_paste[0], 920), "U07 / ACTUAL EVENT TIMELINE", fill=AMBER, font=font(20))
    image.alpha_composite(timeline, timeline_paste)
    output.parent.mkdir(parents=True, exist_ok=True)
    image.convert("RGB").save(output, format="PNG", optimize=False, compress_level=9)
    recipe_path.write_text(json.dumps({
        "protocol": "G3-KIT-UI-v1",
        "pairId": "PAIR-KIT-UI",
        "boardSize": list(BOARD_SIZE),
        "runtimeCaptureSha256": sha256(runtime_capture),
        "timelineSourceBox": list(timeline_box),
        "cells": records,
        "atomicChromeBindings": [
            {
                "cellId": cell_id,
                "runtimePath": str(ui_root / filename),
                "runtimeSha256": sha256(ui_root / filename),
            }
            for cell_id, filename in UI_ASSETS
        ],
        "boardSha256": sha256(output),
    }, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def build_ui_reference(
    references: dict[str, tuple[Path, Image.Image]],
    rois: list[Roi],
    output: Path,
    recipe_path: Path,
) -> None:
    """Build the UI reference board with its actual lower control strip.

    Timeline is a scored criterion, so omitting the pinned reference strip while
    adding the candidate event bar would create a structurally non-equivalent
    judgment input. All seven regions remain original-pixel crops only.
    """
    image, draw = board(
        "PAIR-KIT-UI / PINNED REFERENCE HUD / U01-U07",
        "original pixels / crop only",
    )
    cells = grid_rects(3, 2, top=72, bottom=240)
    records = [
        place_original_crop(image, draw, references[roi.reference_name][1], roi, rect)
        for roi, rect in zip(rois, cells)
    ]
    timeline_roi = Roi("U07", "01", (470, 850, 1070, 960))
    timeline = references["01"][1].crop(timeline_roi.box).convert("RGBA")
    timeline_paste = ((BOARD_SIZE[0] - timeline.width) // 2, 930)
    draw.rectangle(
        (timeline_paste[0] - 8, 914, timeline_paste[0] + timeline.width + 8, 1058),
        fill=CELL_FILL,
        outline=CELL_EDGE,
        width=2,
    )
    draw.text((timeline_paste[0], 920), "U07 / REFERENCE CONTROL STRIP", fill=AMBER, font=font(20))
    image.alpha_composite(timeline, timeline_paste)
    records.append({
        "cellId": timeline_roi.cell_id,
        "reference": timeline_roi.reference_name,
        "sourceBox": list(timeline_roi.box),
        "boardPasteRect": [timeline_paste[0], timeline_paste[1], timeline.width, timeline.height],
        "resized": False,
        "retouched": False,
    })
    output.parent.mkdir(parents=True, exist_ok=True)
    image.convert("RGB").save(output, format="PNG", optimize=False, compress_level=9)
    recipe_path.write_text(json.dumps({
        "protocol": "G3-REFERENCE-PARITY-v2",
        "pairId": "PAIR-KIT-UI",
        "boardSize": list(BOARD_SIZE),
        "referenceOriginalSha256": {
            name: sha256(path) for name, (path, _) in references.items()
        },
        "rendering": {
            "pillowVersion": pillow_version,
            "sourcePixelResize": False,
            "retouch": False,
            "colorCorrection": False,
        },
        "cells": records,
        "boardSha256": sha256(output),
    }, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("repository", type=Path)
    parser.add_argument("output_directory", type=Path)
    args = parser.parse_args()
    root = args.repository.resolve()
    output = args.output_directory.resolve()
    references = {
        f"0{index}": (root / f"assets/0{index}-{name}.png", Image.open(root / f"assets/0{index}-{name}.png").convert("RGBA"))
        for index, name in [
            (1, "grid-construction"),
            (2, "heatwave-outage"),
            (3, "route-comparison"),
            (4, "plant-siting"),
        ]
    }

    # These coordinates are protocol-owned reference ROIs, selected before the
    # final jury. They cover the role table's ground, river, facility, and HUD
    # regions without resizing, sharpening, or borrowing their pixels at runtime.
    ground = [
        Roi("D01", "01", (310, 510, 650, 730)), Roi("D02", "01", (690, 630, 1030, 850)),
        Roi("D03", "01", (1040, 650, 1380, 870)), Roi("D04", "01", (120, 620, 460, 840)),
        Roi("D05", "02", (280, 300, 620, 520)), Roi("D06", "02", (610, 530, 950, 750)),
        Roi("D07", "02", (930, 670, 1270, 890)), Roi("D08", "02", (80, 720, 420, 940)),
        Roi("D09", "04", (180, 420, 520, 640)), Roi("D10", "04", (540, 480, 880, 700)),
        Roi("D11", "04", (880, 540, 1220, 760)), Roi("D12", "04", (960, 500, 1300, 720)),
    ]
    city = [
        Roi("C01", "01", (930, 320, 1270, 560)), Roi("C02", "01", (1160, 410, 1500, 650)),
        Roi("C03", "01", (1000, 600, 1340, 840)), Roi("C04", "01", (170, 560, 510, 800)),
        Roi("C05", "02", (980, 310, 1320, 550)), Roi("C06", "02", (1180, 480, 1520, 720)),
        Roi("C07", "02", (230, 520, 570, 760)), Roi("C08", "02", (1050, 660, 1390, 900)),
        Roi("C09", "04", (910, 160, 1250, 400)), Roi("C10", "04", (1080, 300, 1420, 540)),
        Roi("C11", "04", (480, 590, 820, 830)), Roi("C12", "04", (690, 650, 1030, 890)),
    ]
    river = [
        Roi("R01", "04", (350, 40, 690, 260)), Roi("R02", "04", (450, 120, 790, 340)),
        Roi("R03", "04", (390, 220, 730, 440)), Roi("R04", "04", (350, 320, 690, 540)),
        Roi("R05", "04", (420, 420, 760, 640)), Roi("R06", "04", (350, 520, 690, 740)),
        Roi("R07", "04", (260, 620, 600, 840)), Roi("R08", "04", (180, 700, 520, 920)),
        Roi("R09", "02", (300, 260, 640, 480)), Roi("R10", "02", (380, 360, 720, 580)),
        Roi("R11", "02", (460, 480, 800, 700)), Roi("R12", "02", (520, 600, 860, 820)),
    ]
    grid = [
        Roi("G01", "01", (80, 100, 420, 380)), Roi("G02", "01", (570, 100, 910, 380)),
        Roi("G03", "01", (720, 230, 1060, 510)), Roi("G04", "03", (180, 190, 520, 470)),
        Roi("G05", "03", (300, 400, 640, 680)), Roi("G06", "03", (690, 420, 1030, 700)),
        Roi("G07", "04", (120, 100, 460, 380)), Roi("G08", "04", (480, 570, 820, 850)),
    ]
    ui = [
        Roi("U01", "01", (90, 0, 550, 190)), Roi("U02", "01", (1370, 80, 1660, 360)),
        Roi("U03", "01", (0, 250, 280, 530)), Roi("U04", "02", (720, 0, 1180, 190)),
        Roi("U05", "03", (1360, 100, 1650, 380)), Roi("U06", "04", (1310, 70, 1600, 350)),
    ]

    build_reference_board(references, output / "pair-kit-ground-reference.png", output / "pair-kit-ground-reference.recipe.json", "PAIR-KIT-GROUND", ground, 4, 3)
    build_reference_board(references, output / "pair-kit-city-reference.png", output / "pair-kit-city-reference.recipe.json", "PAIR-KIT-CITY", city, 4, 3)
    build_reference_board(references, output / "pair-kit-river-reference.png", output / "pair-kit-river-reference.recipe.json", "PAIR-KIT-RIVER", river, 4, 3)
    build_reference_board(references, output / "pair-kit-grid-reference.png", output / "pair-kit-grid-reference.recipe.json", "PAIR-KIT-GRID", grid, 4, 2)
    build_ui_reference(
        references,
        ui,
        output / "pair-kit-ui-reference.png",
        output / "pair-kit-ui-reference.recipe.json",
    )

    flood = [
        Roi("F01", "01", (200, 80, 920, 930)),
        Roi("F02", "04", (200, 80, 920, 930)),
    ]
    build_reference_board(references, output / "pair-flood-reference.png", output / "pair-flood-reference.recipe.json", "PAIR-FLOOD", flood, 2, 1)
    build_ground_candidate(root / "game/art/commercial", output / "pair-kit-ground-candidate.png", output / "pair-kit-ground-candidate.recipe.json")
    normal_capture = root / "playtests/commercial-2d/g3-final-candidate/runtime/pair-normal.png"
    heat_capture = root / "playtests/commercial-2d/g3-final-candidate/runtime/pair-heat.png"
    route_capture = root / "playtests/commercial-2d/g3-final-candidate/runtime/pair-route.png"
    siting_capture = root / "playtests/commercial-2d/g3-final-candidate/runtime/pair-siting.png"
    flood_capture = root / "playtests/commercial-2d/g3-final-candidate/runtime/pair-flood.png"
    flood_baseline_capture = root / "playtests/commercial-2d/g3-final-candidate/runtime/pair-flood-baseline.png"
    build_runtime_roi_candidate(
        "PAIR-FLOOD",
        [
            ("F01", flood_baseline_capture, (500, 110, 1220, 960)),
            ("F02", flood_capture, (500, 110, 1220, 960)),
        ],
        root / "game/art/commercial",
        [],
        2,
        1,
        output / "pair-flood-candidate-comparison.png",
        output / "pair-flood-candidate-comparison.recipe.json",
    )
    build_runtime_roi_candidate(
        "PAIR-KIT-GROUND",
        [
            ("D01", normal_capture, (260, 300, 600, 520)),
            ("D02", normal_capture, (560, 400, 900, 620)),
            ("D03", normal_capture, (840, 500, 1180, 720)),
            ("D04", normal_capture, (320, 680, 660, 900)),
            ("D05", heat_capture, (250, 300, 590, 520)),
            ("D06", heat_capture, (600, 420, 940, 640)),
            ("D07", heat_capture, (900, 560, 1240, 780)),
            ("D08", heat_capture, (300, 700, 640, 920)),
            ("D09", flood_capture, (260, 300, 600, 520)),
            ("D10", flood_capture, (560, 420, 900, 640)),
            ("D11", flood_capture, (820, 500, 1160, 720)),
            ("D12", flood_capture, (1030, 650, 1370, 870)),
        ],
        root / "game/art/commercial",
        GROUND_ASSETS,
        4,
        3,
        output / "pair-kit-ground-candidate-comparison.png",
        output / "pair-kit-ground-candidate-comparison.recipe.json",
    )
    build_runtime_roi_candidate(
        "PAIR-KIT-CITY",
        [
            ("C01", normal_capture, (1180, 100, 1520, 320)),
            ("C02", normal_capture, (1220, 250, 1560, 470)),
            ("C03", normal_capture, (1180, 500, 1520, 720)),
            ("C04", normal_capture, (1020, 700, 1360, 920)),
            ("C05", heat_capture, (1120, 100, 1460, 320)),
            ("C06", heat_capture, (1160, 280, 1500, 500)),
            ("C07", heat_capture, (1060, 620, 1400, 840)),
            ("C08", route_capture, (1000, 650, 1340, 870)),
            ("C09", siting_capture, (310, 220, 650, 440)),
            ("C10", siting_capture, (1030, 260, 1370, 480)),
            ("C11", siting_capture, (930, 650, 1270, 870)),
            ("C12", flood_capture, (1050, 650, 1390, 870)),
        ],
        root / "game/art/commercial/g3/atomic",
        CITY_ASSETS,
        4,
        3,
        output / "pair-kit-city-candidate-comparison.png",
        output / "pair-kit-city-candidate-comparison.recipe.json",
    )
    build_runtime_roi_candidate(
        "PAIR-KIT-GRID",
        [
            ("G01", normal_capture, (150, 90, 490, 370)),
            ("G02", normal_capture, (420, 80, 760, 360)),
            ("G03", normal_capture, (700, 80, 1040, 360)),
            ("G04", normal_capture, (980, 100, 1320, 380)),
            ("G05", route_capture, (300, 200, 640, 480)),
            ("G06", route_capture, (520, 260, 860, 540)),
            ("G07", route_capture, (780, 200, 1120, 480)),
            ("G08", route_capture, (900, 480, 1240, 760)),
        ],
        root / "game/art/commercial/g3/grid",
        GRID_ASSETS,
        4,
        2,
        output / "pair-kit-grid-candidate-comparison.png",
        output / "pair-kit-grid-candidate-comparison.recipe.json",
    )
    build_runtime_roi_candidate(
        "PAIR-KIT-RIVER",
        [
            ("R01", normal_capture, (540, 240, 880, 460)),
            ("R02", normal_capture, (520, 300, 860, 520)),
            ("R03", normal_capture, (500, 360, 840, 580)),
            ("R04", normal_capture, (470, 420, 810, 640)),
            ("R05", normal_capture, (430, 480, 770, 700)),
            ("R06", normal_capture, (380, 520, 720, 740)),
            ("R07", route_capture, (650, 320, 990, 540)),
            ("R08", route_capture, (560, 480, 900, 700)),
            ("R09", heat_capture, (540, 260, 880, 480)),
            ("R10", heat_capture, (470, 430, 810, 650)),
            ("R11", flood_capture, (760, 300, 1100, 520)),
            ("R12", flood_capture, (680, 430, 1020, 650)),
        ],
        root / "game/art/commercial/g3/river",
        RIVER_ASSETS,
        4,
        3,
        output / "pair-kit-river-candidate-comparison.png",
        output / "pair-kit-river-candidate-comparison.recipe.json",
    )
    build_ui_candidate(
        root / "game/art/commercial/g3/ui-v2",
        normal_capture,
        output / "pair-kit-ui-candidate.png",
        output / "pair-kit-ui-candidate.recipe.json",
    )


if __name__ == "__main__":
    main()
