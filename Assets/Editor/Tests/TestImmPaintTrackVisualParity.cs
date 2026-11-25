using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TiltBrush.FrameAnimation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace TiltBrush.Tests
{
    internal class TestImmPaintTrackVisualParity
    {
        const int Size = 384;
        const int CaptureLayer = 30;
        static readonly string[] Sections = { "circle", "ellipse", "square", "ribbon" };
        static readonly string[] Brushes = {
            Quill.BRUSH_QUILL_CYLINDER, Quill.BRUSH_QUILL_ELLIPSE, Quill.BRUSH_QUILL_CUBE, Quill.BRUSH_QUILL_RIBBON
        };

        [UnitySetUp]
        public IEnumerator EnterRuntime() => new TestAnimationCanvasLifecycle().EnterRuntime();

        [UnityTearDown]
        public IEnumerator ExitRuntime() => new TestAnimationCanvasLifecycle().ExitRuntime();

        [UnityTest]
        public IEnumerator CanvasAndImmRenderTheSameQuillDrawing()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("The IMM image comparison needs a graphics device.");
            var manager = App.Scene.animationUI_manager;
            manager.StartTimeline();
            manager.ConfigureLegacyAnimationTracks(new List<IReadOnlyList<int>> { new[] { 4 }, new[] { 4 } },
                new List<bool> { true, true });
            var canvas = manager.GetOrCreateContentCanvas(1, 0);
            var centers = new Vector3[Brushes.Length];
            Color[] colors = { new Color(0.85f, 0.25f, 0.14f), new Color(0.2f, 0.85f, 0.31f),
                new Color(0.25f, 0.42f, 0.94f), new Color(0.9f, 0.69f, 0.12f) };
            var tangent = new Vector3(1, 0.04f, 0.08f).normalized;
            for (int row = 0; row < Brushes.Length; ++row)
            {
                Vector3 start = new Vector3(-0.6f + row * 0.04f, 0.48f - row * 0.32f, 0);
                var points = Enumerable.Range(0, 14).Select(i => new PointerManager.ControlPoint
                {
                    m_Pos = start + tangent * (i * 0.09f),
                    m_Orient = Quaternion.LookRotation(tangent, Vector3.back),
                    m_Pressure = 0.7f + 0.3f * i / 13f,
                    m_TimestampMs = (uint)(row * 100 + i)
                }).ToArray();
                var stroke = new Stroke {
                    m_BrushGuid = new Guid(Brushes[row]), m_Color = colors[row], m_BrushSize = 0.13f, m_BrushScale = 1,
                    m_ControlPoints = points, m_ControlPointsToDrop = new bool[points.Length]
                };
                stroke.Recreate(canvas: canvas);
                Assert.Greater(stroke.m_BatchSubset.m_nTriIndex, 0, Sections[row]);
                SketchMemoryScript.m_Instance.MemoryListAdd(stroke);
                centers[row] = canvas.transform.TransformPoint((points[0].m_Pos + points[13].m_Pos) * 0.5f);
            }

            manager.SelectTimelineFrame(1, 2);
            var renderers = canvas.GetComponentsInChildren<MeshRenderer>();
            Assert.IsNotEmpty(renderers);
            int[] layers = renderers.Select(r => r.gameObject.layer).ToArray();
            var cameraObject = new GameObject("IMM paint comparison camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.CopyFrom(App.VrSdk.GetVrCamera());
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.cullingMask = 1 << CaptureLayer;
            camera.orthographic = false;
            camera.fieldOfView = 35;
            float scale = canvas.transform.TransformVector(Vector3.right).magnitude;
            camera.transform.SetPositionAndRotation(canvas.transform.TransformPoint(new Vector3(0, 0, -2.8f)),
                canvas.transform.rotation);
            camera.nearClipPlane = 0.1f * scale;
            camera.farClipPlane = 10 * scale;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var target = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            camera.targetTexture = target;
            var rows = centers.Select(v => camera.WorldToViewportPoint(v).y * Size).ToArray();
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/ImmPaintVisualParity"));
            Directory.CreateDirectory(directory);
            NativeImmRenderer nativeCamera = null;
            try
            {
                foreach (var renderer in renderers) renderer.gameObject.layer = CaptureLayer;
                camera.cullingMask = 0;
                Color32[] background = Capture(camera, target, directory, "background");
                camera.cullingMask = 1 << CaptureLayer;
                Color32[] canvasPixels = Capture(camera, target, directory, "canvas");

                var options = new SerializedObject(manager);
                options.FindProperty("m_ImmPaintTrackIndex").intValue = 1;
                options.FindProperty("m_UseImmPaintTrack").boolValue = true;
                options.ApplyModifiedPropertiesWithoutUndo();
                manager.TimelineSlideDown(true);
                float deadline = Time.realtimeSinceStartup + 30;
                while (Time.realtimeSinceStartup < deadline && (nativeCamera == null || canvas.gameObject.activeSelf))
                {
                    if (nativeCamera == null) nativeCamera = NativeImmRenderer.RetainForCapture(camera);
                    camera.Render();
                    yield return null;
                }
                Assert.IsNotNull(nativeCamera, "Capture camera registration");
                Assert.IsFalse(canvas.gameObject.activeSelf, "IMM handoff");
                // Exclude all Unity geometry from the native capture.
                camera.cullingMask = 0;
                camera.Render();
                yield return null;
                Color32[] nativePixels = Capture(camera, target, directory, "imm");

                var reports = Sections.Select(s => new Comparison { section = s }).ToArray();
                for (int y = 0; y < Size; ++y)
                {
                    int nearest = 0;
                    for (int row = 1; row < rows.Length; ++row)
                        if (Mathf.Abs(y - rows[row]) < Mathf.Abs(y - rows[nearest])) nearest = row;
                    for (int x = 0; x < Size; ++x)
                    {
                        int index = y * Size + x;
                        reports[nearest].Add(x, y, canvasPixels[index], nativePixels[index], background[index]);
                    }
                }
                foreach (var report in reports) report.Finish();
                File.WriteAllText(Path.Combine(directory, "metrics.json"),
                    JsonUtility.ToJson(new Results { sections = reports }, true));
                foreach (var report in reports)
                {
                    Assert.Greater(report.canvasPixels, 100, $"Canvas coverage for {report.section}");
                    Assert.Greater(report.immPixels, 100, $"IMM coverage for {report.section}");
                    Assert.Greater(report.iou, 0.65f, $"Foreground overlap for {report.section}");
                    Assert.Less(report.centroidDistancePixels, 6, $"Centroid displacement for {report.section}");
                    Assert.Less(report.meanChannelDifference, 32, $"Overlapping foreground RGB for {report.section}");
                }
            }
            finally
            {
                manager.TimelineSlideDown(false);
                if (nativeCamera != null) nativeCamera.Release();
                for (int i = 0; i < renderers.Length; ++i)
                    if (renderers[i] != null) renderers[i].gameObject.layer = layers[i];
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        static Color32[] Capture(Camera camera, RenderTexture target, string directory, string name)
        {
            camera.Render();
            var previous = RenderTexture.active;
            var image = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
                return image.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        [Serializable] class Results { public Comparison[] sections; }

        [Serializable] class Comparison
        {
            public string section;
            public int canvasPixels, immPixels, intersection;
            public float iou, centroidDistancePixels, meanChannelDifference;
            Vector2 canvasCenter, immCenter;
            long channelDifference;

            public void Add(int x, int y, Color32 canvas, Color32 imm, Color32 background)
            {
                bool a = Foreground(canvas, background);
                bool b = Foreground(imm, background);
                if (a) { canvasPixels++; canvasCenter += new Vector2(x, y); }
                if (b) { immPixels++; immCenter += new Vector2(x, y); }
                if (a && b)
                {
                    intersection++;
                    channelDifference += Math.Abs(canvas.r - imm.r) + Math.Abs(canvas.g - imm.g) + Math.Abs(canvas.b - imm.b);
                }
            }

            static bool Foreground(Color32 c, Color32 background) =>
                Math.Max(Math.Abs(c.r - background.r), Math.Max(Math.Abs(c.g - background.g), Math.Abs(c.b - background.b))) > 20;

            public void Finish()
            {
                iou = intersection / (float)Math.Max(1, canvasPixels + immPixels - intersection);
                centroidDistancePixels = Vector2.Distance(canvasCenter / Math.Max(1, canvasPixels), immCenter / Math.Max(1, immPixels));
                meanChannelDifference = channelDifference / (3f * Math.Max(1, intersection));
            }
        }
    }
}
