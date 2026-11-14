# Standard glTF material fallbacks for brush exports

## Problem

Some Open Brush shaders are not recognised by UnityGLTF's standard material
exporter. Their exported materials can consequently contain only a name:

```json
{"name": "ob-MatteHull"}
```

Gallery Viewer recognises this brush identity and installs its own brush shader.
Generic glTF viewers do not know that convention and use glTF material defaults.
In particular, omitted `metallicFactor` defaults to 1, which makes a matte brush
behave as metal. Roughness already defaults to 1.

The recent export identity fix is separate: the export plugin associates each
batch or stroke's actual brush descriptor with its primary material, avoiding
shader-prefix and material-name guesses. Standard fallbacks must retain that
identity so brush-aware viewers can continue replacing the approximation.

## Goal

Export a reasonable standard glTF material alongside the brush identity, using
vertex colours, normals, and texture coordinates already exported with the mesh.
Generic viewers should get a useful approximation without requiring Open Brush
shader support. Exact reproduction of every brush effect is not the goal.

## Implementation location

Implement the brush fallback policy in
`Assets/Scripts/UnityGLTF Plugins/OpenBrushExportPlugin.cs`, in or called from
`AfterMaterialExport()` after resolving the actual brush descriptor.

UnityGLTF should remain responsible for standard glTF serialization. The Open
Brush plugin supplies brush-specific approximation policy. This does not require
changes to Gallery Viewer or renaming shaders in the Unity Tools package.

## Proposed policy

1. Audit which brushes already export usable standard material properties and
   which produce incomplete materials. Preserve correctly exported properties.
2. Define shared fallback categories rather than writing a separate exporter for
   every brush. Candidate categories are matte, shiny, unlit, emissive, and
   transparent. These are design categories, not yet an approved mapping.
3. Assign each brush to a category using explicit brush-descriptor policy. Do not
   infer the category from shader-name prefixes or the presence of a material name.
4. Allow individual overrides where a category cannot describe the brush well.
5. Preserve the `ob-<DurableName>` identity and existing backface/blend handling.
6. Apply the policy only to identified brush materials. Imported model materials
   and overlay passes require their own treatment.

All brushes should have a usable standard fallback, but only incomplete exports
need additional properties from this policy. A shared shader does not imply that
all brushes using it have identical export requirements.

## First case: MatteHull

For MatteHull, supply a white base colour factor, non-metallic surface, and high
roughness. White leaves the exported vertex colours unchanged. The minimal
explicit metallic/roughness repair is:

```json
{
  "name": "ob-MatteHull",
  "pbrMetallicRoughness": {
    "metallicFactor": 0,
    "roughnessFactor": 1
  }
}
```

This is a standard approximation, not a replacement for the brush shader.
Do not apply the MatteHull settings unconditionally to shiny, emissive, or
transparent brushes.

## Other categories to investigate

1. Shiny: choose metallic and roughness values from intended appearance; shininess
   alone does not imply a metallic surface.
2. Unlit: use `KHR_materials_unlit` when appropriate, preserving vertex colours.
3. Emissive: establish suitable emissive factors/textures and any necessary
   extension support. Standard glTF cannot reproduce every animated effect.
4. Transparent: preserve alpha mode, cutoff, textures, and sidedness. Additive
   blending has no exact core glTF equivalent; document the chosen approximation.
5. Textured: verify texture references, sampler settings, UVs, and colour-space
   interpretation before supplying or overriding texture properties.

## Validation

1. Re-export `kouji_bug.tilt` as a representative mixed-brush fixture.
2. Inspect material definitions to confirm brush identities survive and matte
   materials explicitly export `metallicFactor: 0`.
3. Compare in a generic glTF viewer and in Gallery Viewer. The former should use
   the approximation; the latter should retain its recognised brush rendering.
4. Check representative matte, shiny, unlit, emissive, textured, and transparent
   brushes individually before extending the category mapping.
5. Include imported models and brushes with overlay passes to detect unintended
   changes outside primary brush materials.

## Status

Design only. The brush-identity association has been implemented separately and
verified in a fresh GLB export. Standard fallback policy and category mappings
have not been implemented or visually validated.
