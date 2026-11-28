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
using System.IO;
using Unity.Collections;
using UnityEngine;

namespace TiltBrush
{
    // Depth-aware compositor shared by the live preview and final render (stage
    // 5/9 of POSTPROD_PREVIEW_PLAN.md). The actual formula lives in
    // PostProdComposite.shader; this class builds the input textures and drives
    // the blit.
    public static class PostProdCompositor
    {
        private static Material s_Material;

        private static Material GetMaterial()
        {
            if (s_Material == null)
            {
                var shader = Resources.Load<Shader>("PostProduction/PostProdComposite");
                if (shader == null)
                {
                    throw new InvalidOperationException(
                        "PostProdCompositor: PostProduction/PostProdComposite shader resource not found");
                }
                s_Material = new Material(shader);
            }
            return s_Material;
        }

        // Builds a single-channel float (RFloat) texture from a row-major,
        // top-row-first float array (ExrDepthReader's own convention - row 0 is
        // the image's top). Point-filtered for CG depth (used at its own native,
        // unresized resolution - matching the Python reference's lack of any
        // CG-side resize) or bilinear for plate depth (matching cv2.resize(...,
        // INTER_LINEAR) on the Python side).
        //
        // Unity's texture buffer is bottom-up (buffer row 0 = the texture's V=0,
        // the image's bottom when sampled normally - Unity's own long-standing
        // GetPixels/SetPixels convention). Texture2D.LoadImage (used for CG/plate
        // colour) already accounts for this when decoding a top-down PNG.
        // SetPixelData does not - it's a raw memory copy - so a top-row-first array
        // must be row-reversed first, confirmed necessary by rendering the actual
        // depth-vs-colour sampling alignment through the real shader pipeline
        // (red/green mask visualization, see PostProdComposite.shader's
        // _DebugMode) rather than just inspecting the raw texture bytes, which
        // cannot show a UV-mapping mismatch on its own.
        public static Texture2D BuildDepthTexture(float[] pixels, int width, int height, bool bilinear)
        {
            var flipped = new float[pixels.Length];
            for (int row = 0; row < height; row++)
            {
                Array.Copy(pixels, row * width, flipped, (height - 1 - row) * width, width);
            }

            var tex = new Texture2D(width, height, TextureFormat.RFloat, false, true);
            tex.filterMode = bilinear ? FilterMode.Bilinear : FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixelData(flipped, 0);
            tex.Apply(false, false);
            return tex;
        }

        // Runs the compositor, returning the result as a RenderTexture sized to
        // outputWidth x outputHeight (the CG resolution in normal use - the plate
        // inputs are resampled up to it by the GPU's own bilinear texture sampling
        // on read, same effect as the Python reference's explicit cv2.resize).
        public static RenderTexture Composite(
            Texture cgColor, Texture cgDepth, Texture plateColor, Texture plateDepth,
            int outputWidth, int outputHeight, bool naiveMode, bool debugMode = false)
        {
            var rt = new RenderTexture(outputWidth, outputHeight, 0, RenderTextureFormat.ARGB32);
            CompositeInto(rt, cgColor, cgDepth, plateColor, plateDepth, naiveMode, debugMode);
            return rt;
        }

        // Composites straight into an existing target (no per-call RenderTexture
        // allocation) - used by the live preview every frame. cgDepthEncoded marks
        // cgDepth as a Unity DepthNormals-encoded texture (live CG path) rather than
        // metres in .r (snapshot path); cgFarClipMetres is needed to decode it.
        public static void CompositeInto(
            RenderTexture destination,
            Texture cgColor, Texture cgDepth, Texture plateColor, Texture plateDepth,
            bool naiveMode, bool debugMode = false,
            bool cgDepthEncoded = false, float cgFarClipMetres = 0f,
            bool undistortPlate = false, bool distortCg = false)
        {
            var mat = GetMaterial();
            mat.SetTexture("_CGColorTex", cgColor);
            mat.SetTexture("_CGDepthTex", cgDepth);
            mat.SetTexture("_PlateColorTex", plateColor);
            mat.SetTexture("_PlateDepthTex", plateDepth);
            mat.SetFloat("_NaiveMode", naiveMode ? 1f : 0f);
            mat.SetFloat("_DepthNearest", PostProductionSettings.DepthNearest ? 1f : 0f);
            mat.SetFloat("_DebugMode", debugMode ? 1f : 0f);
            mat.SetFloat("_CGDepthEncoded", cgDepthEncoded ? 1f : 0f);
            mat.SetFloat("_CGFarClipMetres", cgFarClipMetres);
            PostProductionLens.SetShaderParams(mat, undistortPlate, distortCg);
            Graphics.Blit(cgColor, destination, mat);
        }

