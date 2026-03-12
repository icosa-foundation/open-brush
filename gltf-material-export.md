# GLTF Material Export — Brush Shader Analysis

## Complete shader reference

| Shader (declared name) | Has _Color | Has _TintColor | Has _MainTex | BlendMode | Animated | Special notes |
|---|---|---|---|---|---|---|
| `Brush/Particle/Bubbles` | NO | NO (code only) | YES | One One | YES | No color/tint in properties; particle with curl-noise displacement |
| `Brush/Special/CelVinyl` | YES | NO | YES | none (AlphaTest) | NO | Standard alpha-cutout |
| `Brush/Visualizer/RainbowTube` (ChromaticWave) | NO | NO | NO | One One + BlendOp Add,Min | YES | Fully procedural RGB waveform; _EmissionGain; audio-reactive |
| `Brush/Special/Comet` | NO | NO | YES + _AlphaMask | One One + BlendOp Add,Min | YES | Two textures (_MainTex + _AlphaMask); scrolling fire-like; _EmissionGain |
| `Brush/Special/DanceFloor` | NO | YES | YES | none (ZWrite On, opaque) | YES | Opaque; vertex-quantized geometry; time-driven color cycling; audio-reactive |
| `Brush/Special/DiamondHull` | NO | NO | YES | One One | YES | Thin-film Fresnel diffraction math; _MainTex as thickness lookup |
| `Brush/Disco` | YES | NO | YES + _BumpMap | none (opaque surface) | YES | Fake disco-ball hotspot; normal from world-space derivatives; audio-reactive |
| `Brush/Visualizer/Dots` | NO | YES | YES | One One + BlendOp Add,Min | YES | _TintColor + _EmissionGain + _BaseGain; particle with FFT displacement |
| `Brush/Special/DiffuseNoTextureDoubleSided` | YES | NO | NO | none (opaque Lambert) | NO | No _MainTex; pure vertex-color Lambert |
| `Brush/Special/DoubleTaperedMarker` | NO | NO | NO | none (Lighting Off) | NO | No _Color, no _MainTex; pure vertex color unlit |
| `Brush/Special/Electricity` | NO | NO | YES | One One + BlendOp Add,Min | YES | 3 passes with curl-noise; procedural inner line; _EmissionGain; audio-reactive |
| `Brush/Particle/Embers` | NO | YES | YES | One One + BlendOp Add,Min | YES | Particle with time-based fade/sparkle cycle; _TintColor |
| `Brush/Special/Fire` | NO | NO | YES | One One + BlendOp Add,Min | YES | Displacement-scroll fire; _Scroll1/_Scroll2; _EmissionGain |
| `Brush/Special/AdditiveCutout` (Highlighter) | NO | NO | YES | SrcAlpha One | NO | SrcAlpha One (different from most additive); alpha cutoff in code |
| `Brush/Special/HyperGrid` | NO | YES | YES | One One + BlendOp Add,Min | YES | Vertex quantization; _TintColor; audio-reactive |
| `Brush/Special/HypercolorDoubleSided` | YES | NO | YES + _BumpMap | none (AlphaTest) | YES | Procedural color cycling via sin() on texture.r; _BumpMap; audio-reactive |
| `Brush/Special/HypercolorSingleSided` | YES | NO | YES + _BumpMap | none (AlphaTest) | YES | Same as above, single-sided |
| `Brush/Special/LightWire` | YES | NO | YES + _BumpMap | none (opaque surface) | YES | "Christmas lights" — vertices extruded at light positions; RGB color cycling; _EmissionGain |
| `Brush/Special/Petal` | NO | NO | YES | none (Opaque) | NO | Diffuse from vertex color with AO simulation; _SpecColor but no _Color |
| `Brush/Special/Plasma` | NO | NO | YES | One One + BlendOp Add,Min | YES | Three-layer scrolling texture; 4 scroll params; _EmissionGain; audio-reactive |
| `Brush/Special/Rainbow` | NO | NO | YES | One One (Add,Min / Max,Min) | YES | Fully procedural rainbow bands from UV; _EmissionGain; dual LOD subshader |
| `Brush/Particle/Smoke` | NO | YES | YES | SrcAlpha One | YES | SrcAlpha One; _TintColor; curl-noise particle displacement |
| `Brush/Particle/Snow` | NO | YES | YES | SrcAlpha One | YES | SrcAlpha One; _TintColor; scrolling particle; audio-reactive |
| `Brush/Special/SoftHighlighter` | NO | NO | YES | SrcAlpha One | YES | SrcAlpha One; no Color/Tint; audio-reactive |
| `Brush/Particle/Stars` | NO | NO | YES | One One + BlendOp Add,Min | YES | Per-particle brightness modulated by sin(time); _SparkleRate |
| `Brush/Special/Streamers` | NO | NO | YES | One One + BlendOp Add,Min | YES | 5-row scrolling UV; _EmissionGain; audio-reactive |
| `Brush/Special/Toon` | NO | NO | YES | none (opaque, 2 passes) | YES | Two-pass toon outline; _OutlineMax; Cull Front outline pass; audio-reactive |
| `Brush/Special/TubeToonInverted` | NO | NO | YES | none (opaque, 2 passes) | NO | Inverted toon (inner pass renders black); scale-aware outline |
| `Brush/Special/VelvetInk` | NO | NO | YES | SrcAlpha One | YES | SrcAlpha One; audio-reactive |
| `Brush/Visualizer/Waveform` | NO | NO | YES | One One + BlendOp Add,Min | YES | Procedural waveform tube; _EmissionGain; audio-reactive |
| `Brush/Visualizer/WaveformFFT` | NO | NO | YES | One One + BlendOp Add,Min | YES | FFT-driven bar display; _EmissionGain; audio-reactive |
| `Brush/Visualizer/WaveformPulse` (NeonPulse) | NO | NO | NO | One One (surface) | YES | No _MainTex; procedural neon pulse via fmod(time); _EmissionGain |
| `Brush/Visualizer/WaveformTube` | NO | NO | YES | One One + BlendOp Add,Min | YES | Waveform-driven UV scroll; _EmissionGain; audio-reactive |
| `Brush/Special/WigglyGraphiteDoubleSided` | NO | NO | YES | none (AlphaTest) | YES | Flipbook animation (6 frames) driven by time; audio-reactive |
| `Brush/Special/WigglyGraphiteSingleSided` | NO | NO | YES + _SecondaryTex | none (AlphaTest) | YES | Flipbook + _SecondaryTex for color; alpha from _MainTex |
| `Brush/Special/Wireframe` | NO | NO | NO | One One | YES | No _MainTex; UV-based wireframe box; audio-reactive |
| `Brush/Visualizer/WaveformParticles` | NO | YES | YES | One One + BlendOp Add,Min | YES | Particle with curl-noise driven by lifetime; _TintColor |
| `Brush/Additive` | NO | NO | YES | One One + BlendOp Add,Min | YES | Generic additive; audio-reactive |
| `Brush/Bloom` | NO | NO | YES | One One (Add,Min / Max,Min) | YES | _EmissionGain; dual LOD subshader |
| `Brush/DiffuseDoubleSided` | YES | NO | YES | none (AlphaTest) | NO | Standard double-sided Lambert |
| `Brush/DiffuseOpaqueDoubleSided` | YES | NO | NO | none (opaque) | NO | No _MainTex; opaque vertex color + _Color Lambert |
| `Brush/DiffuseOpaqueSingleSided` | YES | NO | NO | none (opaque) | NO | No _MainTex; opaque vertex color + _Color Lambert, single-sided |
| `Brush/DiffuseSingleSided` | YES | NO | YES | none (AlphaTest) | NO | Standard single-sided Lambert |
| `Brush/Special/AdditiveScrolling` | NO | YES | YES | SrcAlpha One | YES | SrcAlpha One; _TintColor; scrolling with view-angle rim falloff |
| `Brush/Special/Faceted` | NO | NO | YES | none (opaque) | NO | Face normal from ddx/ddy; _ColorX/_ColorY/_ColorZ axis-blended shading |
| `Brush/StandardDoubleSided` | YES | NO | YES + _BumpMap | none (AlphaTest) | NO | Full Standard Specular + bump; many LOD fallback subshaders |
| `Brush/StandardSingleSided` | YES | NO | YES + _BumpMap | none (AlphaTest) | NO | Same as above, single-sided |
| `Brush/Standard Wireframe` | YES | NO | YES + _BumpMap | none (AlphaTest) | NO | Adds barycentric wireframe overlay; _VertOrder param |
| `Brush/Special/Unlit` | NO | NO | YES | none (AlphaTest, Lighting Off) | NO | Unlit; alpha cutout |
| `Brush/Multiplicative` | NO | NO | YES | DstColor Zero | NO | UNIQUE: multiply blend; darkens scene |
| `Blocks/BlocksGem` | YES | NO | NO | One SrcAlpha | NO | No _MainTex; Voronoi noise procedural gem; unusual blend One SrcAlpha |
| `Blocks/BlocksGlass` | YES | NO | NO | One SrcAlpha | NO | No _MainTex; specular + rim lighting; unusual blend One SrcAlpha |
| `Blocks/Basic` (BlocksPaper) | YES | NO | NO | none (opaque) | NO | No _MainTex; StandardSpecular with vertex color × _Color |

