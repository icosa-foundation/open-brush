# /// script
# requires-python = ">=3.12"
# dependencies = [
#     "pyyaml",
# ]
# ///

#!/usr/bin/env python3
"""
Unity brush materials → Godot .tres (rough first pass)

Requires:  pip install pyyaml
Outputs:   godot_brush_materials/<subdirs>/<material>.tres
"""
import re
import yaml
import shutil
from pathlib import Path

ASSETS_ROOT = Path("Assets")
BRUSH_ROOT  = ASSETS_ROOT / "Resources" / "Brushes"
OUTPUT_ROOT = Path("godot_brush_materials")

# Unity texture slot → (Godot property, optional extra line)
TEX_MAP = {
    "_MainTex":          ("albedo_texture", None),
    "_BumpMap":          ("normal_texture", "normal_enabled = true"),
    "_EmissionMap":      ("emission_texture", "emission_enabled = true"),
    "_MetallicGlossMap": ("metallic_texture", None),
    "_MetallicMap":      ("metallic_texture", None),
    "_SpecGlossMap":     ("specular_texture", None),
    "_OcclusionMap":     ("ao_texture", None),
    "_ParallaxMap":      ("height_texture", "height_enabled = true"),
    "_DetailMask":       ("detail_mask", None),
    "_DetailAlbedoMap":  ("detail_albedo_texture", None),
    "_DetailNormalMap":  ("detail_normal_texture", None),
    "_GlossMap":         ("roughness_texture", None),
    "_Illum":            ("emission_texture", "emission_enabled = true"),
    # Extend as needed
}

# Shaders that can be replaced with StandardMaterial3D
STANDARD_MATERIAL_SHADERS = {
    "DiffuseOpaqueDoubleSided": {"cull_mode": 0},
    "DiffuseOpaqueSingleSided": {},
    "DiffuseDoubleSided": {"cull_mode": 0},
    "DiffuseSingleSided": {},
    "StandardSingleSided": {},
    "StandardDoubleSided": {"cull_mode": 0},
    "Unlit": {"flags_unshaded": True, "cull_mode": 0},
    "Additive": {"blend_mode": 2, "flags_transparent": True, "cull_mode": 0},
    "Multiplicative": {"blend_mode": 3, "flags_transparent": True}
}

# Enhanced regexes for shader directive parsing
SHADER_PATTERNS = {
    "Cull":    re.compile(r"^\s*Cull\s+(Off|Front|Back)", re.MULTILINE),
    "ZWrite":  re.compile(r"^\s*ZWrite\s+(On|Off)",       re.MULTILINE),
    "ZTest":   re.compile(r"^\s*ZTest\s+(\w+)",           re.MULTILINE),
    "Blend":   re.compile(r"^\s*Blend\s+(\w+)\s+(\w+)",   re.MULTILINE),
    "BlendOp": re.compile(r"^\s*BlendOp\s+(\w+)",         re.MULTILINE),
    "ColorMask":re.compile(r"^\s*ColorMask\s+([RGBA0-9]+)",re.MULTILINE),
    "Queue":   re.compile(r'Tags\s*\{[^}]*"Queue"\s*=\s*"([^"]+)"', re.MULTILINE),
    "RenderType": re.compile(r'Tags\s*\{[^}]*"RenderType"\s*=\s*"([^"]+)"', re.MULTILINE),
    "AlphaToMask": re.compile(r"^\s*AlphaToMask\s+(On|Off)", re.MULTILINE),
}

def build_guid_map():
    guid_map = {}
    for meta in ASSETS_ROOT.rglob("*.meta"):
        try:
            with meta.open("r", encoding="utf-8", errors="ignore") as fh:
                for line in fh:
                    if line.startswith("guid: "):
                        guid_map[line.split("guid: ")[1].strip()] = str(meta.with_suffix(""))
                        break
        except Exception:
            pass
    return guid_map

