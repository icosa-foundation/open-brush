# Brush Export Baking Plan

## Goal

Export custom Open Brush materials to glTF on a best-effort basis by combining the existing brush mesh baker with selective texture-map baking. Generated textures must only represent shader behavior that remains after vertex-stage deformation has been baked into the exported mesh.

Vertex color is part of the exported mesh and is expected to multiply the glTF base color. It must therefore be white while generating reusable texture maps, and it is not by itself a reason to bake a texture.

## Export model

For each brush GUID:

1. Bake supported vertex-stage deformation into the mesh.
2. Preserve the vertex attributes needed by the exported material, including color, normals, and supported UV channels.
3. Export existing material textures and factors using the correct UnityGLTF map types.
4. Bake only residual fragment-stage behavior that can be represented by a reusable 2D texture.
5. Apply an explicit best-effort fallback when the remaining behavior depends on geometry, view direction, multiple render passes, animation, audio, or unsupported vertex attributes.

The policy must be keyed by brush GUID. Shader-name matching and the absence of a recognized base-color texture are not sufficient evidence that a texture bake is useful.

## Texture bake policies

Each brush can select one of these policies independently of whether it has a mesh compute shader:

| Policy | Behavior |
| --- | --- |
| `None` | Do not generate a texture. Mesh data, vertex color, existing maps, and material factors are sufficient. |
| `UvBaseColor` | Bake a static UV-driven base-color modulation on a controlled 0-1 mesh. |
| `UvUnlit` | Bake a UV-driven luminous/additive approximation as unlit base color so vertex color remains effective. |
| `UvEmission` | Bake emission only when it does not require multiplication by vertex color. |
| `Unsupported` | Do not generate a misleading texture; retain the closest ordinary glTF material and log the omitted behavior. |

The initial default is `None`. Texture baking is enabled only for brushes that have been reviewed and explicitly classified.

## Initial classifications

| Brush | Mesh bake | Texture policy | Reason |
| --- | --- | --- | --- |
| Double Tapered Marker | Existing taper bake | `None` | Fragment output is vertex color. A generated white texture is a no-op. |
| Double Tapered Flat | Existing taper bake | `None` | Remaining `_Color * vertexColor` is represented by base-color factor and vertex color. |
| Electricity | Existing static curl bake | `UvUnlit` | The centre-line profile is driven by UV.y. Three independently displaced additive passes collapse to a single-strand best effort. |
| Neon Pulse | No animation target | `UvUnlit` | Bake a fixed-phase grayscale pulse from UV.x. Ignore view-dependent attenuation; vertex color supplies hue. |
| Petal | No required mesh bake identified | `UvBaseColor` best effort | UV gradient is bakeable, but front/back differences cannot be represented by one double-sided glTF material. Bake the front-face result. |
| Disco | Existing deformation bake | `Unsupported` | Emissive hot spot depends on derivatives of the real world-space surface. Preserve ordinary maps and PBR factors only. |
| Diamond Hull | None identified | `Unsupported` | Appearance depends on view direction, world position, normals, Fresnel, and diffraction. Use a smooth/specular approximation. |
| Faceted | None identified | `Unsupported` | Color depends on derivatives of the real face position and orientation. |
| Tube Toon Inverted | None identified | `Unsupported` | Outline requires a second normal-inflated geometry pass. |

This table is a starting point, not a complete brush inventory. Every exportable brush must be reviewed and added before texture baking is enabled for it.

## Controlled texture-bake mesh

Replace the implicit `Graphics.Blit` quad with an explicitly rendered mesh containing:

- UV0 spanning 0-1.
- White vertex color.
- Defined normals and tangents.
- Known triangle winding.
- A neutral transform and camera.
- An explicitly selected shader pass.
- Optional front- and back-facing validation renders for two-sided shaders.

The bake must render to a linear intermediate texture and export using the destination glTF map type. Base color and emission require their corresponding color-space export settings; normal maps require UnityGLTF's normal-channel conversion.

## Implementation phases

### Phase 1: Policy foundation and no-op prevention

- Add a brush-GUID export policy adjacent to the existing mesh-baker mappings.
- Carry the selected brush policy from mesh processing to material export.
- Remove the blanket rule that bakes every unhandled Brush/Blocks shader without a texture.
- Classify Double Tapered Marker and Double Tapered Flat as `None`.
- Use a unique `[OB_GLTF_BAKE]` prefix for policy and bake diagnostics.

### Phase 2: Controlled UV renderer

- Build and render an explicit 0-1 UV mesh with white vertex color and neutral attributes.
- Keep texture transform selection separate from UnityGLTF map-type selection.
- Export generated base-color maps as base color and normal maps as normal maps.
- Leave `BaseColorFactor` white only after a successful bake that already includes the material factor.
- Add deterministic visibility and render-failure validation without using pixel content as the policy decision.

### Phase 3: First supported brushes

- Implement and verify the Electricity single-strand approximation.
- Implement Neon Pulse as a fixed-phase unlit pulse without view-dependent rim attenuation.
- Evaluate Petal's front-face UV gradient and document its back-face limitation.

### Phase 4: Full inventory

- Review every brush descriptor, shader, existing texture, and mesh-baker compute mapping.
- Record whether vertex deformation is baked and which fragment inputs remain.
- Add explicit policies only after comparing the original Unity render with the exported glTF.
- Treat animation and audio-reactive behavior as out of scope.

### Phase 5: Verification

- Compile after each isolated change.
- Export a representative sketch containing every classified brush.
- Inspect the current Unity Editor log using the `[OB_GLTF_BAKE]` prefix and compare timestamps with the current clock.
- Validate generated GLB materials, image color spaces, normal conversion, vertex colors, UVs, and visibility.
- Visually compare Unity and glTF renders, recording accepted best-effort differences.

## Commit boundaries

Keep fixes independently reviewable:

1. Document the brush export baking plan.
2. Add brush-GUID texture bake policies and no-op classifications.
3. Add the controlled UV bake renderer.
4. Add each supported brush approximation separately where practical.
5. Add verification or diagnostics separately from behavioral changes.

