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
using UnityEngine;

namespace TiltBrush
{
    internal sealed class VoxMaterialSet
    {
        // Approximately the Light brush's usual output: 2 * exp(0.45 * 10).
        private const float MaxEmissionIntensity = 180f;

        public IReadOnlyList<Material> Materials { get; }
        public IReadOnlyList<int> PaletteSubmeshIndices { get; }
        public IReadOnlyList<bool> PaletteOccludesFaces { get; }
        public IReadOnlyList<Material> OwnedMaterials { get; }

        public VoxMaterialSet(RuntimeVoxDocument document, Material defaultMaterial,
            IEnumerable<byte> additionalPaletteIndices = null)
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
            var paletteOccludesFaces = Enumerable.Repeat(true, 256).ToArray();
            var slotByProperties = new Dictionary<string, int>(StringComparer.Ordinal);
            var usedPaletteIndices = new HashSet<int>(document.Materials.Keys);
            if (additionalPaletteIndices != null)
                foreach (byte index in additionalPaletteIndices) usedPaletteIndices.Add(index);

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
                // glTF vertex colours do not tint emission. Each emissive palette slot
                // needs its own material colour for rendering and GLB export to agree.
                if (VoxMaterialAdapter.Emission(voxMaterial) > 0f)
                    propertiesKey = $"{propertiesKey}\nPaletteIndex={paletteIndex}";
                if (!slotByProperties.TryGetValue(propertiesKey, out int slot))
                {
                    Material material = CreateMaterial(voxMaterial, document.Palette[paletteIndex - 1]);
                    slot = materials.Count;
                    slotByProperties.Add(propertiesKey, slot);
                    materials.Add(material);
                    ownedMaterials.Add(material);
                }
                paletteSubmeshIndices[paletteIndex - 1] = slot;
                if (VoxMaterialAdapter.Read(voxMaterial).Transparent)
                {
                    Material material = materials[slot];
                    float alpha = material.HasProperty("_BaseColorFactor")
                        ? material.GetColor("_BaseColorFactor").a : 1f;
                    paletteOccludesFaces[paletteIndex - 1] =
                        alpha * document.Palette[paletteIndex - 1].a / 255f >= 1f;
                }
            }

            Materials = materials;
            PaletteSubmeshIndices = paletteSubmeshIndices;
            PaletteOccludesFaces = paletteOccludesFaces;
            OwnedMaterials = ownedMaterials;
        }

        private static Material CreateMaterial(RuntimeVoxDocument.RuntimeMaterial voxMaterial, Color32 paletteColor)
        {
            var values = VoxMaterialAdapter.Read(voxMaterial);
            var template = Resources.Load<Material>(values.Transparent
                ? "VoxMaterials/Glass" : "VoxMaterials/Opaque");

            var material = new Material(template)
            {
                name = $"VOX {voxMaterial.Type} {voxMaterial.PaletteIndex}",
            };

            PbrMaterialProperties.Apply(material, values, Color.white);
            if (VoxMaterialAdapter.Emission(voxMaterial) > 0f)
            {
                // Preserve control over dim emission while giving the upper end HDR headroom.
                float strength = Mathf.Pow(1f + MaxEmissionIntensity, VoxMaterialAdapter.Emission(voxMaterial)) - 1f;
                material.SetFloat("_EmissionStrength", strength);
                // Match the glowing brushes' colour preparation before increasing
                // brightness. glTF needs this colour separately from vertex colours.
                Color color = paletteColor;
                Color emission = new Color(Mathf.Pow(color.r, 2.2f), Mathf.Pow(color.g, 2.2f),
                    Mathf.Pow(color.b, 2.2f)) * strength;
                emission.a = 1f;
                material.SetColor("_EmissionColor", emission);
                material.EnableKeyword("_EMISSION");
            }
            return material;
        }
    }
}
