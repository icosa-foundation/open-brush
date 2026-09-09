#!/usr/bin/env python3
"""Generate Open Brush's runtime-safe UnityGLTF URP shader collection."""

from __future__ import annotations

import argparse
import re
from pathlib import Path


PBR_GRAPH_GUID = "478ce3626be7a5f4ea58d6b13f05a2e4"
UNLIT_GRAPH_GUID = "59541e6caf586ca4f96ccf48a4813a51"

# UnityGLTF's runtime importer explicitly disables premultiplied alpha and never
# enables alpha modulation. Keeping either in the packaged power set multiplies
# variants that runtime imports cannot select.
UNUSED_RUNTIME_KEYWORDS = {
    "_ALPHAMODULATE_ON",
    "_ALPHAPREMULTIPLY_ON",
}

TRANSMISSION_KEYWORDS = {
    "_VOLUME_TRANSMISSION_ON",
    "_VOLUME_TRANSMISSION_ANDDISPERSION",
}

EXPECTED_KEYWORDS = {
    PBR_GRAPH_GUID: {
        "_ALPHAMODULATE_ON",
        "_ALPHAPREMULTIPLY_ON",
        "_ALPHATEST_ON",
        "_CLEARCOAT_ON",
        "_IRIDESCENCE_ON",
        "_SHEEN_ON",
        "_SPECULAR_ON",
        "_TEXTURE_TRANSFORM_ON",
        "_VOLUME_TRANSMISSION_ANDDISPERSION",
        "_VOLUME_TRANSMISSION_ON",
        "INSTANCING_ON",
    },
    UNLIT_GRAPH_GUID: {
        "_ALPHAMODULATE_ON",
        "_ALPHAPREMULTIPLY_ON",
        "_ALPHATEST_ON",
        "_TEXTURE_TRANSFORM_ON",
        "INSTANCING_ON",
    },
}

EXPECTED_OUTPUT_VARIANTS = {
    PBR_GRAPH_GUID: 383,
    UNLIT_GRAPH_GUID: 7,
}

SHADER_RE = re.compile(r"^  - first: .*guid: ([0-9a-f]+),")
VARIANT_RE = re.compile(r"^      - keywords:\s*(.*)$")


def variant_blocks(lines: list[str]) -> list[tuple[str | None, list[str], set[str] | None]]:
    """Split collection YAML into pass-through and shader-variant blocks."""
    blocks: list[tuple[str | None, list[str], set[str] | None]] = []
    shader_guid: str | None = None
    i = 0
    while i < len(lines):
        shader_match = SHADER_RE.match(lines[i])
        if shader_match:
            shader_guid = shader_match.group(1)

        variant_match = VARIANT_RE.match(lines[i])
        if not variant_match:
            blocks.append((shader_guid, [lines[i]], None))
            i += 1
            continue

        block = [lines[i]]
        keyword_parts = [variant_match.group(1)]
        i += 1
        while i < len(lines) and not VARIANT_RE.match(lines[i]) and not SHADER_RE.match(lines[i]):
            block.append(lines[i])
            stripped = lines[i].strip()
            if stripped and not stripped.startswith("passType:"):
                keyword_parts.append(stripped)
            i += 1
        keywords = set(" ".join(keyword_parts).split())
        blocks.append((shader_guid, block, keywords))
    return blocks


def generate(source_text: str) -> tuple[str, dict[str, int]]:
    lines = source_text.splitlines(keepends=True)
    output: list[str] = []
    output_counts = {guid: 0 for guid in EXPECTED_KEYWORDS}
    seen_keywords = {guid: set() for guid in EXPECTED_KEYWORDS}

    for shader_guid, block, keywords in variant_blocks(lines):
        if keywords is None or shader_guid not in EXPECTED_KEYWORDS:
            output.extend(block)
            continue

        seen_keywords[shader_guid].update(keywords)
        if keywords & UNUSED_RUNTIME_KEYWORDS:
            continue
        if TRANSMISSION_KEYWORDS <= keywords:
            continue

        output.extend(block)
        output_counts[shader_guid] += 1

    for shader_guid, expected in EXPECTED_KEYWORDS.items():
        if seen_keywords[shader_guid] != expected:
            raise ValueError(
                f"UnityGLTF keywords changed for {shader_guid}: "
                f"expected {sorted(expected)}, found {sorted(seen_keywords[shader_guid])}"
            )

    if output_counts != EXPECTED_OUTPUT_VARIANTS:
        raise ValueError(
            f"UnityGLTF variant topology changed: expected {EXPECTED_OUTPUT_VARIANTS}, "
            f"generated {output_counts}"
        )

    return "".join(output), output_counts


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()

    source_text = args.source.read_text(encoding="utf-8")
    generated, counts = generate(source_text)

    if args.check:
        if not args.output.exists() or args.output.read_text(encoding="utf-8") != generated:
            raise SystemExit(f"{args.output} is not up to date")
    else:
        with args.output.open("w", encoding="utf-8", newline="\n") as output_file:
            output_file.write(generated)

    print(
        f"PBR variants: {counts[PBR_GRAPH_GUID]}; "
        f"Unlit variants: {counts[UNLIT_GRAPH_GUID]}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