        // TEMP DEBUG (stage 5 bring-up): renders the depth/colour sampling-
        // alignment visualization (see _DebugMode in PostProdComposite.shader)
        // through the real GPU pipeline - the actual test this should have been
        // from the start, rather than inspecting raw buffer bytes outside the
        // shader. Only needs CG color + CG depth; plate slots are unused in this
        // mode so cgColor/cgDepth are reused as harmless placeholders.
        public static string DebugDepthColorAlignment(
            string cgColorPath, string cgDepthPath, string outputPath)
        {
            var cgColorTex = LoadPng(cgColorPath, point: true);
            float[] cgDepthPixels = ExrDepthReader.ReadSingleChannelFloat(
                cgDepthPath, out int cgDepthW, out int cgDepthH);
            var cgDepthTex = BuildDepthTexture(cgDepthPixels, cgDepthW, cgDepthH, bilinear: false);

            var rt = Composite(
                cgColorTex, cgDepthTex, cgColorTex, cgDepthTex,
                cgColorTex.width, cgColorTex.height, naiveMode: false, debugMode: true);

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var readback = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            readback.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            readback.Apply();
            RenderTexture.active = prevActive;

            File.WriteAllBytes(outputPath, readback.EncodeToPNG());

            UnityEngine.Object.Destroy(cgColorTex);
            UnityEngine.Object.Destroy(cgDepthTex);
            UnityEngine.Object.Destroy(readback);
            rt.Release();

            return $"wrote {outputPath} ({readback.width}x{readback.height})";
        }

        // TEMP DEBUG / regression-test entry point (see
        // query.postprod.compositor.test in ApiManager.cs and
        // POSTPROD_PREVIEW_PLAN.md stage 5's test description): runs the
        // compositor on four explicit files and writes the result to a PNG, for
        // direct comparison against the equivalent frame from the existing
        // make_composite_video.py output.
        public static string TestFromFiles(
            string cgColorPath, string cgDepthPath,
            string plateColorPath, string plateDepthPath,
            string outputPath, bool naiveMode)
        {
            var cgColorTex = LoadPng(cgColorPath, point: true);
            float[] cgDepthPixels = ExrDepthReader.ReadSingleChannelFloat(
                cgDepthPath, out int cgDepthW, out int cgDepthH);
            var cgDepthTex = BuildDepthTexture(cgDepthPixels, cgDepthW, cgDepthH, bilinear: false);
            DumpDepthTextureRaw(cgDepthTex, outputPath + ".cgdepth.raw");

            var plateColorTex = LoadPng(plateColorPath, point: false);
            float[] plateDepthPixels = ExrDepthReader.ReadSingleChannelFloat(
                plateDepthPath, out int plateDepthW, out int plateDepthH);
            var plateDepthTex = BuildDepthTexture(plateDepthPixels, plateDepthW, plateDepthH, bilinear: true);
            DumpDepthTextureRaw(plateDepthTex, outputPath + ".platedepth.raw");

            var rt = Composite(
                cgColorTex, cgDepthTex, plateColorTex, plateDepthTex,
                cgColorTex.width, cgColorTex.height, naiveMode);

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var readback = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            readback.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            readback.Apply();
            RenderTexture.active = prevActive;

            File.WriteAllBytes(outputPath, readback.EncodeToPNG());

            UnityEngine.Object.Destroy(cgColorTex);
            UnityEngine.Object.Destroy(cgDepthTex);
            UnityEngine.Object.Destroy(plateColorTex);
            UnityEngine.Object.Destroy(plateDepthTex);
            UnityEngine.Object.Destroy(readback);
            rt.Release();

            return $"wrote {outputPath} ({readback.width}x{readback.height})";
        }

        // TEMP DEBUG: dumps a depth Texture2D's raw GPU-side pixel data exactly as
        // Unity stored it (i.e. after whatever internal row-order Unity applies -
        // this is deliberately NOT re-flipped or otherwise "corrected", since the
        // point is to see what the shader actually samples from, to check it
        // against the known-correct Python-read values). Format: int32 width,
        // int32 height, then width*height float32 values, row-major as stored.
        public static void DumpDepthTextureRaw(Texture2D tex, string outputPath)
        {
            NativeArray<float> data = tex.GetRawTextureData<float>();
            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);
            bw.Write(tex.width);
            bw.Write(tex.height);
            float[] arr = data.ToArray();
            var bytes = new byte[arr.Length * 4];
            Buffer.BlockCopy(arr, 0, bytes, 0, bytes.Length);
            bw.Write(bytes);
        }

        private static Texture2D LoadPng(string path, bool point)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            tex.LoadImage(bytes);
            tex.filterMode = point ? FilterMode.Point : FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }
    }
}