def load_mat(mat_path):
    text = mat_path.read_text(encoding="utf-8", errors="ignore")
    text = re.sub(r'!u!\d+ &\d+', '', text)  # strip Unity YAML tags

    material_doc = None
    for doc in yaml.safe_load_all(text):    # handle multi-document YAML
        if isinstance(doc, dict) and "Material" in doc:
            material_doc = doc["Material"]
            break
    if material_doc is None:
        raise ValueError(f"No Material block found in {mat_path}")

    props  = material_doc.get("m_SavedProperties", {})
    tex    = {k:v for d in props.get("m_TexEnvs", []) for k,v in d.items()}
    floats = {k:v for d in props.get("m_Floats", [])  for k,v in d.items()}
    colors = {k:v for d in props.get("m_Colors", [])  for k,v in d.items()}

    # Get shader GUID from material
    shader_guid = material_doc.get("m_Shader", {}).get("guid")
    return material_doc["m_Name"], tex, floats, colors, shader_guid

def parse_shader(shader_path):
    directives = {}
    if shader_path and shader_path.exists():
        text = shader_path.read_text(encoding="utf-8", errors="ignore")
        for key, pat in SHADER_PATTERNS.items():
            m = pat.search(text)
            if m:
                directives[key] = m.groups()
    return directives

def convert_unity_shader_to_godot(unity_code, shader_name):
    """Convert Unity shader code to Godot shader with automated conversions"""

    # 1. Extract Properties
    properties = extract_unity_properties(unity_code)

    # 2. Determine shader type and render modes
    shader_type, render_modes = determine_godot_shader_type(unity_code)

    # 3. Extract and convert vertex function
    vertex_code = extract_and_convert_vertex(unity_code, shader_type)

    # 4. Extract and convert fragment function
    fragment_code = extract_and_convert_fragment(unity_code, shader_type)

    # 5. Add Unity built-in conversions
    builtin_conversions = get_unity_builtin_conversions(unity_code)

    # 6. Convert basic structure
    godot_shader = f"""shader_type {shader_type};

{render_modes}

{builtin_conversions}

{properties}

{vertex_code}

{fragment_code}
"""

    return godot_shader

def extract_unity_properties(unity_code):
    """Extract Unity Properties block and convert to Godot uniforms"""
    properties_match = re.search(r'Properties\s*\{([^{}]*(?:\{[^{}]*\}[^{}]*)*)\}', unity_code, re.DOTALL)
    if not properties_match:
        return "// No properties found"

    properties_block = properties_match.group(1)
    uniforms = []

    # Common Unity property patterns
    patterns = [
        (r'_(\w+)\s*\(\s*"([^"]+)"\s*,\s*Color\s*\)\s*=\s*\([^)]+\)', r'uniform vec4 \1 : source_color = vec4(1.0, 1.0, 1.0, 1.0); // \2'),
        (r'_(\w+)\s*\(\s*"([^"]+)"\s*,\s*2D\s*\)\s*=\s*"[^"]*"', r'uniform sampler2D \1 : source_color; // \2'),
        (r'_(\w+)\s*\(\s*"([^"]+)"\s*,\s*Float\s*\)\s*=\s*([0-9.-]+)', r'uniform float \1 : hint_range(0.0, 10.0) = \3; // \2'),
        (r'_(\w+)\s*\(\s*"([^"]+)"\s*,\s*Range\s*\(\s*([0-9.-]+)\s*,\s*([0-9.-]+)\s*\)\s*\)\s*=\s*([0-9.-]+)', r'uniform float \1 : hint_range(\3, \4) = \5; // \2'),
        (r'_(\w+)\s*\(\s*"([^"]+)"\s*,\s*Vector\s*\)\s*=\s*\([^)]+\)', r'uniform vec4 \1 = vec4(0.0, 0.0, 0.0, 0.0); // \2'),
    ]

    converted_props = properties_block
    for unity_pattern, godot_replacement in patterns:
        converted_props = re.sub(unity_pattern, godot_replacement, converted_props, flags=re.MULTILINE)

    # Clean up and format
    lines = [line.strip() for line in converted_props.split('\n') if line.strip() and not line.strip().startswith('//')]
    uniform_lines = [line for line in lines if line.startswith('uniform')]

    return '\n'.join(uniform_lines) if uniform_lines else "// No convertible properties found"

