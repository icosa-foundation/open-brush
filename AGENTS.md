# Repository Guidelines

- For every brush with noticeable visual differences from the old Unity version, excluding surface shaders, copy the old Unity shader and make only the minor changes required to support URP.
- For brush visual-parity work and screenshot analysis, use the normal `m_Material` path and ignore `m_TestingMaterial`. Do not infer that `m_TestingMaterial` is the active material, a URP migration target, or suitable for repurposing. Verify the material actually used at runtime when material selection is relevant.
