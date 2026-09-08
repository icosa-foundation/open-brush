// Copyright 2023 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#if FUSION_WEAVER

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenBrush.Multiplayer
{
    public static class NetworkingConstants
    {
        // Maximum capacity for NetworkedStroke's NetworkArrays
        public const int MaxControlPointsPerChunk = 10;

        // each control point is 36 bytes:

        // public Vector3 m_Pos 12 bytes (3 floats x 4 bytes each)
        // public Quaternion m_Orient 16 bytes (4 floats x 4 bytes each)
        // public float m_Pressure 4 bytes
        // public uint m_TimestampMs 4 bytes

        // Given the maximum payload size of 512 bytes for Fusion RPCs
        // (let's assume approximately 50 bytes for overhead)
        // we can fit 12 control points in a single RPC 

    }
}

#endif // FUSION_WEAVER
