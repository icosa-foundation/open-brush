// Copyright 2021 The Tilt Brush Authors
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

namespace TiltBrush
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public class BrushLister : MonoBehaviour
    {
        private static StringBuilder brushList;
        private static List<Guid> deprecated;
        private static TiltBrushManifest brushManifest;
        private static TiltBrushManifest brushManifestX;

        [MenuItem("Open Brush/Info/Brush Lister")]
        static void ListBrushes()
        {
            brushList = new StringBuilder();

            Object[] defaultBrushes = Resources.LoadAll("Brushes", typeof(BrushDescriptor)).ToArray();
            var experimentalBrushes = Resources.LoadAll("X/Brushes", typeof(BrushDescriptor)).ToArray();

            brushManifest = AssetDatabase.LoadAssetAtPath<TiltBrushManifest>("Assets/Manifest.asset");
            brushManifestX = AssetDatabase.LoadAssetAtPath<TiltBrushManifest>("Assets/Manifest_Experimental.asset");

            deprecated = new List<Guid>();

            foreach (BrushDescriptor b in defaultBrushes)
            {
                if (b.m_Supersedes != null) deprecated.Add(b.m_Supersedes.m_Guid);
            }
            foreach (BrushDescriptor b in experimentalBrushes)
            {
                if (b.m_Supersedes != null) deprecated.Add(b.m_Supersedes.m_Guid);
            }

            foreach (BrushDescriptor brush in defaultBrushes) { AppendValidBrushString(brush, false); }
            foreach (BrushDescriptor brush in experimentalBrushes)
            {
                try { AppendValidBrushString(brush, true); }
                catch (Exception UnassignedReferenceException)
                {
                    Debug.Log($"Experimental brush loading error: {UnassignedReferenceException}");
                }
            }

            Debug.Log($"{brushList}");
        }

        public static string getBrushRowString(BrushDescriptor brush, bool experimental)
        {
            // Exclude legacy brushes
            if (brush.m_SupersededBy != null) return "";
            var brushScripts = sniffBrushScript(brush);
            string prefabName = brush.m_BrushPrefab != null ? brush.m_BrushPrefab.name : "";
            string materialName = brush.Material != null ? brush.Material.name : "";
            string shaderName = brush.Material != null ? brush.Material.shader.name : "";
            string manifest = "";
            if (brushManifest.Brushes.Contains(brush) && brushManifestX.Brushes.Contains(brush))
            {
                manifest = "both";
            }
            else if (brushManifest.Brushes.Contains(brush))
            {
                manifest = "Default";
            }
            else if (brushManifestX.Brushes.Contains(brush))
            {
                manifest = "Experimental";
            }
            return $"{brush.Description}\t{brush.m_AudioReactive}\t{prefabName}\t{materialName}\t{shaderName}\t{brushScripts}\t{experimental}\t{brush.m_SupersededBy}\t{manifest}";
        }

        public static string sniffBrushScript(BrushDescriptor brush)
        {
            GameObject prefab = brush.m_BrushPrefab;
            if (prefab == null) return "";
            var componentNames = new List<string>();
            foreach (MonoBehaviour c in prefab.GetComponents<MonoBehaviour>())
            {
                if (c.GetType() == typeof(MeshFilter) || c.GetType() == typeof(Transform) || c.GetType() == typeof(MeshRenderer)) continue;
                componentNames.Add(c.GetType().ToString().Replace("TiltBrush.", ""));
            }
            return string.Join(",", componentNames);
        }

        public static void AppendValidBrushString(BrushDescriptor brush, bool experimental)
        {
            if (deprecated.Contains(brush.m_Guid)) return;
            var rowString = getBrushRowString(brush, experimental);
            if (rowString != "") brushList.AppendLine($"{rowString}");
        }

    }
}