#!/usr/bin/env python3
"""
Open Brush Unity 6 / Vulkan shader compatibility fixes.

This script:
1. Disables auto-generated DEFERRED passes on legacy Surface Shaders used by brushes.
   The Unity 6 Vulkan compiler is failing in those generated passes with:
     "argument pulled into unrelated predicate"
2. Fixes UnlitHDRColorButton's wrong UNITY_INITIALIZE_OUTPUT type.
3. Adds the missing stereo output fields in SwatchBloom's mobile v2f.

Run from the Open Brush repository root:

    python3 openbrush_unity6_vulkan_fix_v3.py --dry-run
    python3 openbrush_unity6_vulkan_fix_v3.py

Then:
    rm -rf Library/ShaderCache
    rm -rf Library/Artifacts
    # reopen Unity

Review:
    git diff

Revert:
    git restore Assets/Resources/Brushes Assets/Resources/X/Brushes \
        Assets/Shaders/UnlitHDRColorButton.shader Assets/Shaders/SwatchBloom.shader
"""

from __future__ import annotations

import argparse
import re
from pathlib import Path


BRUSH_ROOTS = [
    Path("Assets/Resources/Brushes"),
    Path("Assets/Resources/X/Brushes"),
]

SURFACE_RE = re.compile(r"^(?P<indent>\s*)#pragma\s+surface\s+.+$", re.MULTILINE)


def read_text(path: Path) -> tuple[str, bool]:
    data = path.read_bytes()
    bom = data.startswith(b"\xef\xbb\xbf")
    if bom:
        return data.decode("utf-8-sig"), True
    return data.decode("utf-8"), False


def write_text(path: Path, text: str, bom: bool) -> None:
    enc = "utf-8-sig" if bom else "utf-8"
    path.write_text(text, encoding=enc, newline="")


def patch_surface_shaders(dry_run: bool) -> tuple[int, int]:
    changed_files = 0
    changed_pragmas = 0

    shader_files: list[Path] = []
    for root in BRUSH_ROOTS:
        if root.exists():
            shader_files.extend(root.rglob("*.shader"))

    for path in sorted(set(shader_files)):
        text, bom = read_text(path)
        lines = text.splitlines(keepends=True)
        changed = []

        for idx, line in enumerate(lines):
            stripped_newline = line.rstrip("\r\n")
            newline = line[len(stripped_newline):]

            if not re.match(r"^\s*#pragma\s+surface\s+", stripped_newline):
                continue
            if "exclude_path:deferred" in stripped_newline:
                continue

            new_line = stripped_newline.rstrip() + " exclude_path:deferred" + newline
            changed.append((idx + 1, stripped_newline.strip(), new_line.rstrip("\r\n").strip()))
            lines[idx] = new_line

        if changed:
            changed_files += 1
            changed_pragmas += len(changed)
            print(f"\n{path}")
            for line_no, old, new in changed:
                print(f"  line {line_no}")
                print(f"    - {old}")
                print(f"    + {new}")

            if not dry_run:
                write_text(path, "".join(lines), bom)

    return changed_files, changed_pragmas


def exact_replace(path: Path, old: str, new: str, description: str, dry_run: bool) -> bool:
    if not path.exists():
        print(f"\nWARNING: {path} not found; skipped {description}")
        return False

    text, bom = read_text(path)

    if new in text:
        print(f"\n{path}: already fixed ({description})")
        return False

    count = text.count(old)
    if count != 1:
        print(f"\nWARNING: {path}: expected exactly 1 match for {description}, found {count}; not modified")
        return False

    print(f"\n{path}")
    print(f"  {description}")
    print(f"    - {old.strip()}")
    print(f"    + {new.strip()}")

    if not dry_run:
        write_text(path, text.replace(old, new, 1), bom)

    return True