---

## Summary by category

### No _Color AND no _TintColor — white BaseColorFactor is correct (vertex color carries the actual stroke color)
`Fire`, `Electricity`, `Stars`, `Streamers`, `Toon`, `TubeToonInverted`, `Petal`, `Highlighter`,
`SoftHighlighter`, `VelvetInk`, `DiamondHull`, `Wireframe`, `NeonPulse`, `ChromaticWave`,
`Waveform*`, `WigglyGraphite`, `Unlit`, `Faceted`, `Bubbles`, `Comet`, `Plasma`, `Rainbow`,
`DoubleTaperedMarker`

### No _MainTex — texture-free, nothing to export
`DoubleTaperedMarker`, `DiffuseNoTextureDoubleSided`, `DiffuseOpaqueSingleSided/DoubleSided`,
`NeonPulse`, `ChromaticWave`, `Wireframe`, `BlocksPaper/Gem/Glass`

### Purely procedural / animated — best effort only, animation is lost
`ChromaticWave`, `NeonPulse`, `Wireframe`, `Electricity`, `Rainbow`, `Plasma`, `Streamers`,
`Waveform`, `WaveformFFT`, `WaveformTube`, `Stars`, `DanceFloor`, `Hypercolor*`, `LightWire`,
`WigglyGraphite` (flipbook), `DiamondHull` (diffraction), `Bubbles`, `Embers`, `Smoke`, `Fire`, `Comet`

