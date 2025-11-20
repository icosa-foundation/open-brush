# SDFMeshAssets Folder

This folder contains Signed Distance Field (SDF) assets generated from 3D models for use as custom painting guides/stencils in Open Brush.

## How to Generate SDFMeshAssets

1. **Import your 3D model** into Unity (OBJ, FBX, GLTF, etc.)
2. **Open the IsoMesh generator**: `Tools > Mesh to SDF`
3. **Configure settings**:
   - **Mesh**: Drag your model's mesh from the Project window
   - **Size**: Resolution of the SDF (128 is good balance, 256 for high quality)
   - **Padding**: Extra space around mesh bounds (0.2 default)
4. **Click "Generate"** to create the SDF
5. **Click "Save"** to save the asset
6. **Move the asset** from `Assets/Data/SDFMeshes/` to this folder: `Assets/Resources/SDFMeshAssets/`

## Naming Convention

ModelStencil will automatically find SDFMeshAssets if they follow this naming pattern:
- `SDFMesh_{ModelName}_{Size}.asset` (e.g., `SDFMesh_Dragon_128.asset`)
- OR: `SDFMesh_{ModelName}.asset` (without size suffix)

Where `{ModelName}` matches your model's filename (without extension).

## Example

For a model named `Sphere.obj`:
1. Generate SDF via Tools > Mesh to SDF
2. Save as `SDFMesh_Sphere_128.asset`
3. Move to `Assets/Resources/SDFMeshAssets/`
4. ModelStencil will automatically find it when using the Sphere model

## Manual Assignment

You can also manually assign an SDFMeshAsset in the Inspector:
- Select the ModelStencil prefab or instance
- Drag the SDFMeshAsset into the "SDF Mesh Asset" field

## Performance Notes

- **Size 128**: Good for most models, ~2MB per asset
- **Size 64**: Lower quality, faster generation, ~250KB per asset
- **Size 256**: High quality, slower generation, ~16MB per asset

Higher-poly models (>20K triangles) REQUIRE an SDFMeshAsset - the MeshCollider fallback only works for low-poly models.
