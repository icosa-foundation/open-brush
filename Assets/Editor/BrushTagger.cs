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

using System.Collections.Generic;
using System.Linq;
using UnityEditor;


namespace TiltBrush
{
    public class BrushTagger
    {
        [MenuItem("Open Brush/Rewrite Brush Tags")]
        private static void TagBrushes()
        {
            var whiteboardBrushes = new List<string>
            {
                "Marker",
                "TaperedMarker",
                "SoftHighlighter",
                "CelVinyl",
                "Dots",
                "Icing",
                "Toon",
                "Wire",
                "MatteHull",
                "ShinyHull",
                "UnlitHull",
            };

            TiltBrushManifest brushManifest = AssetDatabase.LoadAssetAtPath<TiltBrushManifest>("Assets/Manifest.asset");
            TiltBrushManifest brushManifestX = AssetDatabase.LoadAssetAtPath<TiltBrushManifest>("Assets/Manifest_Experimental.asset");

            var guids = AssetDatabase.FindAssets("t:BrushDescriptor");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                BrushDescriptor brush = AssetDatabase.LoadAssetAtPath<BrushDescriptor>(path);

                brush.m_Tags = new List<string>();

                EditorUtility.SetDirty(brush);

                if (whiteboardBrushes.Contains(brush.DurableName)) brush.m_Tags.Add("whiteboard");
                if (brushManifest.Brushes.Contains(brush)) brush.m_Tags.Add("default");
                if (brushManifestX.Brushes.Contains(brush)) brush.m_Tags.Add("experimental");
                if (brush.m_AudioReactive) brush.m_Tags.Add("audioreactive");

                if (brush.m_BrushPrefab == null) continue;
                if (brush.m_BrushPrefab.GetComponent<HullBrush>() != null) brush.m_Tags.Add("hull");
                if (brush.m_BrushPrefab.GetComponent<GeniusParticlesBrush>() != null) brush.m_Tags.Add("particle");
                if (brush.m_BrushPrefab.GetComponent<ParentBrush>() != null) brush.m_Tags.Add("broken");
            }
        }
    }
}
