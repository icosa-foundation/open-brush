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
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Audits BrushDescriptor.m_AudioReactive against static shader source evidence.
    /// The descriptor flag controls whether the brush page shows the audio reactive icon.
    /// </summary>
    public static class BrushAudioReactiveAudit
    {
        private const string kLogPrefix = "BRUSH_AUDIO_REACTIVE_AUDIT_20260626";
        private const string kStandardManifestPath = "Assets/Manifest.asset";
        private const string kExperimentalManifestPath = "Assets/Manifest_Experimental.asset";
        private const string kOutputDirectory = "BrushAudioReactiveAudit";

        private static readonly string[] kAudioInputTokens =
        {
            "_BeatOutput",
            "_BeatOutputAccum",
            "_AudioVolume",
            "_FFTTex",
            "_WaveFormTex",
            "_PeakBandLevels",
        };

        private static readonly string[] kAudioTextureInputTokens =
        {
            "_FFTTex",
            "_WaveFormTex",
            "_PeakBandLevels",
        };

        private static readonly string[] kAudioHelperTokens =
        {
            "musicReactiveColor",
            "musicReactiveAnimation",
            "musicReactiveAnimationWorldSpace",
        };

        private class Row
        {
            public string Manifest;
            public string Role;
            public string AssetPath;
            public string Guid;
            public string DurableName;
            public string MaterialName;
            public string ShaderName;
            public string ShaderPath;
            public bool DescriptorAudioReactive;
            public bool HasAudioKeyword;
            public bool UsesAudioInputs;
            public bool UsesAudioTextureInputs;
            public bool UsesMusicReactiveHelpers;
            public bool SuspectedIneffectiveAudioInput;
            public bool ExpectedAudioReactive;
            public string VertexLayoutUv0;
            public string AnalysisNotes;
            public string Evidence;
            public string Finding;
        }

        private struct ManifestBrush
        {
            public BrushDescriptor Brush;
            public string Manifest;
            public string Role;
        }

        [MenuItem("Open Brush/Info/Audio Reactive Brush Audit")]
        private static void ExportAudit()
        {
            List<Row> rows = BuildRows();
            Directory.CreateDirectory(kOutputDirectory);

            string csvPath = Path.Combine(kOutputDirectory, "brush_audio_reactive_catalog.csv");
            string reportPath = Path.Combine(kOutputDirectory, "brush_audio_reactive_mismatches.md");

            File.WriteAllText(csvPath, BuildCsv(rows), Encoding.UTF8);
            File.WriteAllText(reportPath, BuildReport(rows, csvPath), Encoding.UTF8);

            int mismatchCount = rows.Count(row => row.Finding.StartsWith("LIKELY MISMATCH", StringComparison.Ordinal));
            Debug.Log($"{kLogPrefix} wrote {rows.Count} brush audio-reactive rows to {csvPath}");
            Debug.Log($"{kLogPrefix} wrote mismatch report with {mismatchCount} likely mismatches to {reportPath}");
        }

        private static List<Row> BuildRows()
        {
            var rows = new List<Row>();
            foreach (ManifestBrush manifestBrush in EnumerateManifestBrushes())
            {
                BrushDescriptor brush = manifestBrush.Brush;
                if (brush == null)
                {
                    continue;
                }

                Material material = brush.Material;
                Shader shader = material != null ? material.shader : null;
                string shaderPath = shader != null ? AssetDatabase.GetAssetPath(shader) : "";
                int uv0Size = GetUv0Size(brush);
                ShaderEvidence shaderEvidence = InspectShader(shaderPath);
                bool suspectedIneffectiveAudioInput = IsSuspectedIneffectiveAudioInput(shaderEvidence, uv0Size);

                var row = new Row
                {
                    Manifest = manifestBrush.Manifest,
                    Role = manifestBrush.Role,
                    AssetPath = AssetDatabase.GetAssetPath(brush),
                    Guid = ((Guid)brush.m_Guid).ToString("D"),
                    DurableName = brush.m_DurableName,
                    MaterialName = material != null ? material.name : "",
                    ShaderName = shader != null ? shader.name : "",
                    ShaderPath = shaderPath,
                    DescriptorAudioReactive = brush.m_AudioReactive,
                    HasAudioKeyword = shaderEvidence.HasAudioKeyword,
                    UsesAudioInputs = shaderEvidence.UsesAudioInputs,
                    UsesAudioTextureInputs = shaderEvidence.UsesAudioTextureInputs,
                    UsesMusicReactiveHelpers = shaderEvidence.UsesMusicReactiveHelpers,
                    SuspectedIneffectiveAudioInput = suspectedIneffectiveAudioInput,
                    ExpectedAudioReactive = shaderEvidence.ExpectedAudioReactive && !suspectedIneffectiveAudioInput,
                    VertexLayoutUv0 = uv0Size >= 0 ? uv0Size.ToString() : "unknown",
                    AnalysisNotes = BuildAnalysisNotes(shaderEvidence, uv0Size, suspectedIneffectiveAudioInput),
                    Evidence = shaderEvidence.Evidence,
                };
                row.Finding = Classify(row);
                rows.Add(row);
            }
            return rows
                .OrderBy(row => FindingSortKey(row.Finding))
                .ThenBy(row => row.DurableName)
                .ThenBy(row => row.Manifest)
                .ThenBy(row => row.Role)
                .ToList();
        }

        private static string Classify(Row row)
        {
            if (string.IsNullOrEmpty(row.ShaderPath))
            {
                return row.DescriptorAudioReactive
                    ? "NEEDS REVIEW: Checkbox enabled but brush has no inspectable shader"
                    : "PROBABLY OK: Checkbox disabled and brush has no inspectable shader";
            }

            if (row.DescriptorAudioReactive && !row.ExpectedAudioReactive)
            {
                if (row.SuspectedIneffectiveAudioInput)
                {
                    return "LIKELY MISMATCH: Checkbox enabled but audio input appears ineffective for brush geometry";
                }
                return row.HasAudioKeyword
                    ? "LIKELY MISMATCH: Checkbox enabled but shader only declares audio keyword"
                    : "LIKELY MISMATCH: Checkbox enabled but shader does not use audio-reactive inputs";
            }

            if (!row.DescriptorAudioReactive && row.ExpectedAudioReactive)
            {
                return "LIKELY MISMATCH: Checkbox disabled but shader uses audio-reactive inputs";
            }

            if (!row.DescriptorAudioReactive && row.SuspectedIneffectiveAudioInput)
            {
                return "PROBABLY OK: Checkbox disabled and shader audio input appears ineffective for brush geometry";
            }

            if (!row.DescriptorAudioReactive && row.HasAudioKeyword)
            {
                return "PROBABLY OK: Checkbox disabled and shader only declares audio keyword";
            }

            return row.DescriptorAudioReactive
                ? "OK: Checkbox enabled and shader uses audio-reactive inputs"
                : "OK: Checkbox disabled and shader does not use audio-reactive inputs";
        }

        private static int FindingSortKey(string finding)
        {
            if (finding.StartsWith("LIKELY MISMATCH", StringComparison.Ordinal)) return 0;
            if (finding.StartsWith("NEEDS REVIEW", StringComparison.Ordinal)) return 1;
            if (finding.StartsWith("PROBABLY OK", StringComparison.Ordinal)) return 2;
            return 3;
        }

        private struct ShaderEvidence
        {
            public bool HasAudioKeyword;
            public bool UsesAudioInputs;
            public bool UsesAudioTextureInputs;
            public bool UsesMusicReactiveHelpers;
            public bool UsesTexcoord0Z;
            public bool ExpectedAudioReactive;
            public string Evidence;
        }

        private static ShaderEvidence InspectShader(string shaderPath)
        {
            if (string.IsNullOrEmpty(shaderPath) || !File.Exists(shaderPath))
            {
                return default;
            }

            string[] lines = StripComments(File.ReadAllLines(shaderPath));
            var evidence = new List<string>();
            bool hasAudioKeyword = false;
            bool usesAudioInputs = false;
            bool usesAudioTextureInputs = false;
            bool usesMusicReactiveHelpers = false;
            bool usesTexcoord0Z = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int lineNumber = i + 1;

                if (line.Contains("AUDIO_REACTIVE"))
                {
                    hasAudioKeyword = true;
                    AddEvidence(evidence, $"AUDIO_REACTIVE@{lineNumber}");
                }

                foreach (string token in kAudioInputTokens)
                {
                    if (line.Contains(token))
                    {
                        usesAudioInputs = true;
                        AddEvidence(evidence, $"{token}@{lineNumber}");
                    }
                }

                foreach (string token in kAudioTextureInputTokens)
                {
                    if (line.Contains(token))
                    {
                        usesAudioTextureInputs = true;
                    }
                }

                foreach (string token in kAudioHelperTokens)
                {
                    if (line.Contains(token))
                    {
                        usesMusicReactiveHelpers = true;
                        AddEvidence(evidence, $"{token}@{lineNumber}");
                    }
                }

                if (line.Contains("texcoord.z") || line.Contains("texcoord0.z"))
                {
                    usesTexcoord0Z = true;
                    AddEvidence(evidence, $"texcoord.z@{lineNumber}");
                }
            }

            return new ShaderEvidence
            {
                HasAudioKeyword = hasAudioKeyword,
                UsesAudioInputs = usesAudioInputs,
                UsesAudioTextureInputs = usesAudioTextureInputs,
                UsesMusicReactiveHelpers = usesMusicReactiveHelpers,
                UsesTexcoord0Z = usesTexcoord0Z,
                ExpectedAudioReactive = usesAudioInputs || usesMusicReactiveHelpers,
                Evidence = string.Join("; ", evidence.ToArray()),
            };
        }

        private static int GetUv0Size(BrushDescriptor brush)
        {
            try
            {
                return brush.VertexLayout.texcoord0.size;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private static bool IsSuspectedIneffectiveAudioInput(ShaderEvidence shaderEvidence, int uv0Size)
        {
            return shaderEvidence.UsesAudioInputs
                && !shaderEvidence.UsesAudioTextureInputs
                && !shaderEvidence.UsesMusicReactiveHelpers
                && shaderEvidence.UsesTexcoord0Z
                && uv0Size >= 0
                && uv0Size < 3;
        }

        private static string BuildAnalysisNotes(
            ShaderEvidence shaderEvidence, int uv0Size, bool suspectedIneffectiveAudioInput)
        {
            if (suspectedIneffectiveAudioInput)
            {
                return $"shader audio math references texcoord.z, but brush uv0 size is {uv0Size}";
            }
            if (shaderEvidence.UsesAudioTextureInputs)
            {
                return "shader samples audio-driven texture/vector globals";
            }
            if (shaderEvidence.UsesMusicReactiveHelpers)
            {
                return "shader calls shared musicReactive helper functions";
            }
            if (shaderEvidence.UsesAudioInputs)
            {
                return "shader reads beat/audio globals directly";
            }
            if (shaderEvidence.HasAudioKeyword)
            {
                return "shader declares AUDIO_REACTIVE but no audio input use was found";
            }
            return "";
        }

        private static void AddEvidence(List<string> evidence, string item)
        {
            if (evidence.Count < 12)
            {
                evidence.Add(item);
            }
        }

        private static string[] StripComments(string[] lines)
        {
            var cleanLines = new string[lines.Length];
            bool inBlockComment = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                var clean = new StringBuilder();
                for (int j = 0; j < line.Length; j++)
                {
                    if (inBlockComment)
                    {
                        if (j + 1 < line.Length && line[j] == '*' && line[j + 1] == '/')
                        {
                            inBlockComment = false;
                            j++;
                        }
                        continue;
                    }

                    if (j + 1 < line.Length && line[j] == '/' && line[j + 1] == '*')
                    {
                        inBlockComment = true;
                        j++;
                        continue;
                    }

                    if (j + 1 < line.Length && line[j] == '/' && line[j + 1] == '/')
                    {
                        break;
                    }

                    clean.Append(line[j]);
                }
                cleanLines[i] = clean.ToString();
            }

            return cleanLines;
        }

        private static IEnumerable<ManifestBrush> EnumerateManifestBrushes()
        {
            foreach (ManifestBrush brush in EnumerateManifestBrushes(kStandardManifestPath, "Manifest"))
            {
                yield return brush;
            }
            foreach (ManifestBrush brush in EnumerateManifestBrushes(kExperimentalManifestPath, "Manifest_Experimental"))
            {
                yield return brush;
            }
        }

        private static IEnumerable<ManifestBrush> EnumerateManifestBrushes(string path, string manifestName)
        {
            TiltBrushManifest manifest = AssetDatabase.LoadAssetAtPath<TiltBrushManifest>(path);
            if (manifest == null)
            {
                Debug.LogWarning($"{kLogPrefix} could not load {path}");
                yield break;
            }

            foreach (BrushDescriptor brush in manifest.UniqueBrushes())
            {
                yield return new ManifestBrush { Brush = brush, Manifest = manifestName, Role = "UniqueBrushes" };
            }
        }

        private static string BuildCsv(IEnumerable<Row> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", new[]
            {
                "finding",
                "manifest",
                "role",
                "durable_name",
                "guid",
                "descriptor_audio_reactive",
                "expected_audio_reactive",
                "has_audio_keyword",
                "uses_audio_inputs",
                "uses_audio_texture_inputs",
                "uses_music_reactive_helpers",
                "suspected_ineffective_audio_input",
                "uv0_size",
                "material",
                "shader",
                "shader_path",
                "asset_path",
                "analysis_notes",
                "evidence",
            }.Select(Csv)));

            foreach (Row row in rows)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    row.Finding,
                    row.Manifest,
                    row.Role,
                    row.DurableName,
                    row.Guid,
                    row.DescriptorAudioReactive ? "true" : "false",
                    row.ExpectedAudioReactive ? "true" : "false",
                    row.HasAudioKeyword ? "true" : "false",
                    row.UsesAudioInputs ? "true" : "false",
                    row.UsesAudioTextureInputs ? "true" : "false",
                    row.UsesMusicReactiveHelpers ? "true" : "false",
                    row.SuspectedIneffectiveAudioInput ? "true" : "false",
                    row.VertexLayoutUv0,
                    row.MaterialName,
                    row.ShaderName,
                    row.ShaderPath,
                    row.AssetPath,
                    row.AnalysisNotes,
                    row.Evidence,
                }.Select(Csv)));
            }

            return sb.ToString();
        }

        private static string BuildReport(IReadOnlyCollection<Row> rows, string csvPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Brush Audio Reactive Flag Audit");
            sb.AppendLine();
            sb.AppendLine("This report compares `BrushDescriptor.m_AudioReactive` with static source-code evidence from each brush material shader.");
            sb.AppendLine();
            sb.AppendLine("## How to read this");
            sb.AppendLine();
            sb.AppendLine("- `LIKELY MISMATCH` means the checkbox and shader source evidence disagree.");
            sb.AppendLine("- `NEEDS REVIEW` means the brush has no inspectable shader but the checkbox is enabled.");
            sb.AppendLine("- `PROBABLY OK` means the shader either only declares the `AUDIO_REACTIVE` keyword, or the apparent audio input path looks ineffective for this brush geometry.");
            sb.AppendLine("- `OK` means the checkbox agrees with the shader source scan.");
            sb.AppendLine();
            sb.AppendLine("The scan strips comments and looks for actual use of `_BeatOutput`, `_BeatOutputAccum`, `_AudioVolume`, `_FFTTex`, `_WaveFormTex`, `_PeakBandLevels`, or the shared `musicReactive*` helper calls. It records `AUDIO_REACTIVE` keyword declarations separately because a keyword alone does not prove visible audio-reactive behavior. It also records one geometry-aware false-positive pattern: shader audio math scaled by `texcoord.z` when the brush only writes `uv.xy`.");
            sb.AppendLine();
            sb.AppendLine($"CSV: `{csvPath}`");
            sb.AppendLine();

            foreach (IGrouping<string, Row> group in rows.GroupBy(row => row.Finding).OrderBy(group => FindingSortKey(group.Key)))
            {
                sb.AppendLine($"## {group.Key} ({group.Count()})");
                sb.AppendLine();
                foreach (Row row in group.OrderBy(row => row.DurableName).ThenBy(row => row.Manifest).ThenBy(row => row.Role))
                {
                    sb.AppendLine($"- **{row.DurableName}** `{row.Guid}`");
                    sb.AppendLine($"  - Descriptor checkbox: `{row.DescriptorAudioReactive}`; shader expected: `{row.ExpectedAudioReactive}`");
                    sb.AppendLine($"  - Analysis: `{row.AnalysisNotes}`");
                    sb.AppendLine($"  - UV0 size: `{row.VertexLayoutUv0}`");
                    sb.AppendLine($"  - Shader: `{row.ShaderName}` at `{row.ShaderPath}`");
                    if (!string.IsNullOrEmpty(row.Evidence))
                    {
                        sb.AppendLine($"  - Evidence: `{row.Evidence}`");
                    }
                    sb.AppendLine($"  - Asset: `{row.AssetPath}` ({row.Manifest}/{row.Role})");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string Csv(string value)
        {
            value = value ?? "";
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