def determine_godot_shader_type(unity_code):
    """Determine Godot shader type and render modes from Unity code"""

    # All brush shaders are for 3D meshes, so always use spatial
    shader_type = "spatial"
    render_modes = []

    # Determine render modes from Unity tags/settings
    if 'Cull Off' in unity_code:
        render_modes.append("cull_disabled")
    if 'ZWrite Off' in unity_code:
        render_modes.append("depth_draw_never")
    if '"Queue"="Transparent"' in unity_code:
        render_modes.append("blend_mix")
    if '"RenderType"="TransparentCutout"' in unity_code:
        render_modes.append("depth_test_disabled")
    if 'Blend SrcAlpha OneMinusSrcAlpha' in unity_code:
        render_modes.append("blend_mix")
    elif 'Blend SrcAlpha One' in unity_code:
        render_modes.append("blend_add")
    elif 'Blend DstColor Zero' in unity_code:
        render_modes.append("blend_mul")

    render_modes_str = "render_mode " + ", ".join(render_modes) + ";" if render_modes else ""

    return shader_type, render_modes_str

def extract_and_convert_vertex(unity_code, shader_type):
    """Extract Unity vertex function and convert to Godot"""

    # Look for vertex function patterns
    vertex_patterns = [
        r'v2f\s+vert\s*\([^)]*\)\s*\{([^{}]*(?:\{[^{}]*\}[^{}]*)*)\}',
        r'void\s+vert\s*\([^)]*\)\s*\{([^{}]*(?:\{[^{}]*\}[^{}]*)*)\}',
    ]

    vertex_body = None
    for pattern in vertex_patterns:
        match = re.search(pattern, unity_code, re.DOTALL)
        if match:
            vertex_body = match.group(1)
            break

    if not vertex_body:
        return """void vertex() {
    // No Unity vertex function found - using default
}"""

    # Convert common vertex operations
    conversions = []

    # Position transformations
    if 'UnityObjectToClipPos' in vertex_body:
        conversions.append("// Unity UnityObjectToClipPos -> Godot built-in vertex processing")
        conversions.append("// VERTEX is automatically transformed by Godot")

    # UV transformations
    if 'TRANSFORM_TEX' in vertex_body:
        conversions.append("// Unity TRANSFORM_TEX -> manual UV transform")
        conversions.append("// UV = UV * _MainTex_ST.xy + _MainTex_ST.zw;")

    # Color transformations
    if 'TbVertToNative' in vertex_body or 'TbVertToSrgb' in vertex_body:
        conversions.append("COLOR = COLOR; // Unity color space conversion")

    # Time-based animations
    if '_Time' in vertex_body or 'GetTime()' in vertex_body:
        conversions.append("// TIME variable available in Godot")
        conversions.append("float time_val = TIME;")

    converted_code = '\n    '.join(conversions) if conversions else "// Vertex conversion needed"

    return f"""void vertex() {{
    {converted_code}

    // TODO: Convert Unity vertex logic:
    /* Original Unity vertex code:
    {vertex_body.strip()}
    */
}}"""

def extract_and_convert_fragment(unity_code, shader_type):
    """Extract Unity fragment/surface function and convert to Godot"""

    # Look for fragment function patterns
    frag_patterns = [
        r'fixed4\s+frag\s*\([^)]*\)\s*:\s*SV_Target\s*\{([^{}]*(?:\{[^{}]*\}[^{}]*)*)\}',
        r'void\s+surf\s*\([^)]*\)\s*\{([^{}]*(?:\{[^{}]*\}[^{}]*)*)\}',
    ]

    frag_body = None
    for pattern in frag_patterns:
        match = re.search(pattern, unity_code, re.DOTALL)
        if match:
            frag_body = match.group(1)
            break

    if not frag_body:
        # Fallback to basic conversion
        return f"""void fragment() {{
    {get_basic_fragment_conversion(unity_code)}
}}"""

    # Convert fragment operations (all brush shaders are spatial)
    conversions = []

    # Texture sampling
    if 'tex2D(' in frag_body:
        conversions.append("// Unity tex2D() -> Godot texture()")
        if '_MainTex' in frag_body or 'MainTex' in frag_body:
            conversions.append("vec4 main_tex = texture(MainTex, UV);")
            conversions.append("ALBEDO = main_tex.rgb;")

    # Color operations
    if '_TintColor' in frag_body or '_Color' in frag_body or 'TintColor' in frag_body or 'Color' in frag_body:
        conversions.append("// Apply tint if available")
        conversions.append("// ALBEDO *= TintColor.rgb; // or Color.rgb")

    # Alpha operations
    if 'SV_Target' in unity_code and ('c.a' in frag_body or 'o.Alpha' in frag_body):
        conversions.append("ALPHA = main_tex.a;")

    # Emission
    if 'o.Emission' in frag_body:
        conversions.append("EMISSION = emission_color.rgb;")

    # Clipping/Dissolve
    if 'discard' in frag_body:
        conversions.append("// TODO: Convert Unity discard to Godot")
        conversions.append("// Use ALPHA = 0.0 or conditional logic")

    # Opacity
    if '_Opacity' in frag_body or 'Opacity' in frag_body:
        conversions.append("ALPHA *= Opacity;")

    converted_code = '\n    '.join(conversions) if conversions else get_basic_fragment_conversion(unity_code, shader_type)

    return f"""void fragment() {{
    {converted_code}

    // TODO: Convert Unity fragment logic:
    /* Original Unity fragment code:
    {frag_body.strip()}
    */
}}"""

