# /// script
# requires-python = ">=3.12"
# dependencies = [
#     "pyyaml",
# ]
# ///

#!/usr/bin/env python3
"""
Godot material exporter using canonical_brushes.json

Uses synthesized brush data including:
- Icosa GLSL shaders (easier to convert than Unity shaders)
- Pre-determined blend modes and cull settings
- Consolidated material variants with shader names
- Legacy GUID support

Outputs: godot_brush_materials/<brush-name>/<material>.tres
"""
import json
import shutil
from pathlib import Path
from collections import defaultdict

# Paths
CANONICAL_DATA = Path("canonical_brushes.json")
ICOSA_SHADERS_ROOT = Path(r"C:\Users\andyb\Documents\icosa-sketch-assets\brushes")
ASSETS_ROOT = Path("Assets")
OUTPUT_ROOT = Path("godot_brush_materials")

# Unity blend mode → Godot mapping
# From canonical data: 0=Opaque, 1=Alpha, 2=Additive, 3=Multiply
BLEND_MODE_MAP = {
    0: {"blend_mode": 0},  # Mix (opaque)
    1: {"transparency": 1},  # Alpha blend
    2: {"blend_mode": 2, "flags_transparent": True},  # Additive
    3: {"blend_mode": 3, "flags_transparent": True}   # Multiply
}

# Unity texture slot → Godot property mapping
TEX_MAP = {
    "MainTex": ("albedo_texture", None),
    "BumpMap": ("normal_texture", "normal_enabled = true"),
    "EmissionMap": ("emission_texture", "emission_enabled = true"),
    "AlphaMask": ("alpha_texture", None),
    "DisplaceTex": ("height_texture", "height_enabled = true"),
    "SpecTex": ("specular_texture", None),
    "SecondaryTex": ("detail_albedo", None),
}

# Common Unity shaders that can use StandardMaterial3D
STANDARD_MATERIAL_SHADERS = {
    "DiffuseOpaqueDoubleSided": {"cull_mode": 0},
    "DiffuseOpaqueSingleSided": {},
    "DiffuseDoubleSided": {"cull_mode": 0},
    "DiffuseSingleSided": {},
    "StandardSingleSided": {},
    "StandardDoubleSided": {"cull_mode": 0},
    "Unlit": {"flags_unshaded": True},
    "Additive": {"blend_mode": 2, "flags_transparent": True},
    "Multiplicative": {"blend_mode": 3, "flags_transparent": True}
}

def load_canonical_data():
    """Load the synthesized canonical brush data"""
    with CANONICAL_DATA.open("r", encoding="utf-8") as f:
        return json.load(f)

def build_guid_map():
    """Build GUID → file path mapping from .meta files"""
    guid_map = {}
    for meta in ASSETS_ROOT.rglob("*.meta"):
        try:
            with meta.open("r", encoding="utf-8", errors="ignore") as fh:
                for line in fh:
                    if line.startswith("guid: "):
                        guid = line.split("guid: ")[1].strip()
                        file_path = str(meta.with_suffix(""))
                        guid_map[guid] = file_path
                        break
        except Exception:
            pass
    return guid_map

def copy_texture(texture_filename, material_dir):
    """
    Copy texture from icosa-sketch-assets or Unity assets to material directory
    Returns the relative path for the .tres file
    """
    # Try icosa-sketch-assets first (these are the export-ready textures)
    # The texture filename contains the brush GUID in it
    # Try to find it in the icosa assets
    for brush_folder in ICOSA_SHADERS_ROOT.iterdir():
        if brush_folder.is_dir():
            texture_path = brush_folder / texture_filename
            if texture_path.exists():
                dest_path = material_dir / texture_filename
                if not dest_path.exists():
                    shutil.copy2(texture_path, dest_path)
                    print(f"  Copied texture: {texture_filename}")
                return texture_filename

    # Fallback: search in Unity assets
    search_results = list(ASSETS_ROOT.rglob(texture_filename))
    if search_results:
        texture_path = search_results[0]
        dest_path = material_dir / texture_filename
        if not dest_path.exists():
            shutil.copy2(texture_path, dest_path)
            print(f"  Copied texture from Unity: {texture_filename}")
        return texture_filename

    print(f"  Warning: Texture not found: {texture_filename}")
    return None

