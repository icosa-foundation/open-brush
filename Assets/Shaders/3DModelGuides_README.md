# 3D Model Custom Guides/Stencils

This feature enables imported 3D models to be used as custom guides/stencils in Open Brush, allowing artists to paint precisely along complex 3D surfaces.

## Overview

The system uses a runtime 3D Jump Flood Algorithm (JFA) to generate distance fields from imported meshes. This enables fast surface queries for brush attraction without expensive per-frame mesh collision checks, making it suitable for mobile VR platforms.

## Architecture

### Core Components

1. **JumpFlood3D.compute** - GPU compute shader that implements the 3D JFA algorithm
2. **DistanceField3D.cs** - Manager component that orchestrates distance field generation
3. **ModelStencil.cs** - Stencil widget that uses distance fields for surface queries
4. **ModelWidget.ConvertToStencil()** - Conversion method to create stencils from models

### How It Works

1. **Voxelization Phase** (CPU Job System)
   - Meshes are voxelized into seed points on a 3D grid
   - Runs asynchronously using Unity's Job System
   - Generates initial seed data for surface voxels

2. **JFA Phase** (GPU Compute)
   - Spread over multiple frames to avoid stalls (~10-12 frames)
   - Each frame executes one JFA iteration
   - Progressively propagates distance information across the volume

3. **Surface Queries** (Runtime)
   - Fast lookups in the 3D texture for nearest surface point
   - Gradient-based normal estimation
   - Falls back to mesh collider if distance field not ready

## Usage

### Via Lua API

```lua
-- Import a 3D model
local model = Model:Import("MyModel.obj")

-- Convert it to a stencil
local stencil = model:ConvertToStencil()

-- The model is now a guide that brushes will snap to!
```

### Via C# API

```csharp
// Get or create a ModelWidget
ModelWidget modelWidget = GetComponent<ModelWidget>();

// Convert to stencil
ModelStencil stencil = modelWidget.ConvertToStencil();

// Optionally provide a custom compute shader
ComputeShader customShader = Resources.Load<ComputeShader>("MyCustomJFA");
ModelStencil stencil = modelWidget.ConvertToStencil(customShader);
```

### Direct Creation

```csharp
// Create directly from a Model
Model model = ModelCatalog.m_Instance.GetModel("mymodel.obj");
ModelStencil stencil = ModelStencil.CreateFromModel(model);
```

## Configuration

### Grid Resolution

Edit the `m_GridSize` field in DistanceField3D.cs:

```csharp
[SerializeField] private Vector3Int m_GridSize = new Vector3Int(64, 64, 64);
```

**Recommendations:**
- **Mobile VR**: 32³ to 48³ (memory-constrained)
- **Desktop VR**: 64³ to 96³ (balanced)
- **High-end PC**: 128³+ (high quality)

### Voxelization Threshold

Controls how close voxels must be to the surface to be marked as seeds:

```csharp
[SerializeField] private float m_VoxelizationThreshold = 0.5f;
```

Lower values = thinner surface layer, more precise
Higher values = thicker surface layer, more forgiving

## Performance Characteristics

### Memory Usage
- 64³ volume with RGBAFloat = ~4MB per texture × 3 = ~12MB total
- 128³ volume = ~96MB total

### Generation Time
- Voxelization: 1-2 frames (CPU job)
- JFA: 6-7 iterations (log₂(max dimension))
- Total: ~10-12 frames at 90 FPS = ~110-130ms

### Runtime Performance
- Distance field lookups: O(1) texture sample
- Much faster than per-frame mesh collision queries
- Suitable for mobile VR (Quest 2, Quest 3)

## Limitations

### Current Implementation
1. **Uniform scaling only** - Non-uniform extents not yet supported
2. **Static topology** - Mesh changes require full rebuild
3. **Memory overhead** - Each stencil allocates ~12-96MB
4. **Normal estimation** - Uses gradient approximation (not exact mesh normals)

### Future Improvements
- [ ] Adaptive resolution based on mesh complexity
- [ ] Compression for sparse volumes
- [ ] GPU voxelization for faster initialization
- [ ] Signed distance fields for interior/exterior detection
- [ ] Multi-resolution cascades for large models

## Technical Details

### JFA Algorithm

The Jump Flood Algorithm is an efficient GPU-parallel method for computing Voronoi diagrams and distance transforms:

1. Initialize seeds at surface voxels
2. Each iteration, sample neighbors at distance `step`
3. Propagate closest seed information
4. Halve `step` each iteration (log steps total)

### Data Encoding

Each voxel stores a `float4`:
- `xyz` = seed position (voxel coordinates)
- `w` = squared distance to seed (`-1` = no seed)

Distance is stored squared to avoid expensive sqrt operations during JFA.

### Compute Shader Kernels

- **ClearVolume** - Initialize volume to empty state
- **InitializeSeeds** - Optional GPU-based seed initialization
- **JumpFlood** - Main JFA iteration kernel

## Examples

### Example 1: Dragon Model Stencil

```lua
-- Import the Stanford Dragon
local dragon = Model:Import("dragon.obj")

-- Position it in the scene
dragon.position = Vector3(0, 1, 0)
dragon.scale = 2.0

-- Convert to stencil
local dragonGuide = dragon:ConvertToStencil()

-- Now paint along the dragon's surface!
-- Brushes will automatically snap to the dragon geometry
```

### Example 2: Architectural Guide

```lua
-- Import building model
local building = Model:Import("architecture.glb")

-- Convert to stencil for precise painting
local buildingGuide = building:ConvertToStencil()

-- Paint details that follow the building's geometry
```

## Troubleshooting

### "Distance field not ready"
- The system is still generating the field
- Wait 10-12 frames (~100-130ms)
- Distance queries will fall back to mesh collider

### Poor surface accuracy
- Increase grid resolution (e.g., 64³ → 96³)
- Decrease voxelization threshold
- Ensure model has good topology

### High memory usage
- Decrease grid resolution
- Use fewer simultaneous model stencils
- Consider model LOD for stencil purposes

### Slow generation
- Reduce mesh complexity before conversion
- Use simpler collision meshes
- Profile voxelization job performance

## References

1. Jump Flooding in GPU with Applications to Voronoi Diagram and Distance Transform
   Guodong Rong, Tiow-Seng Tan (2006)

2. GPU-Accelerated Distance Fields
   Christopher Klose, Thomas Ertl (2013)

3. Unity Job System Documentation
   https://docs.unity3d.com/Manual/JobSystem.html

4. Unity Compute Shader Documentation
   https://docs.unity3d.com/Manual/ComputeShaders.html

## License

Copyright 2025 The Open Brush Authors

Licensed under the Apache License, Version 2.0
