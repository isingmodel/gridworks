#!/usr/bin/env python3
"""Trim transparent generation padding while preserving one isolated RGBA unit."""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--alpha-threshold", type=int, default=96)
    parser.add_argument("--padding", type=int, default=2)
    args = parser.parse_args()
    if not 1 <= args.alpha_threshold <= 254:
        raise SystemExit("alpha threshold must be between 1 and 254")
    if not 0 <= args.padding <= 64:
        raise SystemExit("padding must be between 0 and 64")

    image = Image.open(args.source).convert("RGBA")
    rgba = np.asarray(image)
    ys, xs = np.nonzero(rgba[:, :, 3] >= args.alpha_threshold)
    if len(xs) == 0:
        raise SystemExit("no foreground alpha at the requested threshold")
    left = max(0, int(xs.min()) - args.padding)
    top = max(0, int(ys.min()) - args.padding)
    right = min(image.width, int(xs.max()) + 1 + args.padding)
    bottom = min(image.height, int(ys.max()) + 1 + args.padding)
    if right - left < 16 or bottom - top < 16:
        raise SystemExit("trimmed component is unexpectedly small")

    output = image.crop((left, top, right, bottom))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    output.save(args.output, optimize=True)
    print(
        f"TRIMMED {args.output} source={image.width}x{image.height} "
        f"bounds={left},{top},{right},{bottom} output={output.width}x{output.height}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
