// Copyright 2024 The Open Brush Authors
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

using System;
using System.Collections.Generic;
using GLTF.Schema;
using UnityEngine;
using UnityGLTF;
using UnityGLTF.Plugins;

namespace TiltBrush
{
    public class OpenBrushMaterialImportPlugin : GLTFImportPlugin
    {
        public override string DisplayName => "Open Brush Material Import";
        public override string Description =>
            "Restores Open Brush brush materials when importing GLTFs exported by Open Brush.";
        public override bool EnabledByDefault => true;

        public override GLTFImportPluginContext CreateInstance(GLTFImportContext context)
        {
            return new OpenBrushMaterialImportContext();
        }
    }

    public class OpenBrushMaterialImportContext : GLTFImportPluginContext
    {
        // Maps material name → brush GUID so we can also fix the vertex-color variant later.
        // The vertex-color clone is created before OnAfterImportMaterial fires, so we can't
        // fix it there — we fix it in OnAfterImportScene by name-matching instead.
        private readonly Dictionary<string, Guid> _nameToGuid = new Dictionary<string, Guid>();

        public override void OnAfterImportMaterial(GLTFMaterial gltfMaterial, int materialIndex, Material materialObject)
        {
            if (BrushCatalog.m_Instance == null) return;

            var guidStr = gltfMaterial.Extras?.Value<string>("TB_BrushGuid");
            if (guidStr == null || !Guid.TryParse(guidStr, out var guid)) return;

            var brush = BrushCatalog.m_Instance.GetBrush(guid);
            if (brush == null) return;

            // Record for vertex-color variant lookup in OnAfterImportScene.
            // materialObject.name is already set to def.Name ("ob-{DurableName}") at this point.
            _nameToGuid[materialObject.name] = guid;

            ApplyBrushMaterial(brush, materialObject);
        }

        public override void OnAfterImportScene(GLTFScene scene, int sceneIndex, GameObject sceneObject)
        {
            if (BrushCatalog.m_Instance == null || sceneObject == null) return;
            if (_nameToGuid.Count == 0) return;

            // UnityGLTF assigns the vertex-color material variant to meshes that have COLOR_0,
            // which is all Open Brush brush meshes. That variant was cloned before
            // OnAfterImportMaterial fired, so its shader is still the PBR default.
            // Find those instances by name and apply the brush material to them too.
            foreach (var mr in sceneObject.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (var mat in mr.sharedMaterials)
                {
                    if (mat == null) continue;
                    if (!_nameToGuid.TryGetValue(mat.name, out var guid)) continue;

                    var brush = BrushCatalog.m_Instance.GetBrush(guid);
                    if (brush == null) continue;

                    // Skip if already using the correct shader (e.g. the base variant we fixed earlier)
                    if (mat.shader == brush.Material.shader) continue;

                    ApplyBrushMaterial(brush, mat);
                }
            }
        }

        private static void ApplyBrushMaterial(BrushDescriptor brush, Material target)
        {
            target.shader = brush.Material.shader;
            target.CopyPropertiesFromMaterial(brush.Material);
        }
    }
}
