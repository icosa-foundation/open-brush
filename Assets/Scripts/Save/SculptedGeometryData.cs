// Copyright 2022 Chingiz Dadashov-Khandan
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

using UnityEngine;
using System.Collections.Generic;

namespace TiltBrush {

/// Class for storing geometry data (verts and normals) of sculpted strokes for
/// serialization purposes.
public class SculptedGeometryData { 
    // This is just POD and the whole class should be a struct, but my crappy
    // implementation needs it to be nullable for now.
    // CTODO: make this less abominable
    public List<Vector3> vertices;
    public List<Vector3> normals;

    public SculptedGeometryData(List<Vector3> vertices, List<Vector3> normals) {
        this.vertices = vertices;
        this.normals = normals;
    }
}
} // namespace TiltBrush

