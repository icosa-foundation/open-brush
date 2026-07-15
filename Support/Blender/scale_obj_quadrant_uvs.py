#!/usr/bin/env python3

import argparse
import math
from pathlib import Path


SCALE_MARKER = "# open-brush-quadrant-texture-scale:"
ROTATION_MARKER = "# open-brush-quadrant-uv-rotation-degrees:"
ALIGNMENT_MARKER = "# open-brush-quadrant-uv-alignment:"
RADIAL_OFFSET_MARKER = "# open-brush-quadrant-uv-radial-offset:"
TARGET_OBJECTS = {
    "Quadrant_East",
    "Quadrant_North",
    "Quadrant_West",
    "Quadrant_South",
}
SOUTH_ALIGNMENT_ROTATIONS = {
    "Quadrant_East": 90.0,
    "Quadrant_North": 180.0,
    "Quadrant_West": -90.0,
    "Quadrant_South": 0.0,
}
RADIAL_OFFSET_DIRECTIONS = {
    "Quadrant_East": (-1.0, 0.0),
    "Quadrant_North": (0.0, -1.0),
    "Quadrant_West": (1.0, 0.0),
    "Quadrant_South": (0.0, 1.0),
}


def parse_args():
    parser = argparse.ArgumentParser(
        description="Scale and rotate each quadrant's OBJ UV island around its centre."
    )
    parser.add_argument("obj", type=Path)
    parser.add_argument(
        "--texture-scale",
        type=float,
        required=True,
        help="Visual texture scale; 0.5 doubles the UV coverage.",
    )
    parser.add_argument(
        "--rotation-degrees",
        type=float,
        help="Absolute anticlockwise UV rotation; omitted preserves the current rotation.",
    )
    parser.add_argument(
        "--align-to-south",
        action="store_true",
        help="Align every quadrant to Quadrant_South's controller-space orientation.",
    )
    parser.add_argument(
        "--radial-offset",
        type=float,
        help="Absolute outward UV offset; 0.15 moves each island by 15%% of UV space.",
    )
    return parser.parse_args()