def get_unity_builtin_conversions(unity_code):
    """Generate Unity built-in variable conversions"""
    conversions = []

    # Time variables
    if '_Time' in unity_code or 'GetTime()' in unity_code:
        conversions.append("// Unity _Time -> Godot TIME")
        conversions.append("// Unity GetTime() -> TIME")

    # Matrix conversions
    if 'unity_ObjectToWorld' in unity_code:
        conversions.append("// Unity unity_ObjectToWorld -> Godot WORLD_MATRIX")

    if 'UNITY_MATRIX_VP' in unity_code:
        conversions.append("// Unity UNITY_MATRIX_VP -> Godot PROJECTION_MATRIX * VIEW_MATRIX * MODEL_MATRIX")

    # Screen/UV conversions
    if '_ScreenParams' in unity_code:
        conversions.append("// Unity _ScreenParams -> Godot SCREEN_PIXEL_SIZE")

    # Camera conversions
    if '_WorldSpaceCameraPos' in unity_code:
        conversions.append("// Unity _WorldSpaceCameraPos -> Godot CAMERA_POSITION_WORLD")

    return '\n'.join([f"// {conv}" for conv in conversions]) if conversions else ""

def create_godot_import_file(texture_path, texture_type="texture"):
    """Create a .import file for Godot texture import settings"""
    import_path = texture_path.with_suffix(texture_path.suffix + ".import")

    # Basic Godot texture import settings
    try:
        rel_under_output = texture_path.relative_to(OUTPUT_ROOT)
        res_rel_str = f"{OUTPUT_ROOT.name}/{str(rel_under_output).replace(chr(92), '/')}"
    except ValueError:
        # Fallback; best-effort path including the output root directory name
        res_rel_str = f"{OUTPUT_ROOT.name}/{texture_path.name}"

    import_content = f"""[remap]

importer="texture"
type="CompressedTexture2D"
uid="uid://b{hash(str(texture_path)) % 1000000000:010d}"
path="res://.godot/imported/{texture_path.name}-{hash(str(texture_path)) % 1000000:06x}.ctex"
metadata={{
"vram_texture": false
}}

[deps]

source_file="res://{res_rel_str}"
dest_files=["res://.godot/imported/{texture_path.name}-{hash(str(texture_path)) % 1000000:06x}.ctex"]

[params]

compress/mode=0
compress/high_quality=false
compress/lossy_quality=0.7
compress/hdr_compression=1
compress/normal_map=0
compress/channel_pack=0
mipmaps/generate=false
mipmaps/limit=-1
roughness/mode=0
roughness/src_normal=""
process/fix_alpha_border=true
process/premult_alpha=false
process/normal_map_invert_y=false
process/hdr_as_srgb=false
process/hdr_clamp_exposure=false
process/size_limit=0
detect_3d/compress_to=1
"""

    import_path.write_text(import_content, encoding="utf-8")

def copy_texture_to_godot(texture_path, guid_map, material_dir):
    """Copy a Unity texture to the same directory as the material"""
    if not texture_path or not Path(texture_path).exists():
        return None

    try:
        texture_path_obj = Path(texture_path)
        godot_texture_path = material_dir / texture_path_obj.name

        # Copy texture if it doesn't already exist
        if not godot_texture_path.exists():
            shutil.copy2(texture_path_obj, godot_texture_path)
            # Create Godot import file for proper texture handling
            create_godot_import_file(godot_texture_path)
            print(f"Copied texture: {texture_path_obj.name} to {material_dir.relative_to(OUTPUT_ROOT)}")

        # Return just the filename since texture is in same directory as material
        return texture_path_obj.name

    except Exception as e:
        print(f"Warning: Could not copy texture {texture_path}: {e}")
        return None