---

## Genuinely problematic cases

| Brush | Problem | Best-effort / TODO |
|---|---|---|
| **Multiplicative** | `DstColor Zero` — no GLTF equivalent, `EXT_blend_operations` doesn't cover multiply | Add `Extras["TB_BlendMode"] = "multiply"` so importers can detect it |
| **BlocksGem / BlocksGlass** | `One SrcAlpha` (pre-multiplied alpha) — we set `AlphaMode.BLEND` which is close but not exact | Minor inaccuracy, probably acceptable |
| **DanceFloor** | ZWrite On / opaque despite being a "special" brush — verify `m_BlendMode` on descriptor | Check BrushDescriptor asset |
| **Comet** | `_AlphaMask` modulates alpha on top of `_MainTex`; we only export `_MainTex` | Could bake `_AlphaMask` into alpha channel of exported base color texture |
| **WigglyGraphiteSingleSided** | `_MainTex` is alpha-only; `_SecondaryTex` provides color — we export `_MainTex` as base color which is wrong | Export `_SecondaryTex` as `BaseColorTexture` for this variant; double-sided variant is fine |
| **Smoke / Snow / AdditiveScrolling** | `SrcAlpha One` ≠ `One One` (standard additive) — we apply `EXT_blend_operations.Add` for all `AdditiveBlend` | Verify `m_BlendMode` on descriptors; check if `EXT_blend_operations` should distinguish |

---

## Open questions
1. **Multiplicative brush** — add `Extras["TB_BlendMode"]` flag, or leave as-is?
2. **Comet/_AlphaMask** — worth baking, or skip?
3. **WigglyGraphite single-sided** — worth special-casing given double-sided is more common?
4. **Smoke/Snow blend modes** — verify their `m_BlendMode` descriptor values match actual shader blend equations?
