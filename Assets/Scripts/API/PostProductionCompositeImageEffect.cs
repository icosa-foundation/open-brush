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
using UnityEngine;

namespace TiltBrush
{
    // Replaces the target camera's rendered output with the stage-5 compositor's
    // result, when both a CG capture and a plate are available - stage 6, step 3
    // of POSTPROD_PREVIEW_PLAN.md. Must live on the camera's own GameObject
    // (Unity's OnRenderImage requirement), so PostProductionPreviewController
    // adds/removes this component dynamically rather than owning the logic itself.
    //
    // Falls back to passing the camera's normal render through untouched if the CG
    // capture or plate hasn't been set up yet, so postprod.preview alone (stages 1-2)
    // keeps working exactly as already tested, and only "upgrades" to a full
    // composite once plate files are also configured.
    public class PostProductionCompositeImageEffect : MonoBehaviour
    {
        public static bool NaiveMode;

        // See postprod.preview.cgonly - shows the live calibrated camera feed
        // directly, ignoring any configured plate entirely (without discarding it,
        // so toggling back off doesn't need the plate re-set).
        public static bool CgOnlyMode;

        private static bool s_CgDirty = true;
        private static bool s_PlateDirty;
        private static string s_PlateColorPath = "";
        private static string s_PlateDepthPath = "";

        // Unity's own missing-texture convention - used here as an unmistakable
        // "never silently substitute" flag (see POSTPROD_PREVIEW_PLAN.md's
        // out-of-range/missing-media design) rather than passing bad content
        // through untouched.
        private static readonly Color s_MediaOfflineColor = new Color(1f, 0f, 1f, 1f);
        // Amber border: the plate on screen is the previous frame, the requested one is
        // still loading into the fast-plate cache.
        private static readonly Color s_HoldingPlateColor = new Color(1f, 0.6f, 0f, 1f);

        private static Texture2D s_SolidColorTex;
        private static Texture2D s_DummyDepthTex;
        private static Material s_BorderMaterial;

        private Texture2D m_CgColorTex;
        private Texture2D m_CgDepthTex;
        private Texture2D m_PlateColorTex;
        private Texture2D m_PlateDepthTex;
        private bool m_HasCg;
        private bool m_HasPlateColor;
        private bool m_HasPlateDepth;

        public static string LastStatus { get; private set; } = "not started";

        // Editor Play sessions keep static state (no domain reload). The cached helper
        // textures and material are kept: their getters recreate them if they were destroyed.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            NaiveMode = false;
            CgOnlyMode = false;
            s_CgDirty = true;
            s_PlateDirty = false;
            s_PlateColorPath = "";
            s_PlateDepthPath = "";
            LastStatus = "not started";
        }

        // Called whenever a fresh CG capture has been written (see
        // PostProductionPreviewController.RefreshCgCapture), so the next
        // OnRenderImage reloads it from disk instead of reusing a stale texture.
        public static void InvalidateCgCache()
        {
            s_CgDirty = true;
        }