def get_basic_fragment_conversion(unity_code, shader_type="spatial"):
    """Generate basic fragment shader conversion hints (all brush shaders are spatial)"""
    conversions = []

    if 'tex2D(' in unity_code:
        conversions.append("// Unity tex2D() -> Godot texture()")
        conversions.append("// tex2D(_MainTex, uv) -> texture(_MainTex, uv)")

    if '_MainTex' in unity_code or 'MainTex' in unity_code:
        conversions.append("ALBEDO = texture(MainTex, UV).rgb;")

    if '_Color' in unity_code or 'Color' in unity_code:
        conversions.append("// ALBEDO *= Color.rgb;")

    if 'o.Alpha' in unity_code or 'c.a' in unity_code:
        conversions.append("ALPHA = texture(MainTex, UV).a;")

    return '\n    '.join(conversions) if conversions else "ALBEDO = vec3(1.0);"

def write_tres(name, tex, floats, colors, shader_guid, guid_map, out_dir):
    out_dir.mkdir(parents=True, exist_ok=True)

    # Look up actual shader path from GUID
    shader_path = None
    shader_exists = False
    use_standard_material = False
    standard_material_settings = {}

    if shader_guid and shader_guid in guid_map:
        shader_path = Path(guid_map[shader_guid])
        shader_exists = shader_path.exists()

        # Check if this shader can be replaced with StandardMaterial3D
        if shader_exists:
            shader_name = shader_path.stem
            if shader_name in STANDARD_MATERIAL_SHADERS:
                use_standard_material = True
                standard_material_settings = STANDARD_MATERIAL_SHADERS[shader_name]
                print(f"Using StandardMaterial3D for {name} (was {shader_name})")

    shader_directives = parse_shader(shader_path) if shader_path and not use_standard_material else {}

    # Determine material type
    material_type = "StandardMaterial3D" if (use_standard_material or not shader_exists) else "ShaderMaterial"

    # We'll assemble ext resources and resource body separately, then join
    ext_resources = []  # list of dicts: {id, type, path}
    next_ext_id = 1

    resource_lines = [
        f'resource_name = "{name}"'
    ]
    shader_params = {}

    # ----- Color parameters -----
    if material_type != "ShaderMaterial":
        if "_Color" in colors:
            c = colors["_Color"]
            resource_lines.append(f'albedo_color = Color({c["r"]}, {c["g"]}, {c["b"]}, {c["a"]})')
        if "_EmissionColor" in colors:
            e = colors["_EmissionColor"]
            if any(e.values()):
                if not any("emission_enabled = true" in line for line in resource_lines):
                    resource_lines.append("emission_enabled = true")
                resource_lines.append(f'emission_color = Color({e["r"]}, {e["g"]}, {e["b"]}, 1.0)')
    else:
        # For ShaderMaterial, wire colors as shader parameters
        for k, c in colors.items():
            pname = k.lstrip('_')
            shader_params[pname] = f'Color({c["r"]}, {c["g"]}, {c["b"]}, {c["a"]})'

    # ----- Scalar/flag parameters -----
    if material_type != "ShaderMaterial":
        if "_Metallic" in floats:
            resource_lines.append(f'metallic = {floats["_Metallic"]}')
        if "_Glossiness" in floats:
            resource_lines.append(f'roughness = {1 - floats["_Glossiness"]}')
        if "_BumpScale" in floats:
            resource_lines.append(f'normal_scale = {floats["_BumpScale"]}')
        if "_OcclusionStrength" in floats:
            resource_lines.append(f'ao_light_affect = {floats["_OcclusionStrength"]}')
        if "_Cutoff" in floats:
            resource_lines.append(f'alpha_scissor_threshold = {floats["_Cutoff"]}')
        if "_Mode" in floats:
            mode = int(floats["_Mode"])  # Unity: 0 Opaque, 1 Cutout, 2 Fade, 3 Transparent
            unity_to_godot = {0:0, 1:2, 2:1, 3:1}
            resource_lines.append(f"transparency = {unity_to_godot.get(mode,0)}")
            if mode in (2,3):
                resource_lines.append("flags_transparent = true")
        if floats.get("_AlphaToMask", 0) > 0:
            resource_lines.append("alpha_antialiasing_mode = 1")
    else:
        # For ShaderMaterial, forward all floats as shader parameters
        for k, v in floats.items():
            # Skip known Unity-only pipeline flags that don't map well as uniforms
            if k in {"_Mode", "_Cull", "_DstBlend", "_SrcBlend", "_ZWrite"}:
                continue
            pname = k.lstrip('_')
            shader_params[pname] = str(v)

    # Culling from material or shader
    if material_type != "ShaderMaterial" and "_Cull" in floats:
        cull_map = {0:"double_sided = true", 1:"cull_mode = 1", 2:"cull_mode = 2"}
        resource_lines.append(cull_map.get(int(floats["_Cull"]), ""))
    elif material_type != "ShaderMaterial" and "Cull" in shader_directives:
        val = shader_directives["Cull"][0]
        cull_map = {"Off":"double_sided = true", "Front":"cull_mode = 1", "Back":"cull_mode = 2"}
        cull_setting = cull_map.get(val, f"# TODO: Cull {val}")
        if cull_setting:  # Only append if not empty
            resource_lines.append(cull_setting)

    if material_type != "ShaderMaterial" and "ZWrite" in shader_directives and shader_directives["ZWrite"][0] == "Off":
        resource_lines.append("no_depth_test = true")
    if material_type != "ShaderMaterial" and "ZTest" in shader_directives:
        ztest = shader_directives["ZTest"][0]
        ztest_map = {
            "Less": "depth_func = 1",
            "Equal": "depth_func = 2",
            "LEqual": "depth_func = 3",
            "Greater": "depth_func = 4",
            "NotEqual": "depth_func = 5",
            "GEqual": "depth_func = 6",
            "Always": "depth_func = 7"
        }
        if ztest in ztest_map:
            resource_lines.append(ztest_map[ztest])
        else:
            resource_lines.append(f"# TODO: ZTest {ztest}")

    # Handle render queue for transparency ordering
    if material_type != "ShaderMaterial" and "Queue" in shader_directives:
        queue = shader_directives["Queue"][0]
        if "Transparent" in queue:
            resource_lines.append("flags_transparent = true")
        elif "Overlay" in queue:
            resource_lines.append("flags_do_not_receive_shadows = true")

    if material_type != "ShaderMaterial" and "AlphaToMask" in shader_directives and shader_directives["AlphaToMask"][0] == "On":
        resource_lines.append("alpha_antialiasing_mode = 1")
    if material_type != "ShaderMaterial" and "Blend" in shader_directives:
        sf, df = shader_directives["Blend"]
        # Common Unity -> Godot blend mode mappings
        blend_modes = {
            ("SrcAlpha", "OneMinusSrcAlpha"): "blend_mode = 1",  # Alpha
            ("One", "One"): "blend_mode = 2",  # Add
            ("DstColor", "Zero"): "blend_mode = 3",  # Multiply
            ("SrcAlpha", "One"): "blend_mode = 2",  # Additive with alpha
        }
        blend_key = (sf, df)
        if blend_key in blend_modes:
            resource_lines.append(blend_modes[blend_key])
            resource_lines.append("flags_transparent = true")
        else:
            resource_lines.append(f"# TODO: Blend {sf} {df}")
    if material_type != "ShaderMaterial" and "ColorMask" in shader_directives:
        resource_lines.append(f"# TODO: ColorMask {shader_directives['ColorMask'][0]}")

    if material_type != "ShaderMaterial" and floats.get("_VertexColorUseAsAlbedo", 0) > 0:
        resource_lines.append("vertex_color_use_as_albedo = true")

    # Handle emission gain for brush materials
    if material_type != "ShaderMaterial" and "_EmissionGain" in floats:
        gain = floats["_EmissionGain"]
        if not any("emission_enabled = true" in line for line in resource_lines):
            resource_lines.append("emission_enabled = true")
        resource_lines.append(f"emission_energy = {gain}")

    # Apply StandardMaterial3D specific settings
    if use_standard_material:
        for setting, value in standard_material_settings.items():
            if isinstance(value, bool):
                resource_lines.append(f"{setting} = {'true' if value else 'false'}")
            else:
                resource_lines.append(f"{setting} = {value}")

    # Preserve custom shader parameters as comments for manual handling
    custom_props = []
    for key, value in floats.items():
        if key.startswith("_") and key not in [
            "_BumpScale", "_Cutoff", "_Metallic", "_Glossiness", "_Mode",
            "_OcclusionStrength", "_Parallax", "_Cull", "_EmissionGain",
            "_VertexColorUseAsAlbedo", "_DstBlend", "_SrcBlend", "_ZWrite"
        ]:
            custom_props.append(f"# Custom: {key} = {value}")

    if custom_props:
        resource_lines.append("")
        resource_lines.extend(custom_props)

    # ----- Textures + UV transforms -----
    for slot, env in tex.items():
        guid = env["m_Texture"].get("guid")
        if not guid or guid not in guid_map:
            continue

        texture_path = guid_map[guid]
        mapping = TEX_MAP.get(slot)
        scale  = env.get("m_Scale",  {"x":1, "y":1})
        offset = env.get("m_Offset", {"x":0, "y":0})

        # Copy texture to material directory
        copied_texture_path = copy_texture_to_godot(texture_path, guid_map, out_dir)

        if copied_texture_path:
            # Build res:// path for texture and register as ext_resource
            tex_abs = out_dir / copied_texture_path
            try:
                res_rel = tex_abs.relative_to(OUTPUT_ROOT)
                res_path = f'res://{OUTPUT_ROOT.name}/{str(res_rel).replace(chr(92), "/")}'
            except ValueError:
                # Fallback: best-effort include output root name
                res_path = f'res://{OUTPUT_ROOT.name}/{copied_texture_path}'

            tex_id = str(next_ext_id)
            next_ext_id += 1
            ext_resources.append({
                "id": tex_id,
                "type": "Texture2D",
                "path": res_path,
            })

            if material_type != "ShaderMaterial":
                # Map into StandardMaterial3D properties
                if mapping:
                    prop, extra = mapping
                    if extra:
                        resource_lines.append(extra)
                    resource_lines.append(f'{prop} = ExtResource("{tex_id}")')
                else:
                    resource_lines.append(f'# TODO: {slot} -> res:// path for {copied_texture_path}')
            else:
                # Wire to shader parameter using Unity slot name without underscore
                pname = slot.lstrip('_')
                shader_params[pname] = f'ExtResource("{tex_id}")'

            # Handle UV transforms comments (shader should deal with UVs)
            if scale != {"x":1, "y":1} or offset != {"x":0, "y":0}:
                resource_lines.append(f'# Note: {slot} has UV scale {scale} offset {offset} (wire in shader if needed)')
        else:
            # Texture couldn't be copied - leave a note
            original_path = texture_path.replace("\\", "/")
            resource_lines.append(f'# MISSING: {slot} texture could not be copied from {original_path}')
            if mapping:
                prop, extra = mapping
                if material_type != "ShaderMaterial":
                    resource_lines.append(f'# {prop} = ExtResource("<id>")  # missing')
                else:
                    resource_lines.append(f'# shader_parameter/{slot.lstrip("_")} = ExtResource("<id>")  # missing')

    # ----- Reference to shader file -----
    if shader_exists and not use_standard_material:
        # Only create custom shaders for materials that aren't using StandardMaterial3D
        # Create shader file in proper Godot location (strip Resources/Brushes like materials)
        if shader_path.is_relative_to(BRUSH_ROOT):
            # Shader is under BRUSH_ROOT, use relative path from BRUSH_ROOT
            shader_relative_path = shader_path.relative_to(BRUSH_ROOT)
        else:
            # Shader is elsewhere (like Shaders/ folder), use relative path from ASSETS_ROOT
            shader_relative_path = shader_path.relative_to(ASSETS_ROOT)

        godot_shader_dir = OUTPUT_ROOT / shader_relative_path.parent
        godot_shader_path = godot_shader_dir / f"{shader_path.stem}.gdshader"

        # Create shader directory and convert shader (only if it doesn't exist yet)
        if not godot_shader_path.exists():
            godot_shader_dir.mkdir(parents=True, exist_ok=True)

            # Parse and convert Unity shader
            unity_shader_code = shader_path.read_text(encoding="utf-8", errors="ignore")
            converted_shader = convert_unity_shader_to_godot(unity_shader_code, shader_path.stem)

            shader_content = f"""// Converted from Unity shader: {shader_path.as_posix()}
// Auto-converted with some manual TODOs remaining

{converted_shader}
"""

            godot_shader_path.write_text(shader_content, encoding="utf-8")
            print(f"Created shared shader: {shader_relative_path.parent}/{shader_path.stem}.gdshader")

        # Reference the shader with proper relative path from material to shader
        try:
            relative_shader_path = godot_shader_path.relative_to(out_dir)
            shader_preload_path = str(relative_shader_path).replace("\\", "/")
        except ValueError:
            # If not in same tree, use absolute res:// path
            shader_res_path = str(godot_shader_path.relative_to(OUTPUT_ROOT)).replace("\\", "/")
            shader_preload_path = f"res://{shader_res_path}"

        # Register shader as ext_resource so it can be referenced from material
        # Even if commented out initially, this keeps the .tres valid when enabled
        try:
            shader_res_rel = godot_shader_path.relative_to(OUTPUT_ROOT)
            shader_res_path = f'res://{OUTPUT_ROOT.name}/{str(shader_res_rel).replace(chr(92), "/")}'
        except ValueError:
            shader_res_path = f'res://{OUTPUT_ROOT.name}/{str(godot_shader_path.name).replace(chr(92), "/")}'

        shader_id = str(next_ext_id)
        next_ext_id += 1
        ext_resources.append({
            "id": shader_id,
            "type": "Shader",
            "path": shader_res_path,
        })

        # Assign the shader immediately
        resource_lines.append(f'shader = ExtResource("{shader_id}")')
        resource_lines.append(f'# Original Unity shader: {shader_path.as_posix()}')
    elif use_standard_material and shader_path:
        resource_lines.append(f'\n# Replaced Unity shader with StandardMaterial3D: {shader_path.as_posix()}')

    # Build final .tres contents
    header = [f'[gd_resource type="{material_type}" format=3]']
    ext_lines = []
    for res in ext_resources:
        ext_lines.append(f'[ext_resource type="{res["type"]}" path="{res["path"]}" id="{res["id"]}"]')
    body = ["[resource]"] + resource_lines

    # Append shader parameters if we have any (for ShaderMaterial only)
    if material_type == "ShaderMaterial" and shader_params:
        for k, v in shader_params.items():
            body.append(f'shader_parameter/{k} = {v}')

    contents = []
    contents.extend(header)
    if ext_lines:
        contents.append("")
        contents.extend(ext_lines)
    contents.append("")
    contents.extend(body)

    (out_dir / f"{name}.tres").write_text("\n".join(filter(None, contents)), encoding="utf-8")



