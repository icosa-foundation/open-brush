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

using UnityEngine;

namespace TiltBrush
{
    // Lens undistortion for the post-production preview (POSTPROD_FAST_PREVIEW_DESIGN.md
    // section 4.6). Open Brush works fully undistorted: the CG is a pinhole render, and the
    // real plate (colour and depth) is undistorted in the compositor through the real
    // camera's OpenCV calibration (camera matrix + k1,k2,p1,p2,k3).
    //
    // The undistorted working frame is overscanned: straightening this lens pushes the
    // frame edges outward, so the working frame is enlarged (same focal length, shifted
    // principal point) until every original pixel fits. Nothing is lost, and the
    // distortion can be re-applied on export.
    public static class PostProductionLens
    {
        public static bool Undistort { get; set; }
        public static bool HasCalibration { get; private set; }

        // Source (real camera) calibration, in pixels at the calibration resolution, which
        // is the same reference resolution as camera.intrinsics.
        public static float SrcFx { get; private set; }
        public static float SrcFy { get; private set; }
        public static float SrcCx { get; private set; }
        public static float SrcCy { get; private set; }
        public static float K1 { get; private set; }
        public static float K2 { get; private set; }
        public static float P1 { get; private set; }
        public static float P2 { get; private set; }
        public static float K3 { get; private set; }

        // Undistorted, overscanned working frame: the CG camera's own intrinsics
        // (camera.intrinsics) with the frame enlarged and the principal point shifted.
        public static float WorkFx { get; private set; }
        public static float WorkFy { get; private set; }
        public static float WorkCx { get; private set; }
        public static float WorkCy { get; private set; }
        public static int WorkWidth { get; private set; }
        public static int WorkHeight { get; private set; }

        public static volatile string LastStatus = "no lens calibration set";

        // Editor Play sessions keep static state (no domain reload); start with no calibration.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Clear();

        public static void Clear()
        {
            Undistort = false;
            HasCalibration = false;
            SrcFx = SrcFy = SrcCx = SrcCy = 0;
            K1 = K2 = P1 = P2 = K3 = 0;
            WorkFx = WorkFy = WorkCx = WorkCy = 0;
            WorkWidth = WorkHeight = 0;
            LastStatus = "no lens calibration set";
        }

        // True when the preview should run in the undistorted working frame.
        public static bool Active =>
            Undistort && HasCalibration && ScreenshotManager.UseCalibratedProjection;

        public static float[] CalibrationArray => HasCalibration
            ? new[] { SrcFx, SrcFy, SrcCx, SrcCy, K1, K2, P1, P2, K3 }
            : null;

        public static void SetCalibration(float fx, float fy, float cx, float cy,
            float k1, float k2, float p1, float p2, float k3)
        {
            SrcFx = fx; SrcFy = fy; SrcCx = cx; SrcCy = cy;
            K1 = k1; K2 = k2; P1 = p1; P2 = p2; K3 = k3;
            HasCalibration = true;
            RecomputeWorkingFrame();
        }

        // Must be re-run when camera.intrinsics changes (the working frame is built on it).
        public static void RecomputeWorkingFrame()
        {
            if (!HasCalibration || !ScreenshotManager.UseCalibratedProjection)
            {
                LastStatus = HasCalibration
                    ? "lens calibration set, but no camera.intrinsics yet"
                    : "no lens calibration set";
                return;
            }
            float w = ScreenshotManager.CalibratedRefWidth;
            float h = ScreenshotManager.CalibratedRefHeight;
            float fu = ScreenshotManager.CalibratedFx, fv = ScreenshotManager.CalibratedFy;
            float cu = ScreenshotManager.CalibratedCx, cv = ScreenshotManager.CalibratedCy;

            // Undistort the source frame's border into the CG camera's pixel space and take
            // the extents (same approach as cv2.undistortPoints with P = CG camera matrix).
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            const int n = 400;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Extend(t * (w - 1), 0);
                Extend(t * (w - 1), h - 1);
                Extend(0, t * (h - 1));
                Extend(w - 1, t * (h - 1));
            }
            void Extend(float u, float v)
            {
                UndistortNormalized((u - SrcCx) / SrcFx, (v - SrcCy) / SrcFy, out float x, out float y);
                float pu = fu * x + cu, pv = fv * y + cv;
                minX = Mathf.Min(minX, pu); maxX = Mathf.Max(maxX, pu);
                minY = Mathf.Min(minY, pv); maxY = Mathf.Max(maxY, pv);
            }

            float padL = Mathf.Max(0f, -minX), padT = Mathf.Max(0f, -minY);
            float padR = Mathf.Max(0f, maxX - (w - 1)), padB = Mathf.Max(0f, maxY - (h - 1));
            WorkWidth = Mathf.CeilToInt(w + padL + padR);
            WorkHeight = Mathf.CeilToInt(h + padT + padB);
            WorkFx = fu;
            WorkFy = fv;
            WorkCx = cu + Mathf.Ceil(padL);
            WorkCy = cv + Mathf.Ceil(padT);
            LastStatus = $"lens calibration set; undistort {(Undistort ? "on" : "off")}; working frame " +
                $"{WorkWidth}x{WorkHeight} (pad L{padL:F1} R{padR:F1} T{padT:F1} B{padB:F1}), " +
                $"principal point {WorkCx:F2},{WorkCy:F2}";
        }

        // Inverse of the OpenCV distortion model, by fixed-point iteration (as
        // cv2.undistortPoints does). Input/output are normalised camera coordinates.
        private static void UndistortNormalized(float xd, float yd, out float x, out float y)
        {
            x = xd;
            y = yd;
            for (int i = 0; i < 20; i++)
            {
                float r2 = x * x + y * y;
                float icdist = 1f / (1f + ((K3 * r2 + K2) * r2 + K1) * r2);
                float deltaX = 2f * P1 * x * y + P2 * (r2 + 2f * x * x);
                float deltaY = P1 * (r2 + 2f * y * y) + 2f * P2 * x * y;
                x = (xd - deltaX) * icdist;
                y = (yd - deltaY) * icdist;
            }
        }

        // Sets the shader uniforms PostProdComposite.shader uses to undistort the plate
        // (preview), or to distort the CG into the original lens (export). Both are always
        // written: the material is shared by the preview and the export.
        public static void SetShaderParams(Material mat, bool active, bool distortCg = false)
        {
            mat.SetFloat("_UndistortPlate", active && !distortCg ? 1f : 0f);
            mat.SetFloat("_DistortCG", distortCg ? 1f : 0f);
            if (!active && !distortCg) { return; }
            mat.SetVector("_WorkK", new Vector4(WorkFx, WorkFy, WorkCx, WorkCy));
            mat.SetVector("_WorkSize", new Vector4(WorkWidth, WorkHeight, 0, 0));
            mat.SetVector("_SrcK", new Vector4(SrcFx, SrcFy, SrcCx, SrcCy));
            mat.SetVector("_SrcSize", new Vector4(
                ScreenshotManager.CalibratedRefWidth, ScreenshotManager.CalibratedRefHeight, 0, 0));
            mat.SetVector("_Dist0", new Vector4(K1, K2, P1, P2));
            mat.SetFloat("_Dist1", K3);
        }
    }
}
