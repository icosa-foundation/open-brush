import os
import re
from pathlib import Path


BRUSHES_DIR = Path("Assets/Resources/X/Brushes")
DUMMY_GUID = "00000000-0000-0000-0000-000000000000"
VERSION = "10.0"


def to_uniform_name(prop_name: str) -> str:
    if prop_name.startswith("_"):
        return f"u_{prop_name[1:]}"
    return f"u_{prop_name}"


def texture_path(material_name: str, filename_stem: str) -> str:
    # filename_stem should not include leading underscore
    brush = material_name
    guid = DUMMY_GUID
    return f"{brush}-{guid}/{brush}-{guid}-v{VERSION}-{filename_stem}.png"


def parse_material(file_path: Path):
    """Parse a Unity .mat (YAML) file for name, floats, colors, and textures."""
    name = None
    uniforms = {}

    with file_path.open("r", encoding="utf-8", errors="ignore") as f:
        lines = [ln.rstrip("\n") for ln in f]

    # Find material name
    for ln in lines:
        if ln.strip().startswith("m_Name:"):
            # m_Name: Muscle
            name = ln.split(":", 1)[1].strip()
            break

    # State machine through sections
    i = 0
    n = len(lines)
    while i < n:
        ln = lines[i]
        stripped = ln.strip()

        # Floats
        if stripped == "m_Floats:":
            i += 1
            while i < n and lines[i].strip().startswith("- "):
                item = lines[i].strip()[2:]
                # pattern: _Shininess: 0.57
                if ":" in item:
                    key, val = item.split(":", 1)
                    key = key.strip()
                    val = val.strip()
                    try:
                        num = float(val)
                        uniforms[to_uniform_name(key)] = {"type": "float", "value": num}
                    except ValueError:
                        pass
                i += 1
            continue

        # Colors
        if stripped == "m_Colors:":
            i += 1
            while i < n and lines[i].strip().startswith("- "):
                item = lines[i].strip()[2:]
                # pattern: _Color: {r: 1, g: 1, b: 1, a: 1}
                if ":" in item:
                    key, rest = item.split(":", 1)
                    key = key.strip()
                    rest = rest.strip()
                    # Extract r,g,b,a
                    m = re.search(r"\{\s*r:\s*([^,}]+),\s*g:\s*([^,}]+),\s*b:\s*([^,}]+)(?:,\s*a:\s*([^,}]+))?\s*\}", rest)
                    if m:
                        r = float(m.group(1))
                        g = float(m.group(2))
                        b = float(m.group(3))
                        a = m.group(4)
                        if a is not None:
                            a = float(a)
                        # Heuristic: _SpecColor as Vector3, others as Vector4 if alpha present
                        uniform_key = to_uniform_name(key)
                        if key == "_SpecColor":
                            uniforms[uniform_key] = {"type": "vec3", "value": (r, g, b)}
                        else:
                            if a is None:
                                uniforms[uniform_key] = {"type": "vec3", "value": (r, g, b)}
                            else:
                                uniforms[uniform_key] = {"type": "vec4", "value": (r, g, b, a)}
                i += 1
            continue

        # Textures
        if stripped == "m_TexEnvs:":
            i += 1
            while i < n and lines[i].strip().startswith("- "):
                # line like: - _MainTex:
                header = lines[i].strip()[2:].rstrip(":")
                tex_prop = header.strip()
                i += 1
                # parse the block for m_Texture
                file_id_zero = False
                while i < n and lines[i].startswith("        "):
                    sub = lines[i].strip()
                    if sub.startswith("m_Texture:"):
                        # e.g. m_Texture: {fileID: 0}
                        if "fileID: 0" in sub:
                            file_id_zero = True
                    i += 1
                if not file_id_zero:
                    # Only include textures that are set
                    filename_stem = tex_prop[1:] if tex_prop.startswith("_") else tex_prop
                    # Will fill actual path later when printing since needs material name
                    uniforms[to_uniform_name(tex_prop)] = {"type": "tex2d", "filename_stem": filename_stem}
            continue

        i += 1

    return name, uniforms


def format_uniform_value(material_name: str, entry: dict) -> str:
    t = entry.get("type")
    if t == "float":
        return f"{entry['value']}"
    if t == "vec3":
        x, y, z = entry["value"]
        return f"new Vector3({x:.6g}, {y:.6g}, {z:.6g})"
    if t == "vec4":
        x, y, z, w = entry["value"]
        return f"new Vector4({x:.6g}, {y:.6g}, {z:.6g}, {w:.6g})"
    if t == "tex2d":
        path = texture_path(material_name, entry["filename_stem"])  # consistent dummy path
        return f"\"{path}\""
    # Fallback
    return "null"


def main():
    materials = []
    for root, _dirs, files in os.walk(BRUSHES_DIR):
        for f in files:
            if f.endswith(".mat"):
                p = Path(root) / f
                name, uniforms = parse_material(p)
                if not name:
                    # derive from filename without extension
                    name = Path(f).stem
                materials.append((name, uniforms))

    # Print JS object entries per material
    first_mat = True
    for name, uniforms in sorted(materials, key=lambda x: x[0].lower()):
        if not first_mat:
            print("")
        first_mat = False
        print(f"\"{name}\" : {{")
        print("uniforms: {")
        # Output each uniform
        items = list(uniforms.items())
        for idx, (uname, uentry) in enumerate(sorted(items)):
            comma = "," if idx < len(items) - 1 else ""
            value_str = format_uniform_value(name, uentry)
            print(f"    {uname}: {{ value: {value_str} }}{comma}")
        print("}")
        print("}")


if __name__ == "__main__":
    main()

