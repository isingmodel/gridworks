#!/usr/bin/env python3
"""Remove ImageGen's light checkerboard matte from an isolated dark sprite.

This is deliberately narrow: it accepts only a near-neutral, very light matte and
refuses outputs whose corners or transparent fraction do not prove isolation.
It does not segment arbitrary scenes or alter RGB pixels retained as foreground.
"""

from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument(
        "--connected-matte-floor",
        type=int,
        help=(
            "Flood only near-neutral pixels at or above this value from the canvas "
            "edge. Use for a uniform white matte around a deliberately dark object."
        ),
    )
    parser.add_argument(
        "--minimum-transparent-fraction",
        type=float,
        default=0.35,
        help=(
            "Minimum fraction of fully transparent pixels required after extraction. "
            "Keep the default for sprites; tightly framed UI plates may declare a lower "
            "value while still requiring all four transparent corners."
        ),
    )
    args = parser.parse_args()
    if not 0.01 <= args.minimum_transparent_fraction <= 0.95:
        raise SystemExit("minimum transparent fraction must be between 0.01 and 0.95")

    rgb = np.asarray(Image.open(args.source).convert("RGB"), dtype=np.uint8)
    low = rgb.min(axis=2).astype(np.int16)
    high = rgb.max(axis=2).astype(np.int16)
    chroma = high - low

    alpha = np.full(low.shape, 255, dtype=np.uint8)
    if args.connected_matte_floor is None:
        # The generated matte alternates between two nearly white neutral values.
        # A short feather band retains antialiased dark silhouette pixels.
        matte = (low >= 222) & (chroma <= 22)
        feather = np.clip((235 - low) * 20, 0, 255).astype(np.uint8)
        alpha[matte] = feather[matte]
    else:
        floor = args.connected_matte_floor
        if floor < 96 or floor > 240:
            raise SystemExit("connected matte floor must be between 96 and 240")
        traversable = (low >= floor) & (chroma <= 28)
        connected = np.zeros(low.shape, dtype=bool)
        queue: deque[tuple[int, int]] = deque()
        height, width = low.shape
        for x in range(width):
            if traversable[0, x]:
                queue.append((0, x))
            if traversable[height - 1, x]:
                queue.append((height - 1, x))
        for y in range(height):
            if traversable[y, 0]:
                queue.append((y, 0))
            if traversable[y, width - 1]:
                queue.append((y, width - 1))
        while queue:
            y, x = queue.popleft()
            if connected[y, x] or not traversable[y, x]:
                continue
            connected[y, x] = True
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    if dx == 0 and dy == 0:
                        continue
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < height and 0 <= nx < width and not connected[ny, nx]:
                        queue.append((ny, nx))
        ceiling = min(250, floor + 70)
        feather = np.clip(
            (ceiling - low) * (255.0 / max(1, ceiling - floor)),
            0,
            255,
        ).astype(np.uint8)
        alpha[connected] = feather[connected]
    # Generators occasionally leave a one-pixel neutral seam on the canvas edge.
    # The source contract requires generous padding, so the outer two pixels can
    # be cleared deterministically without touching the isolated object.
    alpha[:2, :] = 0
    alpha[-2:, :] = 0
    alpha[:, :2] = 0
    alpha[:, -2:] = 0

    # Recover straight-alpha edge colour from the near-white generated matte.
    # Keeping the observed near-white RGB under a fractional alpha creates a
    # conspicuous halo after mipmapping. Solve observed = fg*a + white*(1-a)
    # for fg on the feather band; fully opaque foreground pixels are untouched.
    clean_rgb = rgb.astype(np.float32)
    fractional = (alpha > 0) & (alpha < 255)
    alpha_fraction = alpha.astype(np.float32) / 255.0
    for channel in range(3):
        observed = clean_rgb[:, :, channel]
        recovered = np.zeros_like(observed)
        recovered[fractional] = (
            observed[fractional] - (255.0 * (1.0 - alpha_fraction[fractional]))
        ) / alpha_fraction[fractional]
        observed[fractional] = np.clip(recovered[fractional], 0.0, 255.0)
        observed[alpha == 0] = 0.0
    rgba = np.dstack((clean_rgb.astype(np.uint8), alpha))
    transparent_fraction = float(np.count_nonzero(alpha == 0)) / alpha.size
    corners = [int(alpha[0, 0]), int(alpha[0, -1]), int(alpha[-1, 0]), int(alpha[-1, -1])]
    if transparent_fraction < args.minimum_transparent_fraction or any(value != 0 for value in corners):
        raise SystemExit(
            "checkerboard extraction rejected: "
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
