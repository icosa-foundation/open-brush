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
    // Drives the post-production live preview on the camera already painting the
    // desktop window (POSTPROD_PREVIEW_PLAN.md, stage 6) - not a new in-app panel,
    // per the brief. Reuses App.VrSdk.GetVrCamera(), confirmed to be the camera
    // already painting the window in NoHeadsetMode - not the Spectator Camera
    // (DropCamWidget), which is a placeable 3D prop, not a "what does the window
    // show" switch (ruled out during design).
    // Runs before other scripts each frame so the sketch clock it holds or drives (see
    // UpdateTransport) is already set when stroke playback reads it.
    [DefaultExecutionOrder(-10000)]
    public class PostProductionPreviewController : MonoBehaviour
    {
        private static PostProductionPreviewController m_Instance;
        public static PostProductionPreviewController Instance
        {
            get
            {
                if (m_Instance == null)
                {
                    var go = new GameObject("PostProductionPreviewController");
                    Object.DontDestroyOnLoad(go);
                    m_Instance = go.AddComponent<PostProductionPreviewController>();
                }
                return m_Instance;
            }
        }

        public bool Enabled { get; private set; }

        // Which camera to drive, by exact GameObject name. Empty (the default)
        // means App.VrSdk.GetVrCamera(). Settable via postprod.preview.camera,
        // since which camera actually paints the window turned out NOT to be a
        // safe assumption to hardcode - this session found a second camera
        // ("CenterEyeAnchor", an Oculus rig camera left active in Monoscopic mode)
        // rendering on top of App.VrSdk.GetVrCamera() and obscuring it entirely.
        // Name is the sensible identifier here rather than an index into
        // Camera.allCameras: that array's order isn't guaranteed stable across
        // frames, whereas a camera's name is - and postprod.debug.dumpcameras /
        // query.postprod.debug.allcameras already reports names directly, so
        // there's nothing an index would add.
        private static string s_TargetCameraName = "";

        // The calibrated extrinsics (real-camera position/rotation), if set via
        // postprod.preview.pose. Applied the same way app.snapshot.quat positions
        // the separate capture rig - App.Scene.AsScene[transform] = TrTransform.TR
        // (position, rotation) - NOT a raw world-space Transform assignment, since
        // the calibrated pose is defined in scene space and a raw assignment would
        // be wrong if the scene has any colocation offset from world origin.
        private static bool s_HasTargetPose;
        private static Vector3 s_TargetPosition;
        private static Quaternion s_TargetRotation;

        // Stage 2 auto-plate-sync (see POSTPROD_PREVIEW_PLAN.md): when set, the plate
        // is kept in sync with the current sketch time automatically every frame,
        // using PostProductionSettings.SketchTimeToC006Frame - no more manually
        // computing and setting postprod.preview.plate by hand for every scrub. Empty
        // disables it (the manual postprod.preview.plate path keeps working either
        // way). Deliberately only the plate (color+depth file paths, cheap to swap) -
        // the CG capture stays an explicit postprod.preview.refreshcg call, since a
        // snapshot costs ~1.4s and doing that every frame would be wasteful (see the
        // plan's "Known issues to revisit" on why CG capture is on-demand).
        private static string s_AutoPlateClipDir = "";
        private static int s_LastAutoPlateFrame = int.MinValue;
        public static string AutoPlateClipDir => s_AutoPlateClipDir;
        public static string LastAutoPlateStatus { get; private set; } = "auto-plate sync not enabled";

        // Plain-data status, safe to read from the HTTP listener thread (see
        // query.postprod.preview.status in ApiManager.cs).
        public static string LastStatus { get; private set; } = "not started";

        // Editor Play sessions keep static state (no domain reload); start with the preview off
        // and no camera setup, as a build does. The instance went with the previous scene.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            m_Instance = null;
            s_TargetCameraName = "";
            s_HasTargetPose = false;
            s_TargetPosition = Vector3.zero;
            s_TargetRotation = Quaternion.identity;
            s_AutoPlateClipDir = "";
            s_LastAutoPlateFrame = int.MinValue;
            LastAutoPlateStatus = "auto-plate sync not enabled";
            LastStatus = "not started";
            PreviewOn = false;
            s_LastSketchTimeMs = 0;
            s_Transport = Transport.Off;
            s_ShuttleRate = 0;
            s_LastSetSketchTime = 0;
            s_HaveLastSet = false;
            LayerVisibility = "";
            LayerNames = "";
            CgCaptureCount = 0;
            LastCgCaptureStatus = "not captured yet";
            AllCamerasReport = "not captured yet";
        }

        public static void SetTargetCameraName(string name)
        {
            s_TargetCameraName = name ?? "";
        }

        // Raw getters for stage 8 persistence.
        public static string TargetCameraName => s_TargetCameraName;
        public static bool HasTargetPose => s_HasTargetPose;
        public static Vector3 TargetPosition => s_TargetPosition;
        public static Quaternion TargetRotation => s_TargetRotation;

        public static void SetTargetPose(Vector3 position, Quaternion rotation)
        {
            s_TargetPosition = position;
            s_TargetRotation = rotation;
            s_HasTargetPose = true;
        }

        // clipDir must contain color_frames/ and depth_frames/ subfolders following
        // the established true-frame-number naming convention (frame_NNNNN_color.png /
        // frame_NNNNN_depth.exr - see DepthFrameResolver). Empty string disables sync.
        public static void SetAutoPlateClipDir(string clipDir)
        {
            s_AutoPlateClipDir = clipDir ?? "";
            s_LastAutoPlateFrame = int.MinValue; // force a resync on the next Update()
            LastAutoPlateStatus = string.IsNullOrEmpty(s_AutoPlateClipDir)
                ? "auto-plate sync disabled"
                : $"auto-plate sync enabled on {s_AutoPlateClipDir}";
        }

        // Keeps the plate in sync with the current sketch time. Only does real work
        // (file checks, SetPlate) when the mapped frame actually changes, since this
        // runs every frame while enabled.
        private void UpdateAutoPlate()
        {
            if (string.IsNullOrEmpty(s_AutoPlateClipDir)) { return; }

            double sketchTimeMs = App.Instance.CurrentSketchTime * 1000.0;
            if (sketchTimeMs >= SketchTimeSentinelThresholdMs) { return; }
            int requestedFrame = PostProductionSettings.SketchTimeToC006Frame(sketchTimeMs);

            // Fast plates: the cache serves the frame itself (no per-change file loads).
            if (PostProductionPlateCache.Enabled)
            {
                PostProductionPlateCache.SetPlayhead(requestedFrame);
                LastAutoPlateStatus = $"sketchTime={sketchTimeMs:F0}ms -> c006Frame={requestedFrame} (fast plates)";
                s_LastAutoPlateFrame = int.MinValue; // full-res path resyncs when fast plates turn off
                return;
            }
            if (requestedFrame == s_LastAutoPlateFrame) { return; }
            s_LastAutoPlateFrame = requestedFrame;

            string depthFramesDir = System.IO.Path.Combine(s_AutoPlateClipDir, PostProductionClip.PreviewDepthDir);
            string colorFramesDir = System.IO.Path.Combine(s_AutoPlateClipDir, "color_frames");
            try
            {
                bool inRange = DepthFrameResolver.TryResolve(depthFramesDir, requestedFrame, out int resolvedFrame);
                string depthPath = DepthFrameResolver.DepthPath(depthFramesDir, resolvedFrame);
                string colorPath = DepthFrameResolver.ColorPath(colorFramesDir, resolvedFrame);
                SetPlate(colorPath, depthPath);
                LastAutoPlateStatus = inRange
                    ? $"sketchTime={sketchTimeMs:F0}ms -> c006Frame={requestedFrame} (resolved {resolvedFrame})"
                    : $"sketchTime={sketchTimeMs:F0}ms -> c006Frame={requestedFrame} OUT OF RANGE, " +
                        $"clamped to nearest {resolvedFrame}";
            }
            catch (System.Exception e)
            {
                LastAutoPlateStatus = $"sketchTime={sketchTimeMs:F0}ms -> c006Frame={requestedFrame} ERROR: {e.Message}";
            }
        }

        private static Camera FindTargetCamera()
        {
            if (string.IsNullOrEmpty(s_TargetCameraName))
            {
                return App.VrSdk.GetVrCamera();
            }
            foreach (var c in Camera.allCameras)
            {
                if (c.name == s_TargetCameraName) { return c; }
            }
            return null;
        }

        // Camera pose (scene space) from just before Preview turned on, so Preview Off
        // can put it back - otherwise only the projection reset and the camera stayed
        // stuck at the calibrated pose.
        private bool m_HasPrePreviewPose;
        private TrTransform m_PrePreviewPose;
        // The calibrated projection also sets the camera's clip planes and field of view;
        // Preview Off puts the camera's own back (2026-10-25: left at the clip's 1-1000 units,
        // a new sketch opened to a black view instead of the environment).
        private bool m_HasPrePreviewLens;
        private float m_PrePreviewNear, m_PrePreviewFar, m_PrePreviewFov;

        // Mirrors Enabled for the HTTP thread (query.postprod.preview.settings).
        public static volatile bool PreviewOn;

        // A loaded clip is ready to preview: preview on, with the live CG and fast plates (the
        // only preview paths offered since 2026-10-24; full quality is the export's job).
        public static void StartPreviewForClip()
        {
            PostProductionLiveCg.Enabled = true;
            string error = PostProductionPlateCache.SetEnabled(true, s_AutoPlateClipDir);
            if (error != null) { PostProductionPlateCache.LastStatus = "error: " + error; }
            Instance.SetEnabled(true);
        }

        // postprod.load quick-loads the sketch, which leaves the scrub timeline finished at the
        // end; once loading is over the timeline is restarted and the playhead goes to the
        // export range start, else the first frame that can be composited, else the sketch
        // start (2026-10-24).
        public static void MovePlayheadAfterLoad()
        {
            App.Instance.StateChanged -= OnAppStateChangedAfterLoad;
            App.Instance.StateChanged += OnAppStateChangedAfterLoad;
        }

        private static void OnAppStateChangedAfterLoad(App.AppState oldState, App.AppState newState)
        {
            if (newState != App.AppState.Standard) { return; }
            App.Instance.StateChanged -= OnAppStateChangedAfterLoad;
            var memory = SketchMemoryScript.m_Instance;
            if (!memory.m_KeepPlaybackAliveForScrubbing)
            {
                Debug.Log("[PostProd] load: finished, not scrubbing so the playhead stays at the end");
                return;
            }
            memory.RestartScrubPlayback();
            double startMs = LoadStartSketchTimeMs(out string from);
            App.Instance.CurrentSketchTime = startMs / 1000.0;
            s_HaveLastSet = false;
            Debug.Log($"[PostProd] load: finished, playhead at {from}, sketch time {startMs:F0}ms");
        }

        private static double LoadStartSketchTimeMs(out string from)
        {
            int? frame = PostProductionSettings.CropStartFrame;
            from = $"export range start (frame {frame})";
            if (frame == null)
            {
                frame = PostProductionPlateCache.FirstCompositableFrame;
                from = $"clip start (frame {frame})";
            }
            if (frame == null)
            {
                from = "sketch start";
                return 0;
            }
            // Before the sketch begins the preview can't go (its clock stops at 0).
            return System.Math.Max(0.0, PostProductionSettings.C006FrameToSketchTimeMs(frame.Value));
        }

        // New Sketch ends a post-production session: nothing it set up may carry over into the
        // next sketch (2026-10-25). The camera gets its own projection and pose back, the
        // plate loader and transport stop, and the clip, calibrated camera, lens and every
        // offset, range and setting are cleared, as at the start of a session.
        public static void EndSession()
        {
            App.Instance.StateChanged -= OnAppStateChangedAfterLoad;
            PostProductionExport.Cancel();
            if (m_Instance != null && m_Instance.Enabled) { m_Instance.SetEnabled(false); }
            PostProductionPlateCache.SetEnabled(false, null);
            s_Transport = Transport.Off;
            s_HaveLastSet = false;
            SketchMemoryScript.m_Instance.m_KeepPlaybackAliveForScrubbing = false;
            s_TargetCameraName = "";
            s_HasTargetPose = false;
            s_AutoPlateClipDir = "";
            s_LastAutoPlateFrame = int.MinValue;
            LastAutoPlateStatus = "auto-plate sync not enabled";
            PostProductionCompositeImageEffect.NaiveMode = false;
            PostProductionCompositeImageEffect.CgOnlyMode = false;
            PostProductionLiveCg.Bloom = false;
            PostProductionLiveCg.Enabled = false;
            PostProductionClip.Clear();
            PostProductionLens.Clear();
            ScreenshotManager.ClearCalibratedCamera();
            PostProductionSettings.ClearSession();
            PostProductionSettings.LoadCount++;
            if (m_Instance != null)
            {
                m_Instance.enabled = false;
                Destroy(m_Instance.gameObject);
                m_Instance = null;
            }
            s_LastSketchTimeMs = 0;
            LayerVisibility = "";
            LayerNames = "";
        }

        // Every preview setting the control page shows, as key=value pairs.
        public static string SettingsReadout()
        {
            return $"preview={(PreviewOn ? 1 : 0)} " +
                $"occlusion={(PostProductionCompositeImageEffect.NaiveMode ? "off" : "depth")} " +
                $"background={(PostProductionCompositeImageEffect.CgOnlyMode ? "cg" : "video")} " +
                $"lens={(PostProductionLens.Undistort ? "undistorted" : "raw")} " +
                $"bloom={(PostProductionLiveCg.Bloom ? 1 : 0)} " +
                $"depth={(PostProductionSettings.DepthNearest ? "nearest" : "bilinear")} " +
                $"livecg={(PostProductionLiveCg.Enabled ? 1 : 0)} fastplates={(PostProductionPlateCache.Enabled ? 1 : 0)}";
        }

        public void SetEnabled(bool enabled)
        {
            bool wasEnabled = Enabled;
            Enabled = enabled;
            PreviewOn = enabled;
            Camera cam = FindTargetCamera();
            if (cam == null)
            {
                LastStatus = $"SetEnabled: target camera not found (name=\"{s_TargetCameraName}\", " +
                    "empty means App.VrSdk.GetVrCamera())";
                return;
            }

            if (enabled)
            {
                if (!wasEnabled && s_HasTargetPose)
                {
                    m_PrePreviewPose = App.Scene.AsScene[cam.transform];
                    m_HasPrePreviewPose = true;
                }
                if (!wasEnabled)
                {
                    m_PrePreviewNear = cam.nearClipPlane;
                    m_PrePreviewFar = cam.farClipPlane;
                    m_PrePreviewFov = cam.fieldOfView;
                    m_HasPrePreviewLens = true;
                }
                ScreenshotManager.ApplyCalibratedProjectionToCamera(cam);
                ApplyTargetPose(cam);
                if (cam.GetComponent<PostProductionCompositeImageEffect>() == null)
                {
                    cam.gameObject.AddComponent<PostProductionCompositeImageEffect>();
                }
            }
            else
            {
                ScreenshotManager.ClearCalibratedProjectionFromCamera(cam);
                if (wasEnabled && m_HasPrePreviewLens)
                {
                    cam.nearClipPlane = m_PrePreviewNear;
                    cam.farClipPlane = m_PrePreviewFar;
                    cam.fieldOfView = m_PrePreviewFov;
                    m_HasPrePreviewLens = false;
                }
                if (wasEnabled && m_HasPrePreviewPose)
                {
                    App.Scene.AsScene[cam.transform] = m_PrePreviewPose;
                    m_HasPrePreviewPose = false;
                }
                var fx = cam.GetComponent<PostProductionCompositeImageEffect>();
                if (fx != null) { Destroy(fx); }
            }
            ReportStatus(cam, "SetEnabled");
        }

        // Cached every frame regardless of Enabled, so query.sketch.currenttime works
        // for basic scrub testing even before postprod.preview is turned on -
        // App.Instance.CurrentSketchTime reads Unity's Time API, so it can only be
        // read here on the main thread, not directly from the HTTP listener thread.
        private static double s_LastSketchTimeMs;
        public static double LastSketchTimeMs => s_LastSketchTimeMs;

        // App.Instance.CurrentSketchTime is set to float.MaxValue by
        // SketchMemoryScript/ScenePlaybackByTime as an internal "fully loaded, past
        // the last stroke" sentinel (seen transiently during save/load) - never meant
        // to be user-facing. Reporting it raw through query.sketch.currenttime broke
        // a client UI (a scrub slider forced to ~3.4e+41 = float.MaxValue*1000 right
        // after a save). Clamp it out here rather than trusting every future caller
        // to know about this sentinel.
        private const double SketchTimeSentinelThresholdMs = float.MaxValue / 2.0;

        // Transport: holds (rate 0) or plays (any other rate, negative = reverse) the sketch
        // clock, driven here each frame rather than by the control page, so playback never
        // depends on HTTP round trips. Only the sketch clock is controlled: Unity's game
        // clock keeps running, because freezing it (Time.timeScale = 0) also froze Open
        // Brush's lighting fade-in after a load and left strokes grey (found 2026-10-21).
        //
        // The sketch clock is Time.timeSinceLevelLoad minus a base, so it advances by
        // Time.deltaTime each frame on its own; that advance is cancelled here and replaced
        // with rate x real time. A seek made by anything else (slider, API) shows up as the
        // clock not being where it was left, and is adopted rather than overridden. This
        // runs first each frame (DefaultExecutionOrder) so stroke playback sees the result.
        private enum Transport { Off, Hold, Shuttle }
        private static Transport s_Transport = Transport.Off;
        private static float s_ShuttleRate;
        private static double s_LastSetSketchTime;
        private static bool s_HaveLastSet;
        public static float ShuttleRate => s_Transport == Transport.Shuttle ? s_ShuttleRate : 0f;

        public static void SetShuttleRate(float rate)
        {
            s_Transport = rate == 0f ? Transport.Hold : Transport.Shuttle;
            s_ShuttleRate = rate;
            s_HaveLastSet = false;
            Time.timeScale = 1f;
        }

        private static void UpdateTransport()
        {
            if (s_Transport == Transport.Off || !SketchMemoryScript.m_Instance.IsPlayingBack) { return; }
            double current = App.Instance.CurrentSketchTime;
            double from;
            if (!s_HaveLastSet)
            {
                from = current;
            }
            else
            {
                double expected = s_LastSetSketchTime + Time.deltaTime;
                from = System.Math.Abs(current - expected) < 1e-4
                    ? s_LastSetSketchTime
                    : current - Time.deltaTime; // seeked since last frame: adopt the seek
            }
            double next = s_Transport == Transport.Shuttle
                ? from + s_ShuttleRate * Time.unscaledDeltaTime
                : from;
            next = System.Math.Max(0.0, next);
            App.Instance.CurrentSketchTime = next;
            s_LastSetSketchTime = next;
            s_HaveLastSet = true;
        }

        private void Update()
        {
            UpdateTransport();
            // After the transport, before stroke playback: an export seeks the sketch here.
            PostProductionExport.Stage();

            double rawSketchTimeMs = App.Instance.CurrentSketchTime * 1000.0;
            s_LastSketchTimeMs = rawSketchTimeMs >= SketchTimeSentinelThresholdMs ? 0.0 : rawSketchTimeMs;
            // Keeps the alignment readout current after loads, resets or direct sets.
            if (Time.frameCount % 15 == 0)
            {
                PostProductionAlignment.RefreshReadouts();
                RefreshLayerVisibility();
            }
            if (!Enabled) { return; }
            Camera cam = FindTargetCamera();
            if (cam == null)
            {
                LastStatus = $"Update: target camera not found (name=\"{s_TargetCameraName}\")";
                return;
            }

            // Reapplied every frame rather than once: setting fieldOfView (which
            // other, unrelated camera code may do) can invalidate an
            // explicitly-assigned projection matrix, so this guards against drift
            // instead of trusting a one-off assignment to stick. Same reasoning
            // applies to the pose - reapplied every frame rather than trusting a
            // one-off assignment, in case anything else (XR Origin tracking sync,
            // etc.) drives this camera's transform independently.
            ScreenshotManager.ApplyCalibratedProjectionToCamera(cam);
            ApplyTargetPose(cam);
            // The preview's plate would chase every export frame and compete with the export
            // for the disk; it resumes when the export ends.
            if (!PostProductionExport.IsRunning) { UpdateAutoPlate(); }
            ReportStatus(cam, "Update");
        }

        // During an export the Game window shows the frames being written, letterboxed, with
        // progress on top, instead of a preview frozen at its last frame.
        private GUIStyle m_ExportOverlayStyle;

        private void OnGUI()
        {
            Texture frame = PostProductionExport.DisplayTexture;
            if (!PostProductionExport.IsRunning) { return; }
            if (Event.current.type != EventType.Repaint) { return; }
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            GUI.DrawTexture(screen, Texture2D.blackTexture);
            if (frame != null) { GUI.DrawTexture(screen, frame, ScaleMode.ScaleToFit, false); }
            if (m_ExportOverlayStyle == null)
            {
                m_ExportOverlayStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = Mathf.Max(14, Screen.height / 40),
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(12, 12, 8, 8),
                };
                m_ExportOverlayStyle.normal.textColor = Color.white;
            }
            var content = new GUIContent(PostProductionExport.OverlayText());
            Vector2 size = m_ExportOverlayStyle.CalcSize(content);
            GUI.Box(new Rect(16, 16, size.x, size.y), content, m_ExportOverlayStyle);
        }

        // "1,1,0,1": each layer's visibility, for query.postprod.layers.visible (the HTTP
        // thread can't read canvases itself).
        public static string LayerVisibility { get; private set; } = "";
        // Layer names in the same order, one per line, for query.postprod.layers.names.
        public static string LayerNames { get; private set; } = "";

        private static void RefreshLayerVisibility()
        {
            if (App.Scene == null) { return; }
            var flags = new System.Text.StringBuilder();
            var names = new System.Text.StringBuilder();
            foreach (var canvas in App.Scene.LayerCanvases)
            {
                if (flags.Length > 0) { flags.Append(','); names.Append('\n'); }
                flags.Append(canvas.gameObject.activeSelf ? '1' : '0');
                names.Append(canvas.name);
            }
            LayerVisibility = flags.ToString();
            LayerNames = names.ToString();
        }

        // Live CG renders after every Update (stroke playback included) has run this
        // frame, so it reflects the current sketch time. During an export only the export
        // renders (the display keeps its last preview frame).
        private void LateUpdate()
        {
            PostProductionExport.Render();
            if (PostProductionExport.IsRunning) { return; }
            if (!Enabled || !PostProductionLiveCg.Enabled) { return; }
            Camera cam = FindTargetCamera();
            if (cam == null) { return; }
            PostProductionLiveCg.Render(cam, s_HasTargetPose, s_TargetPosition, s_TargetRotation);
        }

        private void ApplyTargetPose(Camera cam)
        {
            if (!s_HasTargetPose) { return; }
            App.Scene.AsScene[cam.transform] = TrTransform.TR(s_TargetPosition, s_TargetRotation);
        }

        // "Live" CG capture for the preview (see the plan's "Known issues to
        // revisit" for why this is a snapshot-on-demand rather than a true
        // per-frame GPU capture): reuses the existing, already-correct
        // ScreenshotManager.TakeSnapshot (same mechanism app.snapshot.quat already
        // uses - background-sentinel detection and culling mask handling are both
        // already solved there, not reimplemented here) against our stored
        // calibrated pose, to a fixed filename so callers always know where to
        // find the latest capture rather than tracking a changing name.
        // Incremented after every finished capture, so a client can wait for the
        // previous one to complete before queueing the next (the command queue is
        // FIFO, so a flood of captures would otherwise delay every later command -
        // including Pause - until the whole backlog has run).
        public static volatile int CgCaptureCount;

        private const string CgCaptureBaseName = "postprod_live_cg";
        // Explicit extension is load-bearing, not decorative: a filename with no
        // extension silently saves as .jpg instead (a documented gotcha in this
        // codebase's own API - re-hit twice already this session by omitting it).
        private const string CgCaptureFilename = CgCaptureBaseName + ".png";

        public static string CgCaptureColorPath =>
            System.IO.Path.Combine(App.SnapshotPath(), CgCaptureFilename);
        public static string CgCaptureDepthPath =>
            System.IO.Path.Combine(App.SnapshotPath(), CgCaptureBaseName + "_depth.exr");

        // Separate from LastStatus deliberately - LastStatus is overwritten every
        // frame by Update()'s continuous camera report, which would clobber a
        // one-off capture result before it could ever be read back (confirmed
        // happening the first time this was tried).
        public static string LastCgCaptureStatus { get; private set; } = "not captured yet";

        public static void RefreshCgCapture(int width, int height)
        {
            // The live CG supersedes the snapshot; a ~1s snapshot here would only stall
            // the fast preview (the control page's auto-refresh and Play loop still ask
            // for them). Turn live CG off to take one, e.g. for a parity check.
            if (PostProductionLiveCg.Enabled)
            {
                LastCgCaptureStatus = "skipped: live CG is on (postprod.preview.livecg=0 to take a snapshot)";
                return;
            }
            if (!s_HasTargetPose)
            {
                LastCgCaptureStatus = "no target pose set - call postprod.preview.pose first";
                return;
            }
            TrTransform tr = TrTransform.TR(s_TargetPosition, s_TargetRotation);
            ScreenshotManager.TakeSnapshot(
                tr, CgCaptureFilename, width, height,
                superSampling: 1f, removeBackground: false, renderDepth: true, renderNormals: false);
            PostProductionCompositeImageEffect.InvalidateCgCache();
            CgCaptureCount++;
            LastCgCaptureStatus = $"wrote {CgCaptureColorPath} + {CgCaptureDepthPath} at {width}x{height}";
        }

        public static void SetPlate(string colorPath, string depthPath)
        {
            PostProductionCompositeImageEffect.SetPlatePaths(colorPath, depthPath);
        }

        public static void SetNaiveMode(bool naive)
        {
            PostProductionCompositeImageEffect.NaiveMode = naive;
        }

        public static void SetCgOnlyMode(bool cgOnly)
        {
            PostProductionCompositeImageEffect.CgOnlyMode = cgOnly;
        }

        private void ReportStatus(Camera cam, string source)
        {
            LastStatus = $"[{source}] enabled={Enabled} cam={cam.name} " +
                $"pixelSize={cam.pixelWidth}x{cam.pixelHeight} fov={cam.fieldOfView:F3} " +
                $"usePhysical={cam.usePhysicalProperties} " +
                $"projMatrix={MatrixSummary(cam.projectionMatrix)} " +
                $"targetTexture={(cam.targetTexture != null ? cam.targetTexture.name : "null")} " +
                $"enabled={cam.enabled} gameObjectActive={cam.gameObject.activeInHierarchy} " +
                $"stereoTargetEye={cam.stereoTargetEye} " +
                $"worldPos={cam.transform.position} worldRot={cam.transform.rotation} " +
                $"hasTargetPose={s_HasTargetPose}";
        }

        private static string MatrixSummary(Matrix4x4 m)
        {
            return $"[{m[0, 0]:F4},{m[0, 2]:F4}|{m[1, 1]:F4},{m[1, 2]:F4}]";
        }

        // TEMP DEBUG: caches a listing of every enabled Camera in the scene, to
        // check whether App.VrSdk.GetVrCamera() is really the only candidate for
        // what's shown - e.g. a separate left/right eye camera, or another camera
        // with a higher depth rendering on top and obscuring it. Must run on the
        // main thread (Camera.allCameras is a Unity API) - called from the
        // postprod.debug.dumpcameras command, not directly from a query.
        public static string AllCamerasReport { get; private set; } = "not captured yet";

        public static void CaptureAllCamerasReport()
        {
            var cams = Camera.allCameras;
            var sb = new System.Text.StringBuilder();
            sb.Append($"count={cams.Length}\n");
            Camera ours = App.VrSdk.GetVrCamera();
            foreach (var c in cams)
            {
                sb.Append(
                    $"name={c.name} isOurs={(c == ours)} depth={c.depth} " +
                    $"targetDisplay={c.targetDisplay} enabled={c.enabled} " +
                    $"fov={c.fieldOfView:F2} pixelSize={c.pixelWidth}x{c.pixelHeight} " +
                    $"clearFlags={c.clearFlags} cullingMask={c.cullingMask} " +
                    $"near={c.nearClipPlane:F3} far={c.farClipPlane:F1} " +
                    $"pos={c.transform.position.ToString("F2")} rot={c.transform.rotation.eulerAngles.ToString("F1")} " +
                    $"targetTexture={(c.targetTexture != null ? c.targetTexture.name : "null")}\n");
            }
            AllCamerasReport = sb.ToString();
        }
    }
}
