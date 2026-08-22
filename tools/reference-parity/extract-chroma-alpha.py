#!/usr/bin/env python3
"""Convert an isolated ImageGen #00ff00 matte into a validated RGBA sprite."""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--dominance-threshold", type=int, default=45)
    args = parser.parse_args()

    rgb = np.asarray(Image.open(args.source).convert("RGB"), dtype=np.uint8)
    work = rgb.astype(np.int16)
    red, green, blue = work[:, :, 0], work[:, :, 1], work[:, :, 2]
    dominance = np.minimum(green - red, green - blue)
    keyed = (green >= 110) & (dominance >= args.dominance_threshold)

    alpha = np.full(green.shape, 255, dtype=np.uint8)
    feather = np.clip((105 - dominance) * 4, 0, 255).astype(np.uint8)
    alpha[keyed] = feather[keyed]
    alpha[:2, :] = 0
    alpha[-2:, :] = 0
    alpha[:, :2] = 0
    alpha[:, -2:] = 0

    # Neutralize chroma spill on every retained edge pixel. Image generation can
    # leave a low-dominance green halo outside the main keyed class, especially
    # after downsizing narrow rubble objects.
    edge = (green > (np.maximum(red, blue) + 5)) & (alpha > 0)
    neutral_cap = np.maximum(red, blue) + 10
    work[:, :, 1][edge] = np.minimum(green[edge], neutral_cap[edge])

    rgba = np.dstack((np.clip(work, 0, 255).astype(np.uint8), alpha))
    transparent_fraction = float(np.count_nonzero(alpha == 0)) / alpha.size
    corners = [int(alpha[0, 0]), int(alpha[0, -1]), int(alpha[-1, 0]), int(alpha[-1, -1])]
    if transparent_fraction < 0.35 or any(value != 0 for value in corners):
        raise SystemExit(
            "chroma extraction rejected: "
            f"transparent_fraction={transparent_fraction:.4f}, corners={corners}"
        )

    args.output.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(rgba).save(args.output, optimize=True)
    print(
        f"EXTRACTED {args.output} transparent_fraction={transparent_fraction:.4f} "
        f"corners={corners}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