        public static void SetPlatePaths(string colorPath, string depthPath)
        {
            s_PlateColorPath = colorPath ?? "";
            s_PlateDepthPath = depthPath ?? "";
            s_PlateDirty = true;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (CgOnlyMode)
            {
                // Live calibrated feed, not a static CG snapshot - stays continuous
                // instead of only updating on postprod.preview.refreshcg, and needs
                // no plate at all, configured or not. With live CG on, show its render
                // rather than the main camera, so it matches the composite exactly:
                // undistorted working frame, spatial offsets (global and per layer).
                bool liveCg = PostProductionLiveCg.Enabled && PostProductionLiveCg.HasFrame;
                Graphics.Blit(liveCg ? (Texture)PostProductionLiveCg.ColorRT : source, destination);
                return;
            }

            // Live CG (POSTPROD_FAST_PREVIEW_DESIGN.md 4.2) replaces the snapshot textures
            // when enabled and a frame has been rendered; the snapshot path is untouched
            // otherwise, so the two can be compared.
            bool live = PostProductionLiveCg.Enabled && PostProductionLiveCg.HasFrame;
            if (!live)
            {
                EnsureCgTextures();
            }
            // Undistorted working frame: only the live CG renders in it (the snapshot path
            // is the raw, non-overscanned frame), so undistortion follows the live CG.
            bool undistort = live && PostProductionLens.Active;
            Texture cgColor = live ? (Texture)PostProductionLiveCg.ColorRT : m_CgColorTex;
            Texture cgDepth = live ? (Texture)PostProductionLiveCg.EncodedDepthRT : m_CgDepthTex;
            bool hasCg = live || m_HasCg;
            float farMetres = live ? PostProductionLiveCg.FarClipMetres : 0f;

            // Plate: the fast-preview cache (design doc 4.1) when on, else the full-res files.
            Texture plateColor, plateDepth;
            bool plateConfigured, hasPlateColor, hasPlateDepth;
            bool holdingPlate = false;
            if (PostProductionPlateCache.Enabled)
            {
                var state = PostProductionPlateCache.TryGetPlate(out var cachedColor, out var cachedDepth);
                if (state == PostProductionPlateCache.PlateState.Loading)
                {
                    // Nothing cached yet (first frames after enabling): show the camera view.
                    Graphics.Blit(source, destination);
                    return;
                }
                plateConfigured = true;
                hasPlateColor = state != PostProductionPlateCache.PlateState.OutOfRange && cachedColor != null;
                hasPlateDepth = cachedDepth != null;
                holdingPlate = state == PostProductionPlateCache.PlateState.Holding;
                plateColor = cachedColor;
                plateDepth = cachedDepth;
            }
            else
            {
                EnsurePlateTextures();
                plateConfigured = !string.IsNullOrEmpty(s_PlateColorPath) && !string.IsNullOrEmpty(s_PlateDepthPath);
                hasPlateColor = m_HasPlateColor;
                hasPlateDepth = m_HasPlateDepth;
                plateColor = m_PlateColorTex;
                plateDepth = m_PlateDepthTex;
            }

            if (!hasCg || !plateConfigured)
            {
                // No CG capture yet, or postprod.preview.plate was never called -
                // legitimate startup state, unrelated to "missing media" below.
                // postprod.preview alone (stages 1-2) keeps working as a plain view.
                Graphics.Blit(source, destination);
                return;
            }

            if (!hasPlateColor)
            {
                // Plate WAS explicitly configured but its color file is missing or
                // unreadable (or the frame is outside the plate range) - never fall back
                // to the raw camera view once real media was requested; flag it instead.
                FillSolidColor(destination, s_MediaOfflineColor);
                return;
            }

            if (!hasPlateDepth)
            {
                // Color is fine but depth isn't (the realistic case - see
                // POSTPROD_PREVIEW_PLAN.md) - degrade to naive-over rather than
                // block the preview, but flag it visibly so it's never mistaken for
                // a real depth-aware composite. _PlateDepthTex is sampled but
                // unused by the shader's naive branch, so a dummy texture is safe.
                PostProdCompositor.CompositeInto(
                    destination, cgColor, cgDepth, plateColor, GetDummyDepthTexture(),
                    naiveMode: true, cgDepthEncoded: live, cgFarClipMetres: farMetres,
                    undistortPlate: undistort);
                DrawBorder(destination, s_MediaOfflineColor);
                return;
            }

            PostProdCompositor.CompositeInto(
                destination, cgColor, cgDepth, plateColor, plateDepth,
                NaiveMode, cgDepthEncoded: live, cgFarClipMetres: farMetres,
                undistortPlate: undistort);
            if (holdingPlate)
            {
                // The plate shown is the previous frame while the requested one loads.
                DrawBorder(destination, s_HoldingPlateColor);
            }
        }

        private void EnsureCgTextures()
        {
            if (!s_CgDirty && m_HasCg) { return; }
            s_CgDirty = false;

            string colorPath = PostProductionPreviewController.CgCaptureColorPath;
            string depthPath = PostProductionPreviewController.CgCaptureDepthPath;
            if (!File.Exists(colorPath) || !File.Exists(depthPath))
            {
                m_HasCg = false;
                LastStatus = $"CG capture not found yet (color={colorPath} depth={depthPath})";
                return;
            }

            try
            {
                DestroyIfNotNull(ref m_CgColorTex);
                DestroyIfNotNull(ref m_CgDepthTex);

                var bytes = File.ReadAllBytes(colorPath);
                m_CgColorTex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                m_CgColorTex.LoadImage(bytes);
                m_CgColorTex.filterMode = FilterMode.Point;

                float[] depthPixels = ExrDepthReader.ReadSingleChannelFloat(
                    depthPath, out int w, out int h);
                m_CgDepthTex = PostProdCompositor.BuildDepthTexture(depthPixels, w, h, bilinear: false);

                m_HasCg = true;
                LastStatus = $"CG loaded: {m_CgColorTex.width}x{m_CgColorTex.height}, depth {w}x{h}";
            }
            catch (Exception e)
            {
                m_HasCg = false;
                LastStatus = $"CG load error: {e.Message}";
            }
        }

