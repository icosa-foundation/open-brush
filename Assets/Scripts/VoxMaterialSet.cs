// Copyright 2026 The Open Brush Authors
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
using System.Linq;
using TiltBrushToolkit;
using UnityEngine;

namespace TiltBrush
{
    internal sealed class VoxMaterialSet
    {
        private const string EmissiveBrushResource = "Brushes/Basic/Light/Light";

        public IReadOnlyList<Material> Materials { get; }
        public IReadOnlyList<int> PaletteSubmeshIndices { get; }
        public IReadOnlyList<Material> OwnedMaterials { get; }

        public VoxMaterialSet(RuntimeVoxDocument document, Material defaultMaterial)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }
            if (defaultMaterial == null)
            {
                throw new ArgumentNullException(nameof(defaultMaterial));
            }

            var materials = new List<Material> { defaultMaterial };
            var ownedMaterials = new List<Material>();
            var paletteSubmeshIndices = new int[256];
            var slotByProperties = new Dictionary<string, int>(StringComparer.Ordinal);
            var usedPaletteIndices = new HashSet<int>(
                document.Models.SelectMany(model => model.Voxels.Values).Select(value => (int)value));

            foreach (int paletteIndex in usedPaletteIndices.OrderBy(value => value))
            {
                if (!document.Materials.TryGetValue(
                        paletteIndex,
                        out RuntimeVoxDocument.RuntimeMaterial voxMaterial))
                {
                    continue;
                }

                string propertiesKey = string.Join(
                    "\n",
                    voxMaterial.Properties
                        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair => $"{pair.Key}={pair.Value}"));
                if (!slotByProperties.TryGetValue(propertiesKey, out int slot))
                {
                    Material material = CreateMaterial(voxMaterial);
                    slot = materials.Count;
                    slotByProperties.Add(propertiesKey, slot);
                    materials.Add(material);
                    ownedMaterials.Add(material);
                }
                paletteSubmeshIndices[paletteIndex - 1] = slot;
            }

            Materials = materials;
            PaletteSubmeshIndices = paletteSubmeshIndices;
            OwnedMaterials = ownedMaterials;
        }

        private static Material CreateMaterial(RuntimeVoxDocument.RuntimeMaterial voxMaterial)
        {
            Material template;
            switch (voxMaterial.Type)
            {
                case RuntimeVoxDocument.MaterialType.Glass:
                    template = TbtSettings.Instance.m_PbrBlendDoubleSided.material;
                    break;
                case RuntimeVoxDocument.MaterialType.Emit:
                    BrushDescriptor lightBrush = Resources.Load<BrushDescriptor>(EmissiveBrushResource);
                    template = lightBrush != null
                        ? lightBrush.Material
                        : TbtSettings.Instance.m_PbrOpaqueDoubleSided.material;
                    break;
                default:
                    template = TbtSettings.Instance.m_PbrOpaqueDoubleSided.material;
                    break;
            }

            var material = new Material(template)
            {
                name = $"VOX {voxMaterial.Type} {voxMaterial.PaletteIndex}",
            };

            float weight = Mathf.Clamp01(voxMaterial.Weight ?? 1f);
            float roughness = Mathf.Clamp01(voxMaterial.Roughness ?? 0.1f);
            if (material.HasProperty("_MetallicFactor"))
            {
                material.SetFloat(
                    "_MetallicFactor",
                    voxMaterial.Type == RuntimeVoxDocument.MaterialType.Metal ? weight : 0f);
            }
            if (material.HasProperty("_RoughnessFactor"))
            {
                material.SetFloat("_RoughnessFactor", roughness);
            }
            if (material.HasProperty("_BaseColorFactor"))
            {
                // The available transparent PBR material has no refraction or distance-based
                // absorption inputs. Approximate glass attenuation with opacity; the exact MATL
                // values remain available on RuntimeMaterial and preserved in the source chunk.
                float alpha = voxMaterial.Type == RuntimeVoxDocument.MaterialType.Glass
                    ? Mathf.Clamp(
                        1f - weight *
                            (1f - Mathf.Clamp01(voxMaterial.Attenuation ?? 0.15f)),
                        0.05f,
                        1f)
                    : 1f;
                material.SetColor("_BaseColorFactor", new Color(1f, 1f, 1f, alpha));
            }
            if (material.HasProperty("_EmissionGain"))
            {
                material.SetFloat(
                    "_EmissionGain",
                    Mathf.Clamp01(weight * Mathf.Max(0f, voxMaterial.Flux ?? 1f)));
            }
            return material;
        }
    }
}
