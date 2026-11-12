// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
using UnityEngine;
namespace TiltBrush
{
    // Imported meshes retain enough identity to be adopted by the runtime renderer.
    public sealed class VoxChunkTag : MonoBehaviour
    {
        public Vector3Int Coordinate;
        public long Signature;
    }
}