def generate_material_tres(brush, material_data, output_dir, guid_map):
    """
    Generate a Godot .tres material file for a brush variant

    Args:
        brush: Canonical brush data dict
        material_data: Unity material data dict (from unityMaterials array) or None for default
        output_dir: Output directory for this material
        guid_map: GUID to file path mapping
    """
    output_dir.mkdir(parents=True, exist_ok=True)

    # Determine material name
    if material_data:
        material_name = material_data["name"]
        shader_name = material_data.get("shaderName")
    else:
        material_name = brush["name"]
        shader_name = None

    # Check if we can use StandardMaterial3D
    use_standard_material = shader_name in STANDARD_MATERIAL_SHADERS if shader_name else True

    # Start building the material
    ext_resources = []
    next_ext_id = 1
    resource_lines = [f'resource_name = "{material_name}"']
    shader_params = {}  # For ShaderMaterial custom uniforms

    # Apply blend mode from canonical data
    blend_settings = BLEND_MODE_MAP.get(brush["blendMode"], {})
    for key, value in blend_settings.items():
        if isinstance(value, bool):
            resource_lines.append(f'{key} = {"true" if value else "false"}')
        else:
            resource_lines.append(f'{key} = {value}')

    # Apply cull mode from canonical data
    if not brush["enableCull"]:
        resource_lines.append("cull_disabled = true")

    # Apply StandardMaterial3D shader-specific settings if applicable
    if use_standard_material and shader_name in STANDARD_MATERIAL_SHADERS:
        shader_settings = STANDARD_MATERIAL_SHADERS[shader_name]
        for key, value in shader_settings.items():
            if isinstance(value, bool):
                resource_lines.append(f'{key} = {"true" if value else "false"}')
            else:
                resource_lines.append(f'{key} = {value}')

    # Enable vertex colors (most Open Brush materials use them)
    if use_standard_material:
        resource_lines.append("vertex_color_use_as_albedo = true")
        resource_lines.append("vertex_color_is_srgb = false")

    # Handle default parameters
    default_floats = brush["defaultParams"]["floats"]
    default_colors = brush["defaultParams"]["colors"]

    # Apply material overrides if present
    if material_data:
        floats = {**default_floats, **{k.lstrip("_"): v for k, v in material_data.get("floatOverrides", {}).items()}}
        colors = {**default_colors, **{k.lstrip("_"): v for k, v in material_data.get("colorOverrides", {}).items()}}
    else:
        floats = default_floats
        colors = default_colors

    # Apply common float parameters
    if "Cutoff" in floats and use_standard_material:
        resource_lines.append(f'alpha_scissor_threshold = {floats["Cutoff"]}')

    # Roughness: prefer Glossiness (Standard shader), fallback to Shininess (legacy)
    if "Glossiness" in floats and use_standard_material:
        roughness = 1.0 - floats["Glossiness"]
        resource_lines.append(f'roughness = {roughness}')
    elif "Shininess" in floats and use_standard_material:
        # Shininess uses different scale, convert properly
        # Unity Shininess is typically 0-1 range in brush params
        roughness = 1.0 - floats["Shininess"]
        resource_lines.append(f'roughness = {roughness}')
    if "Metallic" in floats and use_standard_material:
        resource_lines.append(f'metallic = {floats["Metallic"]}')
    if "EmissionGain" in floats and use_standard_material:
        resource_lines.append("emission_enabled = true")
        resource_lines.append(f'emission_energy = {floats["EmissionGain"]}')

    # For ShaderMaterial, add ALL float parameters as shader uniforms
    if not use_standard_material:
        # Skip Unity blend/render state params and runtime animation params
        ignored_params = {"Mode", "Cull", "DstBlend", "SrcBlend", "ZWrite",
                         "TimeBlend", "TimeSpeed", "Dissolve", "ClipStart", "ClipEnd"}
        for k, v in floats.items():
            if k not in ignored_params:
                shader_params[k] = str(v)

    # Apply color parameters
    if "Color" in colors:
        c = colors["Color"]
        if use_standard_material:
            if isinstance(c, list):
                resource_lines.append(f'albedo_color = Color({c[0]}, {c[1]}, {c[2]}, {c[3]})')
            else:
                resource_lines.append(f'albedo_color = Color({c.get("r", 1)}, {c.get("g", 1)}, {c.get("b", 1)}, {c.get("a", 1)})')
        else:
            # ShaderMaterial: set as shader parameter
            if isinstance(c, list):
                shader_params["Color"] = f'Color({c[0]}, {c[1]}, {c[2]}, {c[3]})'
            else:
                shader_params["Color"] = f'Color({c.get("r", 1)}, {c.get("g", 1)}, {c.get("b", 1)}, {c.get("a", 1)})'

    if "SpecColor" in colors:
        c = colors["SpecColor"]
        if use_standard_material:
            if isinstance(c, list):
                resource_lines.append(f'specular_mode = 1')
                resource_lines.append(f'specular_color = Color({c[0]}, {c[1]}, {c[2]})')
        else:
            if isinstance(c, list):
                shader_params["SpecColor"] = f'Color({c[0]}, {c[1]}, {c[2]}, 1.0)'

    # For ShaderMaterial, add ALL remaining colors as shader uniforms
    if not use_standard_material:
        for k, c in colors.items():
            if k not in ["Color", "SpecColor"]:  # Already handled above
                if isinstance(c, list):
                    shader_params[k] = f'Color({c[0]}, {c[1]}, {c[2]}, {c[3] if len(c) > 3 else 1.0})'
                else:
                    shader_params[k] = f'Color({c.get("r", 1)}, {c.get("g", 1)}, {c.get("b", 1)}, {c.get("a", 1)})'

    # Handle textures
    texture_names = brush["textures"]["names"]
    for slot, filename in texture_names.items():
        mapping = TEX_MAP.get(slot)
        if not mapping:
            continue

        # Copy texture to output directory
        copied_filename = copy_texture(filename, output_dir)
        if not copied_filename:
            continue

        # Create ext_resource reference
        tex_id = str(next_ext_id)
        next_ext_id += 1

        # Build res:// path
        try:
            tex_abs = output_dir / copied_filename
            res_rel = tex_abs.relative_to(OUTPUT_ROOT)
            res_path = f'res://{OUTPUT_ROOT.name}/{str(res_rel).replace(chr(92), "/")}'
        except ValueError:
            res_path = f'res://{OUTPUT_ROOT.name}/{output_dir.name}/{copied_filename}'

        ext_resources.append({
            "id": tex_id,
            "type": "Texture2D",
            "path": res_path
        })

        prop, extra = mapping
        if use_standard_material:
            if extra:
                resource_lines.append(extra)
            resource_lines.append(f'{prop} = ExtResource("{tex_id}")')
        else:
            # ShaderMaterial: add as shader_parameter with Unity slot name
            shader_params[slot] = f'ExtResource("{tex_id}")'

    # Build final .tres file
    material_type = "StandardMaterial3D" if use_standard_material else "ShaderMaterial"

    header = [f'[gd_resource type="{material_type}" format=3]']
    ext_lines = []
    for res in ext_resources:
        ext_lines.append(f'[ext_resource type="{res["type"]}" path="{res["path"]}" id="{res["id"]}"]')

    body = ["[resource]"] + resource_lines

    # Add shader parameters for ShaderMaterial
    if material_type == "ShaderMaterial" and shader_params:
        for k, v in shader_params.items():
            body.append(f'shader_parameter/{k} = {v}')

    # Write file
    contents = []
    contents.extend(header)
    if ext_lines:
        contents.append("")
        contents.extend(ext_lines)
    contents.append("")
    contents.extend(body)

    output_file = output_dir / f"{material_name}.tres"
    output_file.write_text("\n".join(contents), encoding="utf-8")

    return output_file