def main():
    guid_map = build_guid_map()
    print(f"Built GUID map with {len(guid_map)} entries")

    converted_count = 0
    error_count = 0
    missing_shaders = set()
    missing_textures = set()

    # Convert materials first
    for mat in BRUSH_ROOT.rglob("*.mat"):
        try:
            name, tex, floats, colors, shader_guid = load_mat(mat)
            out_dir = OUTPUT_ROOT / mat.relative_to(BRUSH_ROOT).parent


            # Validate shader exists
            if shader_guid and shader_guid not in guid_map:
                missing_shaders.add(shader_guid)

            # Validate textures exist
            for slot, env in tex.items():
                tex_guid = env["m_Texture"].get("guid")
                if tex_guid and tex_guid not in guid_map:
                    missing_textures.add(tex_guid)

            write_tres(name, tex, floats, colors, shader_guid, guid_map, out_dir)
            converted_count += 1
        except Exception as e:
            print(f"Error processing {mat}: {e}")
            error_count += 1


    # Summary report
    print(f"\nConversion Summary:")
    print(f"  Materials processed: {converted_count} (shaders and textures created alongside)")
    print(f"  Material errors: {error_count}")
    if missing_shaders:
        print(f"  Missing shaders: {len(missing_shaders)}")
    if missing_textures:
        print(f"  Missing textures: {len(missing_textures)}")
    print(f"  Output directory: {OUTPUT_ROOT}")
    print(f"\nThe '{OUTPUT_ROOT}' folder is now self-contained and ready to copy into a Godot project!")

if __name__ == "__main__":
    main()