def patch_swatch_bloom(path: Path, dry_run: bool) -> bool:
    """Add stereo output only to SwatchBloom's MOBILE v2f."""
    if not path.exists():
        print(f"\nWARNING: {path} not found; skipped SwatchBloom stereo fix")
        return False

    text, bom = read_text(path)
    marker = "// MOBILE VERSION"
    if marker not in text:
        print(f"\nWARNING: {path}: MOBILE VERSION marker not found; not modified")
        return False

    head, mobile = text.split(marker, 1)

    # If the mobile v2f already has the field, don't touch it.
    v2f_start = mobile.find("struct v2f")
    vert_start = mobile.find("v2f vert", v2f_start)
    if v2f_start < 0 or vert_start < 0:
        print(f"\nWARNING: {path}: mobile v2f block not found; not modified")
        return False

    v2f_block = mobile[v2f_start:vert_start]
    if "UNITY_VERTEX_OUTPUT_STEREO" in v2f_block:
        print(f"\n{path}: already fixed (mobile UNITY_VERTEX_OUTPUT_STEREO)")
        return False

    old = "        UNITY_VERTEX_INPUT_INSTANCE_ID\n      };"
    new = "        UNITY_VERTEX_INPUT_INSTANCE_ID\n\n        UNITY_VERTEX_OUTPUT_STEREO\n      };"

    if old not in v2f_block:
        # Handle CRLF-normalized content or slightly different spacing by replacing
        # the closing brace immediately after UNITY_VERTEX_INPUT_INSTANCE_ID.
        pattern = re.compile(
            r"(struct\s+v2f\s*\{.*?UNITY_VERTEX_INPUT_INSTANCE_ID)(\s*\};)",
            re.DOTALL,
        )
        m = pattern.search(mobile, v2f_start, vert_start)
        if not m:
            print(f"\nWARNING: {path}: could not locate insertion point in mobile v2f")
            return False
        replacement = m.group(1) + "\n\n        UNITY_VERTEX_OUTPUT_STEREO" + m.group(2)
        mobile = mobile[:m.start()] + replacement + mobile[m.end():]
    else:
        # Restrict replacement to the mobile v2f block.
        fixed_block = v2f_block.replace(old, new, 1)
        mobile = mobile[:v2f_start] + fixed_block + mobile[vert_start:]

    print(f"\n{path}")
    print("  add missing UNITY_VERTEX_OUTPUT_STEREO to mobile v2f")

    if not dry_run:
        write_text(path, head + marker + mobile, bom)
    return True


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    if not Path("Assets").is_dir():
        print("ERROR: Run this script from the Open Brush repository root.")
        return 2

    print("Open Brush Unity 6 / Vulkan shader fix v3")
    if args.dry_run:
        print("DRY RUN - no files will be changed")

    changed_files, changed_pragmas = patch_surface_shaders(args.dry_run)

    ui_fix = exact_replace(
        Path("Assets/Shaders/UnlitHDRColorButton.shader"),
        "UNITY_INITIALIZE_OUTPUT(Input, o);",
        "UNITY_INITIALIZE_OUTPUT(v2f, o);",
        "fix wrong output struct passed to UNITY_INITIALIZE_OUTPUT",
        args.dry_run,
    )

    swatch_fix = patch_swatch_bloom(
        Path("Assets/Shaders/SwatchBloom.shader"),
        args.dry_run,
    )

    print("\nSummary")
    print(f"  Surface Shader files changed: {changed_files}")
    print(f"  Surface pragmas changed:      {changed_pragmas}")
    print(f"  UnlitHDRColorButton fix:      {'yes' if ui_fix else 'no/new change not needed'}")
    print(f"  SwatchBloom stereo fix:       {'yes' if swatch_fix else 'no/new change not needed'}")

    if not args.dry_run:
        print("\nNow clear Unity's generated shader/artifact cache:")
        print("  rm -rf Library/ShaderCache Library/Artifacts")
        print("\nThen reopen Unity and inspect the first remaining shader error.")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