def main():
    args = parse_args()
    if args.texture_scale <= 0:
        raise ValueError("--texture-scale must be greater than zero")

    lines = args.obj.read_text(encoding="utf-8").splitlines()
    current_texture_scale = 1.0
    current_rotation_degrees = 0.0
    current_alignment = "radial"
    current_radial_offset = 0.0
    for line in lines:
        if line.startswith(SCALE_MARKER):
            current_texture_scale = float(line[len(SCALE_MARKER):].strip())
        elif line.startswith(ROTATION_MARKER):
            current_rotation_degrees = float(line[len(ROTATION_MARKER):].strip())
        elif line.startswith(ALIGNMENT_MARKER):
            current_alignment = line[len(ALIGNMENT_MARKER):].strip()
        elif line.startswith(RADIAL_OFFSET_MARKER):
            current_radial_offset = float(line[len(RADIAL_OFFSET_MARKER):].strip())

    target_rotation_degrees = (
        current_rotation_degrees
        if args.rotation_degrees is None
        else args.rotation_degrees
    )
    target_alignment = "south" if args.align_to_south else current_alignment
    target_radial_offset = (
        current_radial_offset
        if args.radial_offset is None
        else args.radial_offset
    )
    if current_alignment not in {"radial", "south"}:
        raise ValueError(f"Unknown current UV alignment: {current_alignment}")
    if current_alignment == "south" and target_alignment == "radial":
        raise ValueError("Converting south alignment back to radial is not supported")

    uv_scale = current_texture_scale / args.texture_scale
    global_rotation_delta = target_rotation_degrees - current_rotation_degrees
    radial_offset_delta = target_radial_offset - current_radial_offset
    uv_values = {}
    object_uv_indices = {name: set() for name in TARGET_OBJECTS}
    current_object = None
    uv_index = 0

    for line in lines:
        if line.startswith("vt "):
            uv_index += 1
            fields = line.split()
            uv_values[uv_index] = (float(fields[1]), float(fields[2]))
        elif line.startswith("o "):
            current_object = line[2:].strip()
        elif line.startswith("f ") and current_object in TARGET_OBJECTS:
            for vertex in line.split()[1:]:
                fields = vertex.split("/")
                if len(fields) > 1 and fields[1]:
                    object_uv_indices[current_object].add(int(fields[1]))

    missing = [name for name, indices in object_uv_indices.items() if not indices]
    if missing:
        raise ValueError(f"No UV coordinates found for: {', '.join(sorted(missing))}")

    owners = {}
    for object_name, indices in object_uv_indices.items():
        for index in indices:
            previous_owner = owners.setdefault(index, object_name)
            if previous_owner != object_name:
                raise ValueError(
                    f"UV index {index} is shared by {previous_owner} and {object_name}"
                )

    transformed_uvs = {}
    for object_name, indices in object_uv_indices.items():
        coordinates = [uv_values[index] for index in indices]
        centre_u = (min(u for u, _ in coordinates) + max(u for u, _ in coordinates)) / 2
        centre_v = (min(v for _, v in coordinates) + max(v for _, v in coordinates)) / 2
        alignment_rotation = 0.0
        if current_alignment == "radial" and target_alignment == "south":
            alignment_rotation = SOUTH_ALIGNMENT_ROTATIONS[object_name]
        rotation_radians = math.radians(global_rotation_delta + alignment_rotation)
        rotation_cos = math.cos(rotation_radians)
        rotation_sin = math.sin(rotation_radians)
        radial_direction_u, radial_direction_v = RADIAL_OFFSET_DIRECTIONS[object_name]
        for index in indices:
            u, v = uv_values[index]
            offset_u = (u - centre_u) * uv_scale
            offset_v = (v - centre_v) * uv_scale
            transformed_uvs[index] = (
                centre_u
                + offset_u * rotation_cos
                - offset_v * rotation_sin
                + radial_direction_u * radial_offset_delta,
                centre_v
                + offset_u * rotation_sin
                + offset_v * rotation_cos
                + radial_direction_v * radial_offset_delta,
            )

    output = []
    marker_written = False
    uv_index = 0
    for line in lines:
        if (
            line.startswith(SCALE_MARKER)
            or line.startswith(ROTATION_MARKER)
            or line.startswith(ALIGNMENT_MARKER)
            or line.startswith(RADIAL_OFFSET_MARKER)
        ):
            continue

        if line.startswith("vt "):
            uv_index += 1
            if uv_index in transformed_uvs:
                u, v = transformed_uvs[uv_index]
                fields = line.split()
                suffix = "" if len(fields) < 4 else f" {fields[3]}"
                line = f"vt {u:.6f} {v:.6f}{suffix}"
        output.append(line)

        if not marker_written and line.startswith("mtllib "):
            output.append(f"{SCALE_MARKER} {args.texture_scale:g}")
            output.append(f"{ROTATION_MARKER} {target_rotation_degrees:g}")
            output.append(f"{ALIGNMENT_MARKER} {target_alignment}")
            output.append(f"{RADIAL_OFFSET_MARKER} {target_radial_offset:g}")
            marker_written = True

    args.obj.write_text("\n".join(output) + "\n", encoding="utf-8")
    print(
        f"Changed texture scale from {current_texture_scale:g} to "
        f"{args.texture_scale:g} using UV multiplier {uv_scale:g}; "
        f"rotation from {current_rotation_degrees:g} to "
        f"{target_rotation_degrees:g} degrees anticlockwise; "
        f"alignment from {current_alignment} to {target_alignment}; "
        f"radial offset from {current_radial_offset:g} to {target_radial_offset:g}"
    )


if __name__ == "__main__":
    main()
