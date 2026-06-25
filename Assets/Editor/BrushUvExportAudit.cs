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

namespace TiltBrush
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Audits brush source UV layouts against the UnityGLTF BrushBaker mappings.
    /// glTF texcoord attributes are two-component vectors, so source channels with
    /// z/w data either need to be consumed by baking or intentionally remapped.
    /// </summary>
    public static class BrushUvExportAudit
    {
        private const string kLogPrefix = "BRUSH_UV_AUDIT_20260624";
        private const string kStandardManifestPath = "Assets/Manifest.asset";
        private const string kExperimentalManifestPath = "Assets/Manifest_Experimental.asset";
        private const string kBrushBakerPrefabPath = "Assets/Prefabs/BrushBaker.prefab";
        private const string kOutputDirectory = "BrushUvExportAudit";

        private struct ManifestBrush
        {
            public BrushDescriptor Brush;
            public string Manifest;
            public string Role;
        }

        private class Row
        {
            public string Manifest;
            public string Role;
            public string AssetPath;
            public string Guid;
            public string DurableName;
            public string PrefabName;
            public string ScriptNames;
            public string MaterialName;
            public string ShaderName;
            public string ShaderPath;
            public bool AllowExport;
            public bool Superseded;
            public string Uv0;
            public string Uv1;
            public string Uv2;
            public string WideUvChannels;
            public bool HasWideUv;
            public bool HasBakerMapping;
            public string BakerName;
            public string BakerComputeShader;
            public string BakerComputeShaderPath;
            public string BakerModifiedAttributes;
            public string MaterialWideUvReads;
            public string MaterialWideUvReadKinds;
            public string ManualReviewNotes;
            public string BakerWideUvReads;
            public string BakerUvBufferWrites;
            public string BakerVertexBufferWrites;
            public string AutomatedSourceReview;
            public string Finding;
        }

        private class WideUvRead
        {
            public string Description;
            public string Kind;
        }

        private struct WideUvInfo
        {
            public int Channel;
            public int Size;
            public GeometryPool.Semantic Semantic;

            public override string ToString()
            {
                return $"uv{Channel}:{Size}:{Semantic}";
            }
        }

        [MenuItem("Open Brush/Info/Export UV Component Audit")]
        private static void ExportAudit()
        {
            List<Row> rows = BuildRows();
            Directory.CreateDirectory(kOutputDirectory);

            string csvPath = Path.Combine(kOutputDirectory, "brush_uv_component_catalog.csv");
            string reportPath = Path.Combine(kOutputDirectory, "brush_uv_component_gaps.md");

            File.WriteAllText(csvPath, BuildCsv(rows), Encoding.UTF8);
            File.WriteAllText(reportPath, BuildReport(rows, csvPath), Encoding.UTF8);

            Debug.Log($"{kLogPrefix} wrote {rows.Count} brush UV rows to {csvPath}");
            Debug.Log($"{kLogPrefix} wrote gap report to {reportPath}");
        }

        private static List<Row> BuildRows()
        {
            var baker = LoadBrushBaker();
            var rows = new List<Row>();
            foreach (ManifestBrush manifestBrush in EnumerateManifestBrushes())
            {
                BrushDescriptor brush = manifestBrush.Brush;
                if (brush == null)
                {
                    continue;
                }

                string guid = ((Guid)brush.m_Guid).ToString("D");
                GeometryPool.VertexLayout layout;
                string layoutError = null;
                try
                {
                    layout = brush.VertexLayout;
                }
                catch (Exception e)
                {
                    layout = default;
                    layoutError = e.Message;
                }

                BrushBaker.ComputeShaderMapping mapping = default;
                bool hasMapping = baker != null && baker.TryGetMapping(guid, out mapping);
                var wideChannels = GetWideUvInfos(layout).ToArray();
                string shaderPath = brush.Material != null && brush.Material.shader != null
                    ? AssetDatabase.GetAssetPath(brush.Material.shader)
                    : "";
                string computeShaderPath = hasMapping && mapping.computeShader != null
                    ? AssetDatabase.GetAssetPath(mapping.computeShader)
                    : "";
                string materialWideUvReads = FindWideUvReadsInMaterialShader(shaderPath, wideChannels);
                string materialWideUvReadKinds = ClassifyWideUvReadsInMaterialShader(shaderPath, wideChannels);
                string bakerWideUvReads = FindWideUvReadsInComputeShader(computeShaderPath, wideChannels);
                string bakerUvBufferWrites = FindUvBufferWritesInComputeShader(computeShaderPath);
                string bakerVertexBufferWrites = FindVertexBufferWritesInComputeShader(computeShaderPath);

                Row row = new Row
                {
                    Manifest = manifestBrush.Manifest,
                    Role = manifestBrush.Role,
                    AssetPath = AssetDatabase.GetAssetPath(brush),
                    Guid = guid,
                    DurableName = brush.m_DurableName,
                    PrefabName = brush.m_BrushPrefab != null ? brush.m_BrushPrefab.name : "",
                    ScriptNames = GetBrushScriptNames(brush),
                    MaterialName = brush.Material != null ? brush.Material.name : "",
                    ShaderName = brush.Material != null && brush.Material.shader != null ? brush.Material.shader.name : "",
                    ShaderPath = shaderPath,
                    AllowExport = brush.m_AllowExport,
                    Superseded = brush.m_SupersededBy != null,
                    Uv0 = layoutError ?? FormatTexcoord(layout.texcoord0),
                    Uv1 = layoutError ?? FormatTexcoord(layout.texcoord1),
                    Uv2 = layoutError ?? FormatTexcoord(layout.texcoord2),
                    WideUvChannels = string.Join(";", wideChannels.Select(channel => channel.ToString()).ToArray()),
                    HasWideUv = wideChannels.Length > 0,
                    HasBakerMapping = hasMapping,
                    BakerName = hasMapping ? mapping.name : "",
                    BakerComputeShader = hasMapping && mapping.computeShader != null ? mapping.computeShader.name : "",
                    BakerComputeShaderPath = computeShaderPath,
                    BakerModifiedAttributes = hasMapping ? FormatModifiedAttributes(mapping) : "",
                    MaterialWideUvReads = materialWideUvReads,
                    MaterialWideUvReadKinds = materialWideUvReadKinds,
                    BakerWideUvReads = bakerWideUvReads,
                    BakerUvBufferWrites = bakerUvBufferWrites,
                    BakerVertexBufferWrites = bakerVertexBufferWrites,
                };
                ApplyManualReview(row);
                row.AutomatedSourceReview = ClassifySourceReview(row);
                row.Finding = layoutError != null
                    ? $"ERROR_LAYOUT: {layoutError}"
                    : ClassifyFinding(row);
                rows.Add(row);
            }
            return rows
                .OrderBy(row => row.Manifest)
                .ThenBy(row => row.Role)
                .ThenBy(row => row.DurableName)
                .ThenBy(row => row.Guid)
                .ToList();
        }

        private static BrushBaker LoadBrushBaker()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(kBrushBakerPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"{kLogPrefix} could not load {kBrushBakerPrefabPath}");
                return null;
            }
            var baker = prefab.GetComponent<BrushBaker>();
            if (baker == null)
            {
                Debug.LogWarning($"{kLogPrefix} {kBrushBakerPrefabPath} has no BrushBaker component");
            }
            return baker;
        }

        private static IEnumerable<ManifestBrush> EnumerateManifestBrushes()
        {
            foreach (ManifestBrush brush in EnumerateManifest(kStandardManifestPath, "Standard"))
            {
                yield return brush;
            }
            foreach (ManifestBrush brush in EnumerateManifest(kExperimentalManifestPath, "Experimental"))
            {
                yield return brush;
            }
        }

        private static IEnumerable<ManifestBrush> EnumerateManifest(string path, string manifestName)
        {
            TiltBrushManifest manifest = AssetDatabase.LoadAssetAtPath<TiltBrushManifest>(path);
            if (manifest == null)
            {
                Debug.LogWarning($"{kLogPrefix} could not load {path}");
                yield break;
            }

            foreach (BrushDescriptor brush in manifest.Brushes ?? Array.Empty<BrushDescriptor>())
            {
                yield return new ManifestBrush { Brush = brush, Manifest = manifestName, Role = "Brushes" };
            }
            foreach (BrushDescriptor brush in manifest.CompatibilityBrushes ?? Array.Empty<BrushDescriptor>())
            {
                yield return new ManifestBrush { Brush = brush, Manifest = manifestName, Role = "CompatibilityBrushes" };
            }
        }

        private static IEnumerable<WideUvInfo> GetWideUvInfos(GeometryPool.VertexLayout layout)
        {
            for (int channel = 0; channel < GeometryPool.kNumTexcoords; channel++)
            {
                GeometryPool.TexcoordInfo info = layout.GetTexcoordInfo(channel);
                if (info.size > 2)
                {
                    yield return new WideUvInfo
                    {
                        Channel = channel,
                        Size = info.size,
                        Semantic = info.semantic
                    };
                }
            }
        }

        private static string FormatTexcoord(GeometryPool.TexcoordInfo info)
        {
            return info.size == 0 ? "unused" : $"{info.size}:{info.semantic}";
        }

        private static string FormatModifiedAttributes(BrushBaker.ComputeShaderMapping mapping)
        {
            var modified = new List<string>();
            if (mapping.ModifyColor) modified.Add("color");
            if (mapping.ModifyNormal) modified.Add("normal");
            if (mapping.ModifyUv0) modified.Add("uv0");
            if (mapping.ModifyUv1) modified.Add("uv1");
            if (mapping.ModifyUv2) modified.Add("uv2");
            return string.Join(";", modified);
        }

        private static void ApplyManualReview(Row row)
        {
            if (row.DurableName == "Slice")
            {
                row.ManualReviewNotes =
                    "Ignored for now by request. Automated scan correctly sees uv0.z in fragment/color code.";
                return;
            }

            if (row.DurableName == "Comet" || row.DurableName == "Hypercolor")
            {
                row.ManualReviewNotes =
                    "Manual source review: uv0.z is only used for AUDIO_REACTIVE vertex deformation. Export can treat this as intentionally dynamic runtime deformation unless WebGL export is expected to preserve audio-reactive motion.";
                return;
            }

            if (row.DurableName == "Toon")
            {
                row.ManualReviewNotes =
                    "Manual source review: uv0.z is radius used outside AUDIO_REACTIVE for outline/inflation. This remains the meaningful missing-baker vertex-path case.";
                return;
            }

            if (IsManualVertexOnlyWideUvBrush(row.DurableName))
            {
                row.MaterialWideUvReadKinds = "vertex";
                row.ManualReviewNotes =
                    "Manual source review: live wide-UV reads are vertex-stage only; scanner uncertainty comes from helper/derived variables or commented code.";
            }
        }

        private static bool IsManualVertexOnlyWideUvBrush(string durableName)
        {
            switch (durableName)
            {
                case "Bubbles":
                case "Dots":
                case "Embers":
                case "Rising Bubbles":
                case "Snow":
                case "Stars":
                case "Smoke":
                case "WaveformParticles":
                case "DanceFloor":
                    return true;
                default:
                    return false;
            }
        }

        private static string ClassifyFinding(Row row)
        {
            if (!row.HasWideUv)
            {
                return row.HasBakerMapping ? "OK_NO_WIDE_UV_WITH_BAKER_MAPPING" : "OK_NO_WIDE_UV";
            }

            bool materialReadsWideUv = !string.IsNullOrEmpty(row.MaterialWideUvReads);
            bool bakerReadsWideUv = !string.IsNullOrEmpty(row.BakerWideUvReads);
            bool bakerWritesUv = !string.IsNullOrEmpty(row.BakerUvBufferWrites);
            bool bakerWritesVertex = !string.IsNullOrEmpty(row.BakerVertexBufferWrites);
            string materialKind = PrimaryMaterialWideUvReadKind(row);

            if (row.DurableName == "Slice")
            {
                return "DEFERRED: Slice ignored for now despite fragment/color wide UV use";
            }

            if (!row.HasBakerMapping)
            {
                if (materialReadsWideUv)
                {
                    if (IsAudioReactiveOnlyWideUvBrush(row.DurableName))
                    {
                        return "PROBABLY OK: Missing BrushBaker entry and wide UV use is audio-reactive vertex-only";
                    }
                    if (materialKind == "fragment/color")
                    {
                        return "LIKELY PROBLEM: Missing BrushBaker entry and material uses wide UV in fragment/color path";
                    }
                    if (materialKind == "vertex")
                    {
                        return "NEEDS REVIEW: Missing BrushBaker entry and material uses wide UV in vertex shader path";
                    }
                    return "NEEDS REVIEW: Missing BrushBaker entry and material uses wide UV in unclear shader path";
                }
                if (BrushBaker.DropsUnusedWideUvForGltf(row.Guid))
                {
                    return "HANDLED: No BrushBaker entry; export drops unused wide UV components";
                }
                return "PROBABLY OK: Missing BrushBaker entry; original material wide UV use not found";
            }
            if (string.IsNullOrEmpty(row.BakerComputeShader))
            {
                return "LIKELY PROBLEM: BrushBaker entry has no compute shader";
            }

            if (!materialReadsWideUv && !bakerReadsWideUv && bakerWritesVertex)
            {
                return "PROBABLY OK: BrushBaker writes vertices and no wide UV input use found";
            }

            if (bakerWritesUv)
            {
                if (materialReadsWideUv)
                {
                    return $"REVIEW EXPORT PATH: BrushBaker writes UVs and material uses wide UV in {materialKind} path";
                }
                return bakerReadsWideUv
                    ? "PROBABLY OK: BrushBaker writes UVs and consumes wide UVs"
                    : "NEEDS REVIEW: BrushBaker writes UVs but source scan did not find wide UV input use";
            }

            if (materialReadsWideUv)
            {
                return $"REVIEW EXPORT PATH: BrushBaker does not write UVs and material uses wide UV in {materialKind} path";
            }
            return bakerReadsWideUv
                ? "PROBABLY OK: BrushBaker consumes wide UVs and original material wide UV use not found"
                : "NEEDS REVIEW: BrushBaker mapping exists but source scan did not find wide UV input use";
        }

        private static bool IsAudioReactiveOnlyWideUvBrush(string durableName)
        {
            switch (durableName)
            {
                case "Comet":
                case "Hypercolor":
                    return true;
                default:
                    return false;
            }
        }

        private static string GetBrushScriptNames(BrushDescriptor brush)
        {
            GameObject prefab = brush.m_BrushPrefab;
            if (prefab == null)
            {
                return "";
            }
            return string.Join(";",
                prefab.GetComponents<MonoBehaviour>()
                    .Where(component => component != null)
                    .Select(component => component.GetType().Name)
                    .Where(name => name != nameof(Transform) && name != nameof(MeshFilter) && name != nameof(MeshRenderer))
                    .Distinct());
        }

        private static string BuildCsv(IEnumerable<Row> rows)
        {
            var sb = new StringBuilder();
            AppendCsvLine(sb, new[]
            {
                "Manifest", "Role", "AssetPath", "Guid", "DurableName", "PrefabName",
                "BrushScripts", "MaterialName", "ShaderName", "ShaderPath", "AllowExport", "Superseded",
                "Uv0", "Uv1", "Uv2", "WideUvChannels", "HasWideUv", "HasBakerMapping",
                "BakerName", "BakerComputeShader", "BakerComputeShaderPath", "BakerModifiedAttributes",
                "MaterialWideUvReads", "MaterialWideUvReadKinds", "ManualReviewNotes",
                "BakerWideUvReads", "BakerUvBufferWrites", "BakerVertexBufferWrites",
                "AutomatedSourceReview", "Finding"
            });
            foreach (Row row in rows)
            {
                AppendCsvLine(sb, new[]
                {
                    row.Manifest,
                    row.Role,
                    row.AssetPath,
                    row.Guid,
                    row.DurableName,
                    row.PrefabName,
                    row.ScriptNames,
                    row.MaterialName,
                    row.ShaderName,
                    row.ShaderPath,
                    row.AllowExport.ToString(),
                    row.Superseded.ToString(),
                    row.Uv0,
                    row.Uv1,
                    row.Uv2,
                    row.WideUvChannels,
                    row.HasWideUv.ToString(),
                    row.HasBakerMapping.ToString(),
                    row.BakerName,
                    row.BakerComputeShader,
                    row.BakerComputeShaderPath,
                    row.BakerModifiedAttributes,
                    row.MaterialWideUvReads,
                    row.MaterialWideUvReadKinds,
                    row.ManualReviewNotes,
                    row.BakerWideUvReads,
                    row.BakerUvBufferWrites,
                    row.BakerVertexBufferWrites,
                    row.AutomatedSourceReview,
                    row.Finding
                });
            }
            return sb.ToString();
        }

        private static void AppendCsvLine(StringBuilder sb, IEnumerable<string> cells)
        {
            sb.AppendLine(string.Join(",", cells.Select(EscapeCsv)));
        }

        private static string EscapeCsv(string value)
        {
            value = value ?? "";
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
            {
                return value;
            }
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private static string BuildReport(IReadOnlyCollection<Row> rows, string csvPath)
        {
            var actionableRows = rows
                .Where(row => !row.Finding.StartsWith("OK_", StringComparison.Ordinal))
                .OrderBy(row => FindingSortKey(row.Finding))
                .ThenBy(row => row.Finding)
                .ThenBy(row => row.DurableName)
                .ToArray();

            var sb = new StringBuilder();
            sb.AppendLine("# Brush UV Component Export Audit");
            sb.AppendLine();
            sb.AppendLine($"CSV catalog: `{csvPath}`");
            sb.AppendLine();
            sb.AppendLine("UnityGLTF exports glTF texture coordinates as two-component vectors. Open Brush brush meshes may use uv.z or uv.w for non-texture data such as radius, lifetime, position, or offsets. This report lists brush descriptors whose declared vertex layout uses more than two components in a UV channel, then compares that against the BrushBaker prefab mappings used before UnityGLTF export.");
            sb.AppendLine();
            sb.AppendLine("## How to read this");
            sb.AppendLine();
            sb.AppendLine("The `source review` text in each row is from static source-code scans only. It looks for original Unity material reads of uv.z/uv.w, whether those reads occur in vertex or fragment/color shader paths, BrushBaker compute shader reads of those same components, and compute shader writes to UV buffers. It does not inspect WebGL/export shaders, generate a stroke mesh, or run a UnityGLTF export, so treat it as a triage aid rather than proof of exported vertex data.");
            sb.AppendLine("Each row appears in exactly one category. Category prefixes indicate action level: `LIKELY PROBLEM` means the trusted source scan found an export-risk pattern; `NEEDS REVIEW` means trusted source scan evidence is incomplete; `REVIEW EXPORT PATH` means the original material uses wide UVs and the later WebGL shader/export validation stage needs to prove that export does not; `DEFERRED` means manually acknowledged but intentionally ignored for now; `PROBABLY OK` means no BrushBaker fix is implied by this source scan.");
            sb.AppendLine();
            foreach (string finding in actionableRows.Select(row => row.Finding).Distinct())
            {
                sb.AppendLine($"- `{finding}`: {DescribeFinding(finding)}");
            }
            sb.AppendLine();
            sb.AppendLine($"Total manifest rows: {rows.Count}");
            sb.AppendLine($"Rows with UV channels wider than two components: {rows.Count(row => row.HasWideUv)}");
            sb.AppendLine($"Rows needing gap/review work: {actionableRows.Length}");
            sb.AppendLine();
            AppendManualReviewNotes(sb);

            foreach (IGrouping<string, Row> group in actionableRows.GroupBy(row => row.Finding))
            {
                sb.AppendLine($"## {group.Key}");
                foreach (Row row in group)
                {
                    string bakerWrites = FormatBakerWritesForReport(row);
                    sb.AppendLine($"- {row.DurableName} `{row.Guid}` {row.Manifest}/{row.Role}: {row.WideUvChannels}; baker `{row.BakerName}` {bakerWrites}; source review: {row.AutomatedSourceReview}");
                    if (!string.IsNullOrEmpty(row.MaterialWideUvReads))
                    {
                        sb.AppendLine($"  - Original Unity material wide UV reads: `{row.MaterialWideUvReads}`");
                    }
                    if (!string.IsNullOrEmpty(row.MaterialWideUvReadKinds))
                    {
                        sb.AppendLine($"  - Original Unity material read paths: `{row.MaterialWideUvReadKinds}`");
                    }
                    if (!string.IsNullOrEmpty(row.ManualReviewNotes))
                    {
                        sb.AppendLine($"  - Manual review: {row.ManualReviewNotes}");
                    }
                    if (!string.IsNullOrEmpty(row.BakerWideUvReads))
                    {
                        sb.AppendLine($"  - Baker compute wide UV reads: `{row.BakerWideUvReads}`");
                    }
                    if (!string.IsNullOrEmpty(row.BakerUvBufferWrites))
                    {
                        sb.AppendLine($"  - Baker compute UV buffer writes: `{row.BakerUvBufferWrites}`");
                    }
                    if (!string.IsNullOrEmpty(row.BakerVertexBufferWrites))
                    {
                        sb.AppendLine($"  - Baker compute vertex buffer writes: `{row.BakerVertexBufferWrites}`");
                    }
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string FormatBakerWritesForReport(Row row)
        {
            var writes = new List<string>();
            if (!string.IsNullOrEmpty(row.BakerModifiedAttributes))
            {
                writes.Add($"declares `{row.BakerModifiedAttributes}`");
            }
            if (!string.IsNullOrEmpty(row.BakerVertexBufferWrites))
            {
                writes.Add($"writes vertex `{row.BakerVertexBufferWrites}`");
            }
            if (!string.IsNullOrEmpty(row.BakerUvBufferWrites))
            {
                writes.Add($"writes UV `{row.BakerUvBufferWrites}`");
            }
            return writes.Count == 0 ? "has no detected writes" : string.Join(", ", writes);
        }

        private static void AppendManualReviewNotes(StringBuilder sb)
        {
            sb.AppendLine("## Manual Review Notes");
            sb.AppendLine();
            sb.AppendLine("`Slice` is currently ignored by request, even though the automated scan places it in the fragment/color bucket.");
            sb.AppendLine();
            sb.AppendLine("The `unclear` automated path labels below were manually checked in source. In these cases, the uncertainty comes from the simple scanner seeing helper or derived variables, and in some cases commented code. The live wide-UV use appears to be vertex-stage only:");
            sb.AppendLine();
            sb.AppendLine("- Bubbles, Dots, Embers, Rising Bubbles, Snow, Stars, Smoke: `texcoord.z` is particle rotation and `texcoord.w` is birth time inside `vert`; `center.z` is a derived particle center/position value used for vertex placement or animation. The fragment functions use the baked `i.texcoord`/color and do not read the original wide UV components.");
            sb.AppendLine("- WaveformParticles: `texcoord1.xyz` is per-vertex offset and `texcoord1.w` is lifetime inside `vert`; the fragment function does not use the original wide UV components.");
            sb.AppendLine("- DanceFloor: `texcoord1.w` is lifetime inside `vert`; the fragment function does not use the original wide UV components. The scanner is also vulnerable to commented shader code in this file.");
            sb.AppendLine();
        }

        private static string ClassifySourceReview(Row row)
        {
            if (!row.HasWideUv)
            {
                return "No wide UV channels declared.";
            }
            if (!string.IsNullOrEmpty(row.MaterialWideUvReads))
            {
                return $"Original Unity material reads uv.z or uv.w in {PrimaryMaterialWideUvReadKind(row)} path. Runtime use can be expected, but WebGL/export shader validation still needs to prove whether export is safe.";
            }
            if (!row.HasBakerMapping)
            {
                return "No original Unity material wide UV read found, and there is no BrushBaker entry.";
            }
            if (!string.IsNullOrEmpty(row.BakerWideUvReads))
            {
                return "BrushBaker reads uv.z or uv.w and the original material shader scan did not.";
            }
            if (!string.IsNullOrEmpty(row.BakerVertexBufferWrites))
            {
                return "BrushBaker writes vertex positions, and the source scan did not find wide UV reads.";
            }
            return "Needs review: source scan did not find where the wide UV data is consumed.";
        }

        private static string FindWideUvReadsInMaterialShader(string shaderPath, IReadOnlyCollection<WideUvInfo> wideChannels)
        {
            return string.Join(";",
                FindWideUvReadsInShader(shaderPath, wideChannels)
                    .Select(read => read.Description)
                    .Distinct()
                    .OrderBy(read => read)
                    .ToArray());
        }

        private static string ClassifyWideUvReadsInMaterialShader(
            string shaderPath, IReadOnlyCollection<WideUvInfo> wideChannels)
        {
            return string.Join(";",
                FindWideUvReadsInShader(shaderPath, wideChannels)
                    .Select(read => read.Kind)
                    .Distinct()
                    .OrderBy(read => MaterialWideUvReadKindSortKey(read))
                    .ThenBy(read => read)
                    .ToArray());
        }

        private static IEnumerable<WideUvRead> FindWideUvReadsInShader(
            string shaderPath, IReadOnlyCollection<WideUvInfo> wideChannels)
        {
            if (string.IsNullOrEmpty(shaderPath) || wideChannels.Count == 0)
            {
                return Enumerable.Empty<WideUvRead>();
            }
            string source = ReadSourceWithIncludes(shaderPath);
            if (string.IsNullOrEmpty(source))
            {
                return Enumerable.Empty<WideUvRead>();
            }

            var reads = new List<WideUvRead>();
            foreach (WideUvInfo wideChannel in wideChannels)
            {
                foreach (string variable in FindTexcoordVariables(source, wideChannel.Channel))
                {
                    AddWideComponentReads(source, variable, wideChannel, reads);
                }
                AddWideComponentReads(source, $"texcoord{wideChannel.Channel}", wideChannel, reads);
                AddWideComponentReads(source, $"uv{wideChannel.Channel}", wideChannel, reads);
                if (wideChannel.Channel == 0)
                {
                    AddWideComponentReads(source, "texcoord", wideChannel, reads);
                    AddWideComponentReads(source, "uv", wideChannel, reads);
                }
            }
            return reads;
        }

        private static string FindWideUvReadsInComputeShader(string computeShaderPath, IReadOnlyCollection<WideUvInfo> wideChannels)
        {
            if (string.IsNullOrEmpty(computeShaderPath) || wideChannels.Count == 0)
            {
                return "";
            }
            string source = ReadSourceWithIncludes(computeShaderPath);
            if (string.IsNullOrEmpty(source))
            {
                return "";
            }

            var reads = new SortedSet<string>();
            foreach (WideUvInfo wideChannel in wideChannels)
            {
                string bufferName = ComputeUvBufferName(wideChannel.Channel);
                foreach (string variable in FindVariablesAssignedFromBuffer(source, bufferName))
                {
                    AddWideComponentReads(source, variable, wideChannel, reads);
                }
            }
            return string.Join(";", reads.ToArray());
        }

        private static string FindUvBufferWritesInComputeShader(string computeShaderPath)
        {
            if (string.IsNullOrEmpty(computeShaderPath))
            {
                return "";
            }
            string source = ReadSourceWithIncludes(computeShaderPath);
            if (string.IsNullOrEmpty(source))
            {
                return "";
            }

            var writes = new List<string>();
            for (int channel = 0; channel < GeometryPool.kNumTexcoords; channel++)
            {
                string bufferName = ComputeUvBufferName(channel);
                if (Regex.IsMatch(source, $@"\b{Regex.Escape(bufferName)}\s*\[[^\]]+\]\s*="))
                {
                    writes.Add($"uv{channel}");
                }
            }
            return string.Join(";", writes);
        }

        private static string FindVertexBufferWritesInComputeShader(string computeShaderPath)
        {
            if (string.IsNullOrEmpty(computeShaderPath))
            {
                return "";
            }
            string source = ReadSourceWithIncludes(computeShaderPath);
            if (string.IsNullOrEmpty(source))
            {
                return "";
            }
            return Regex.IsMatch(source, @"\bvertexBuffer\s*\[[^\]]+\]\s*=") ? "vertex" : "";
        }

        private static IEnumerable<string> FindTexcoordVariables(string source, int channel)
        {
            string pattern = $@"\b(?:float|half|fixed)(?:2|3|4)\s+(\w+)\s*:\s*TEXCOORD{channel}\b";
            return Regex.Matches(source, pattern)
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .Distinct();
        }

        private static IEnumerable<string> FindVariablesAssignedFromBuffer(string source, string bufferName)
        {
            string pattern = $@"\b(?:float|half|fixed)(?:2|3|4)\s+(\w+)\s*=\s*{Regex.Escape(bufferName)}\s*\[";
            return Regex.Matches(source, pattern)
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .Distinct();
        }

        private static void AddWideComponentReads(
            string source, string variable, WideUvInfo wideChannel, SortedSet<string> reads)
        {
            if (wideChannel.Size >= 3
                && Regex.IsMatch(source, $@"\b{Regex.Escape(variable)}\s*\.\s*(?:z|xz|yz|xyz|xyzw|zw)\b"))
            {
                reads.Add($"uv{wideChannel.Channel}.z via {variable}");
            }
            if (wideChannel.Size >= 4
                && Regex.IsMatch(source, $@"\b{Regex.Escape(variable)}\s*\.\s*(?:w|xw|yw|zw|xyw|xyzw)\b"))
            {
                reads.Add($"uv{wideChannel.Channel}.w via {variable}");
            }
        }

        private static void AddWideComponentReads(
            string source, string variable, WideUvInfo wideChannel, ICollection<WideUvRead> reads)
        {
            if (wideChannel.Size >= 3)
            {
                AddWideComponentReads(source, variable, wideChannel, "z", @"(?:z|xz|yz|xyz|xyzw|zw)", reads);
            }
            if (wideChannel.Size >= 4)
            {
                AddWideComponentReads(source, variable, wideChannel, "w", @"(?:w|xw|yw|zw|xyw|xyzw)", reads);
            }
        }

        private static void AddWideComponentReads(
            string source, string variable, WideUvInfo wideChannel, string component,
            string swizzlePattern, ICollection<WideUvRead> reads)
        {
            foreach (Match match in Regex.Matches(
                         source, $@"\b{Regex.Escape(variable)}\s*\.\s*{swizzlePattern}\b"))
            {
                reads.Add(new WideUvRead
                {
                    Description = $"uv{wideChannel.Channel}.{component} via {variable}",
                    Kind = ClassifyShaderReadPath(source, match.Index)
                });
            }
        }

        private static string PrimaryMaterialWideUvReadKind(Row row)
        {
            if (string.IsNullOrEmpty(row.MaterialWideUvReadKinds))
            {
                return "unclear";
            }
            return row.MaterialWideUvReadKinds
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .OrderBy(MaterialWideUvReadKindSortKey)
                .FirstOrDefault() ?? "unclear";
        }

        private static int MaterialWideUvReadKindSortKey(string kind)
        {
            switch (kind)
            {
                case "fragment/color":
                    return 0;
                case "unclear":
                    return 1;
                case "vertex":
                    return 2;
                default:
                    return 3;
            }
        }

        private static string ClassifyShaderReadPath(string source, int readIndex)
        {
            FunctionInfo function = FindContainingFunction(source, readIndex);
            if (function.Name == null)
            {
                return "unclear";
            }

            string name = function.Name.ToLowerInvariant();
            string signature = function.Signature.ToLowerInvariant();
            if (name.Contains("surf") || name.Contains("frag") || signature.Contains(": color"))
            {
                return "fragment/color";
            }
            if (name.Contains("vert"))
            {
                return "vertex";
            }
            return "unclear";
        }

        private struct FunctionInfo
        {
            public string Name;
            public string Signature;
        }

        private static FunctionInfo FindContainingFunction(string source, int readIndex)
        {
            string signaturePattern =
                @"\b(?:void|float|float2|float3|float4|half|half2|half3|half4|fixed|fixed2|fixed3|fixed4|v2f|\w+)\s+(\w+)\s*\([^;{}]*\)\s*(?::\s*\w+)?\s*\{";
            foreach (Match match in Regex.Matches(source, signaturePattern))
            {
                int bodyStart = match.Index + match.Length - 1;
                if (bodyStart > readIndex)
                {
                    continue;
                }
                int bodyEnd = FindMatchingBrace(source, bodyStart);
                if (bodyEnd >= readIndex)
                {
                    return new FunctionInfo
                    {
                        Name = match.Groups[1].Value,
                        Signature = match.Value
                    };
                }
            }
            return default;
        }

        private static int FindMatchingBrace(string source, int openBraceIndex)
        {
            int depth = 0;
            for (int i = openBraceIndex; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                }
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        private static string ComputeUvBufferName(int channel)
        {
            return channel == 0 ? "uvBuffer" : $"uv{channel}Buffer";
        }

        private static string ReadSourceWithIncludes(string assetPath)
        {
            return ReadSourceWithIncludes(assetPath, new HashSet<string>());
        }

        private static string ReadSourceWithIncludes(string assetPath, HashSet<string> visited)
        {
            if (string.IsNullOrEmpty(assetPath) || visited.Contains(assetPath) || !File.Exists(assetPath))
            {
                return "";
            }
            visited.Add(assetPath);

            string source = File.ReadAllText(assetPath);
            var sb = new StringBuilder(source);
            foreach (Match includeMatch in Regex.Matches(source, @"#include\s+""([^""]+)"""))
            {
                string includePath = ResolveIncludePath(assetPath, includeMatch.Groups[1].Value);
                if (!string.IsNullOrEmpty(includePath))
                {
                    sb.AppendLine();
                    sb.AppendLine(ReadSourceWithIncludes(includePath, visited));
                }
            }
            return sb.ToString();
        }

        private static string ResolveIncludePath(string sourcePath, string includePath)
        {
            string projectRelative = includePath.Replace("\\", "/");
            if (projectRelative.StartsWith("Assets/", StringComparison.Ordinal)
                || projectRelative.StartsWith("Packages/", StringComparison.Ordinal))
            {
                return File.Exists(projectRelative) ? projectRelative : "";
            }

            string sourceDirectory = Path.GetDirectoryName(sourcePath);
            if (!string.IsNullOrEmpty(sourceDirectory))
            {
                string relativePath = Path.GetFullPath(Path.Combine(sourceDirectory, includePath));
                string projectRoot = Path.GetFullPath(".");
                if (relativePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(relativePath))
                {
                    return relativePath;
                }
            }
            return "";
        }

        private static int FindingSortKey(string finding)
        {
            switch (finding)
            {
                case "LIKELY PROBLEM: Missing BrushBaker entry and material uses wide UV in fragment/color path":
                case "LIKELY PROBLEM: BrushBaker entry has no compute shader":
                    return 0;
                case "NEEDS REVIEW: Missing BrushBaker entry and material uses wide UV in unclear shader path":
                case "REVIEW EXPORT PATH: BrushBaker does not write UVs and material uses wide UV in fragment/color path":
                case "REVIEW EXPORT PATH: BrushBaker writes UVs and material uses wide UV in fragment/color path":
                    return 1;
                case "NEEDS REVIEW: Missing BrushBaker entry and material uses wide UV in vertex shader path":
                case "REVIEW EXPORT PATH: BrushBaker does not write UVs and material uses wide UV in vertex path":
                case "REVIEW EXPORT PATH: BrushBaker writes UVs and material uses wide UV in vertex path":
                case "REVIEW EXPORT PATH: BrushBaker does not write UVs and material uses wide UV in unclear path":
                case "REVIEW EXPORT PATH: BrushBaker writes UVs and material uses wide UV in unclear path":
                    return 2;
                case "NEEDS REVIEW: BrushBaker mapping exists but source scan did not find wide UV input use":
                case "NEEDS REVIEW: BrushBaker writes UVs but source scan did not find wide UV input use":
                    return 3;
                case "HANDLED: No BrushBaker entry; export drops unused wide UV components":
                    return 4;
                case "PROBABLY OK: Missing BrushBaker entry; original material wide UV use not found":
                    return 5;
                case "PROBABLY OK: Missing BrushBaker entry and wide UV use is audio-reactive vertex-only":
                    return 6;
                case "PROBABLY OK: BrushBaker writes vertices and no wide UV input use found":
                    return 7;
                case "PROBABLY OK: BrushBaker consumes wide UVs and original material wide UV use not found":
                case "PROBABLY OK: BrushBaker writes UVs and consumes wide UVs":
                    return 8;
                case "DEFERRED: Slice ignored for now despite fragment/color wide UV use":
                    return 9;
                default:
                    return 10;
            }
        }

        private static string DescribeFinding(string finding)
        {
            switch (finding)
            {
                case "LIKELY PROBLEM: Missing BrushBaker entry and material uses wide UV in fragment/color path":
                    return "the brush has no BrushBaker mapping, and the original Unity material reads uv.z/uv.w in a fragment, surface, or color path. Until the real WebGL/export shader proves otherwise, assume exported rendering needs data UnityGLTF cannot carry as wide UV components.";
                case "LIKELY PROBLEM: BrushBaker entry has no compute shader":
                    return "the BrushBaker prefab has a mapping entry, but no compute shader is assigned.";
                case "NEEDS REVIEW: Missing BrushBaker entry and material uses wide UV in vertex shader path":
                    return "the brush has no BrushBaker mapping, and the original Unity material reads uv.z/uv.w in a vertex shader path. This may be safe only if the value is fully baked into exported vertex positions or otherwise absent from the real export shader.";
                case "NEEDS REVIEW: Missing BrushBaker entry and material uses wide UV in unclear shader path":
                    return "the brush has no BrushBaker mapping, and the source scan found uv.z/uv.w use but could not classify whether it is vertex or fragment/color.";
                case "REVIEW EXPORT PATH: BrushBaker does not write UVs and material uses wide UV in fragment/color path":
                    return "the original Unity material reads uv.z/uv.w in a fragment/color path, and BrushBaker does not write UV buffers for this mapping. The later WebGL/export shader validation should prove those components are not needed after export.";
                case "REVIEW EXPORT PATH: BrushBaker does not write UVs and material uses wide UV in vertex path":
                    return "the original Unity material reads uv.z/uv.w in a vertex path, and BrushBaker does not write UV buffers for this mapping. This may be safe only if the result is baked into exported geometry.";
                case "REVIEW EXPORT PATH: BrushBaker writes UVs and material uses wide UV in fragment/color path":
                    return "BrushBaker writes UV buffers, while the original Unity material reads uv.z/uv.w in a fragment/color path. The later WebGL/export shader validation should confirm the exported material uses the baked/remapped data.";
                case "REVIEW EXPORT PATH: BrushBaker writes UVs and material uses wide UV in vertex path":
                    return "BrushBaker writes UV buffers, while the original Unity material reads uv.z/uv.w in a vertex path. This can be fine, but exported UV component count still needs validation.";
                case "REVIEW EXPORT PATH: BrushBaker does not write UVs and material uses wide UV in unclear path":
                case "REVIEW EXPORT PATH: BrushBaker writes UVs and material uses wide UV in unclear path":
                    return "the original Unity material reads uv.z/uv.w, but the source scan could not classify the shader path.";
                case "NEEDS REVIEW: BrushBaker mapping exists but source scan did not find wide UV input use":
                    return "a BrushBaker mapping exists, but the source scan did not find the compute shader reading the declared wide UV data. This may be a scan limitation or a stale/unnecessary mapping.";
                case "NEEDS REVIEW: BrushBaker writes UVs but source scan did not find wide UV input use":
                    return "BrushBaker writes UV buffers, but the source scan did not find it reading the declared wide UV data. Check whether the write is unrelated, indirect, or stale.";
                case "HANDLED: No BrushBaker entry; export drops unused wide UV components":
                    return "static source review found no original Unity material reads of uv.z/uv.w, and BrushBaker now trims these brushes' exported UV channels to two components without running a compute shader.";
                case "PROBABLY OK: Missing BrushBaker entry; original material wide UV use not found":
                    return "no BrushBaker fix is implied by this source scan. The brush declares wide UV data, but the original Unity material scan did not find wide UV reads.";
                case "PROBABLY OK: Missing BrushBaker entry and wide UV use is audio-reactive vertex-only":
                    return "manual source review found uv.z/uv.w use only inside AUDIO_REACTIVE vertex deformation. This does not imply a BrushBaker remap fix unless exported/WebGL rendering is expected to preserve audio-reactive runtime motion.";
                case "PROBABLY OK: BrushBaker writes vertices and no wide UV input use found":
                    return "BrushBaker writes vertex positions, but the source scan did not find original material or compute shader use of uv.z/uv.w. This looks like a vertex-bake mapping rather than a wide-UV remap gap.";
                case "PROBABLY OK: BrushBaker consumes wide UVs and original material wide UV use not found":
                    return "BrushBaker reads uv.z/uv.w and the original material shader scan did not find wide UV reads. This looks like wide data is baked away for export.";
                case "PROBABLY OK: BrushBaker writes UVs and consumes wide UVs":
                    return "BrushBaker reads uv.z/uv.w and writes UV buffers. This looks intentional from source scan; exported UV component count still requires export validation.";
                case "DEFERRED: Slice ignored for now despite fragment/color wide UV use":
                    return "Slice has a fragment/color-path wide UV read and no BrushBaker mapping, but it is intentionally ignored for now by request.";
                default:
                    return "review the rows in this group.";
            }
        }
    }
}
