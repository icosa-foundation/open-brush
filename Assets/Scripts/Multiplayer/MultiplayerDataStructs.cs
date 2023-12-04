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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenBrush.Multiplayer
{
    [System.Serializable]
    public struct PlayerRigData
    {
        public Vector3 HeadPosition;
        public Quaternion HeadRotation;
        public Vector3 HeadScale;

        public Vector3 ToolPosition;
        public Quaternion ToolRotation;

        public BrushData BrushData;
        public ExtraData ExtraData;

    }

    [System.Serializable]
    public struct BrushData
    {
        public Color Color;
        public string Guid;
        public float Size;
    }

    [System.Serializable]
    public struct ExtraData
    {
        public ulong OculusPlayerId;
    }
}