        private void EnsurePlateTextures()
        {
            if (!s_PlateDirty && m_HasPlateColor && m_HasPlateDepth) { return; }
            s_PlateDirty = false;
            m_HasPlateColor = false;
            m_HasPlateDepth = false;

            if (string.IsNullOrEmpty(s_PlateColorPath) || string.IsNullOrEmpty(s_PlateDepthPath))
            {
                LastStatus = "plate not set - call postprod.preview.plate first";
                return;
            }

            // Color and depth are checked and reported independently - color
            // missing means there is nothing to show at all (media-offline);
            // depth missing alone still leaves a usable naive-over plate. See
            // POSTPROD_PREVIEW_PLAN.md's out-of-range/missing-media design.
            if (!File.Exists(s_PlateColorPath))
            {
                LastStatus = $"plate COLOR file missing ({s_PlateColorPath}) - showing media-offline placeholder";
                return;
            }

            try
            {
                DestroyIfNotNull(ref m_PlateColorTex);
                var bytes = File.ReadAllBytes(s_PlateColorPath);
                m_PlateColorTex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                m_PlateColorTex.LoadImage(bytes);
                m_PlateColorTex.filterMode = FilterMode.Bilinear;
                m_HasPlateColor = true;
            }
            catch (Exception e)
            {
                LastStatus = $"plate color load error: {e.Message}";
                return;
            }

            if (!File.Exists(s_PlateDepthPath))
            {
                LastStatus = $"plate loaded: {m_PlateColorTex.width}x{m_PlateColorTex.height} - " +
                    $"DEPTH file missing ({s_PlateDepthPath}), falling back to naive composite";
                return;
            }

            try
            {
                DestroyIfNotNull(ref m_PlateDepthTex);
                float[] depthPixels = ExrDepthReader.ReadSingleChannelFloat(
                    s_PlateDepthPath, out int w, out int h);
                m_PlateDepthTex = PostProdCompositor.BuildDepthTexture(depthPixels, w, h, bilinear: true);
                m_HasPlateDepth = true;
                LastStatus = $"plate loaded: {m_PlateColorTex.width}x{m_PlateColorTex.height}, depth {w}x{h}";
            }
            catch (Exception e)
            {
                LastStatus = $"plate depth load error: {e.Message}";
            }
        }

        // Fills the whole destination with a flat color - the "media offline" card.
        // A 1x1 texture blitted full-screen samples the same texel everywhere.
        private static void FillSolidColor(RenderTexture destination, Color color)
        {
            if (s_SolidColorTex == null)
            {
                s_SolidColorTex = new Texture2D(1, 1, TextureFormat.RGB24, false);
            }
            s_SolidColorTex.SetPixel(0, 0, color);
            s_SolidColorTex.Apply(false);
            Graphics.Blit(s_SolidColorTex, destination);
        }

        // A 1x1 black depth texture, safe to pass as _PlateDepthTex in naive mode -
        // the shader's naive branch samples but never reads that value.
        private static Texture2D GetDummyDepthTexture()
        {
            if (s_DummyDepthTex == null)
            {
                s_DummyDepthTex = new Texture2D(1, 1, TextureFormat.RFloat, false);
                s_DummyDepthTex.SetPixelData(new float[] { 0f }, 0);
                s_DummyDepthTex.Apply(false);
            }
            return s_DummyDepthTex;
        }

        private static Material GetBorderMaterial()
        {
            if (s_BorderMaterial == null)
            {
                // Built-in Unity shader made for exactly this: drawing flat-colored
                // GL geometry directly onto a render target, no shader asset needed.
                s_BorderMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
                s_BorderMaterial.hideFlags = HideFlags.HideAndDontSave;
                s_BorderMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                s_BorderMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                s_BorderMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                s_BorderMaterial.SetInt("_ZWrite", 0);
            }
            return s_BorderMaterial;
        }

        // Draws a solid border directly onto destination - the visible flag that
        // distinguishes an automatic naive-fallback (depth unavailable) from a
        // correct depth-aware composite, without needing any font rendering.
        private static void DrawBorder(RenderTexture destination, Color color)
        {
            const int thicknessPx = 12;
            var prevActive = RenderTexture.active;
            RenderTexture.active = destination;

            GL.PushMatrix();
            GL.LoadPixelMatrix(0, destination.width, destination.height, 0);
            GetBorderMaterial().SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Color(color);
            DrawRect(0, 0, destination.width, thicknessPx);
            DrawRect(0, destination.height - thicknessPx, destination.width, thicknessPx);
            DrawRect(0, 0, thicknessPx, destination.height);
            DrawRect(destination.width - thicknessPx, 0, thicknessPx, destination.height);
            GL.End();
            GL.PopMatrix();

            RenderTexture.active = prevActive;
        }

        private static void DrawRect(float x, float y, float w, float h)
        {
            GL.Vertex3(x, y, 0);
            GL.Vertex3(x + w, y, 0);
            GL.Vertex3(x + w, y + h, 0);
            GL.Vertex3(x, y + h, 0);
        }

        private static void DestroyIfNotNull(ref Texture2D tex)
        {
            if (tex != null) { Destroy(tex); }
            tex = null;
        }

        private void OnDestroy()
        {
            DestroyIfNotNull(ref m_CgColorTex);
            DestroyIfNotNull(ref m_CgDepthTex);
            DestroyIfNotNull(ref m_PlateColorTex);
            DestroyIfNotNull(ref m_PlateDepthTex);
        }
    }
}