def main():
    print("Loading canonical brush data...")
    canonical = load_canonical_data()
    brushes = canonical["brushes"]

    print(f"Found {len(brushes)} canonical brushes")

    print("Building GUID map...")
    guid_map = build_guid_map()
    print(f"Mapped {len(guid_map)} GUIDs")

    # Statistics
    materials_created = 0
    brushes_processed = 0
    errors = []
    shader_usage = defaultdict(int)

    print("\nGenerating Godot materials...")

    for guid, brush in brushes.items():
        brush_name = brush["name"]
        brushes_processed += 1

        # Create output directory for this brush
        # Use a clean folder name (remove spaces and special chars)
        safe_name = brush_name.replace(" ", "_").replace("(", "").replace(")", "")
        brush_output_dir = OUTPUT_ROOT / safe_name

        try:
            # Check if brush has Unity materials
            if brush["unityMaterials"]:
                # Generate a material for each Unity material variant
                for mat_data in brush["unityMaterials"]:
                    shader_name = mat_data.get("shaderName")
                    if shader_name:
                        shader_usage[shader_name] += 1

                    generate_material_tres(brush, mat_data, brush_output_dir, guid_map)
                    materials_created += 1
                    print(f"[OK] {brush_name} / {mat_data['name']} (shader: {shader_name or 'unknown'})")
            else:
                # Generate a default material using canonical defaults
                generate_material_tres(brush, None, brush_output_dir, guid_map)
                materials_created += 1
                print(f"[OK] {brush_name} (default material)")

        except Exception as e:
            error_msg = f"Error processing {brush_name}: {e}"
            errors.append(error_msg)
            print(f"[ERROR] {error_msg}")

    # Summary
    print(f"\n{'='*60}")
    print(f"Godot Material Export Complete")
    print(f"{'='*60}")
    print(f"Brushes processed: {brushes_processed}/{len(brushes)}")
    print(f"Materials created: {materials_created}")
    print(f"Errors: {len(errors)}")

    if shader_usage:
        print(f"\nTop Unity shaders converted:")
        for shader, count in sorted(shader_usage.items(), key=lambda x: -x[1])[:10]:
            print(f"  {shader}: {count} materials")

    if errors:
        print(f"\nErrors encountered:")
        for err in errors[:10]:
            print(f"  - {err}")
        if len(errors) > 10:
            print(f"  ... and {len(errors) - 10} more")

    print(f"\nOutput directory: {OUTPUT_ROOT}")
    print(f"\nNext steps:")
    print(f"  1. Copy '{OUTPUT_ROOT}' folder into your Godot project")
    print(f"  2. Convert icosa GLSL shaders to Godot .gdshader format")
    print(f"  3. Update materials that need custom shaders (non-Standard)")

if __name__ == "__main__":
    main()
