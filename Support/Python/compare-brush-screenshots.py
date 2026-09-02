#!/usr/bin/env python3
"""Rank Open Brush screenshot differences using foreground-focused RGB SSIM."""

from __future__ import annotations

import argparse
from pathlib import Path
import sys

try:
    import numpy as np
    from PIL import Image
    from skimage.metrics import structural_similarity
    from skimage.morphology import dilation, disk
except ModuleNotFoundError as error:
    print(
        f"Missing Python dependency: {error.name}. "
        "Run `uv pip install --python .venv-ssim\\Scripts\\python.exe "
        "-r scripts\\requirements-brush-screenshots.txt`.",
        file=sys.stderr,
    )
    raise SystemExit(2) from error


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--old-dir", type=Path, required=True)
    parser.add_argument("--new-dir", type=Path, required=True)
    parser.add_argument("--pattern", default="brush-*.png")
    parser.add_argument("--top", type=int, default=30)
    parser.add_argument(
        "--background-threshold",
        type=int,
        default=8,
        help="maximum RGB value treated as black background (default: 8)",
    )
    parser.add_argument(
        "--mask-radius",
        type=int,
        default=8,
        help="radius around rendered pixels included in foreground SSIM (default: 8)",
    )
    parser.add_argument(
        "--crop-padding",
        type=int,
        default=16,
        help="padding around the union of rendered-pixel bounds (default: 16)",
    )
    return parser.parse_args()


def crop_to_foreground_union(
    old_pixels: np.ndarray,
    new_pixels: np.ndarray,
    background_threshold: int,
    crop_padding: int,
) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    foreground = np.any(old_pixels > background_threshold, axis=2)
    foreground |= np.any(new_pixels > background_threshold, axis=2)
    if not foreground.any():
        return old_pixels, new_pixels, foreground

    y_coordinates, x_coordinates = np.nonzero(foreground)
    height, width = foreground.shape
    x_min = max(0, int(x_coordinates.min()) - crop_padding)
    x_max = min(width, int(x_coordinates.max()) + crop_padding + 1)
    y_min = max(0, int(y_coordinates.min()) - crop_padding)
    y_max = min(height, int(y_coordinates.max()) + crop_padding + 1)
    crop = np.s_[y_min:y_max, x_min:x_max]
    return old_pixels[crop], new_pixels[crop], foreground[crop]


def compare_pair(
    old_path: Path,
    new_path: Path,
    background_threshold: int,
    mask_radius: int,
    crop_padding: int,
) -> tuple[float, float]:
    old_pixels = np.asarray(Image.open(old_path).convert("RGB"))
    new_pixels = np.asarray(Image.open(new_path).convert("RGB"))
    if old_pixels.shape != new_pixels.shape:
        raise ValueError(f"image dimensions differ: {old_pixels.shape} != {new_pixels.shape}")

    full_similarity = structural_similarity(
        old_pixels,
        new_pixels,
        channel_axis=2,
        data_range=255,
    )
    old_crop, new_crop, foreground = crop_to_foreground_union(
        old_pixels,
        new_pixels,
        background_threshold,
        crop_padding,
    )
    if not foreground.any():
        return float(full_similarity), float(full_similarity)

    foreground = dilation(foreground, disk(mask_radius))
    _, similarity_map = structural_similarity(
        old_crop,
        new_crop,
        channel_axis=2,
        data_range=255,
        full=True,
    )
    foreground_similarity = similarity_map.mean(axis=2)[foreground].mean()
    return float(foreground_similarity), float(full_similarity)


def main() -> int:
    args = parse_args()
    old_files = {
        path.name.casefold(): path for path in args.old_dir.glob(args.pattern)
    }
    new_files = {
        path.name.casefold(): path for path in args.new_dir.glob(args.pattern)
    }
    common_names = sorted(old_files.keys() & new_files.keys())
    results: list[tuple[float, float, str]] = []
    failures: list[str] = []

    for common_name in common_names:
        old_path = old_files[common_name]
        new_path = new_files[common_name]
        try:
            foreground_similarity, full_similarity = compare_pair(
                old_path,
                new_path,
                args.background_threshold,
                args.mask_radius,
                args.crop_padding,
            )
        except (OSError, ValueError) as error:
            failures.append(f"{old_path.name}: {error}")
            continue
        brush_name = old_path.stem.removeprefix("brush-")
        results.append(
            (1 - foreground_similarity, 1 - full_similarity, brush_name)
        )

    results.sort(reverse=True)
    limit = len(results) if args.top <= 0 else min(args.top, len(results))
    print(
        f"pairs={len(results)} old_only={len(old_files.keys() - new_files.keys())} "
        f"new_only={len(new_files.keys() - old_files.keys())} failures={len(failures)}"
    )
    print("rank foreground_difference full_frame_difference brush")
    for rank, (foreground_difference, full_difference, brush_name) in enumerate(
        results[:limit], start=1
    ):
        print(
            f"{rank:4d} {foreground_difference:.9f} "
            f"{full_difference:.9f} {brush_name}"
        )

    if failures:
        print("failures:", file=sys.stderr)
        for failure in failures:
            print(f"  {failure}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
