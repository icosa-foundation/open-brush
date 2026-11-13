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

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    // "Fix it in post" spatial alignment nudges, relative to the calibrated camera's view.
    // The camera pose (ChArUco), floor and scale are trusted, so the only controls are:
    // move along the floor (forward/back along the camera's flattened view direction,
    // left/right along its horizontal right axis) and turn about the room's vertical axis
    // through a pivot on the floor under the centre of the target's strokes.
    //
    // Target -1 is the global offset (whole sketch; pivot under all strokes); 0..N-1 is a
    // layer's own offset (pivot under that layer's strokes). Offsets stay full transforms
    // (that is what is saved). Nudges are defined in room space and conjugated into the
    // offset's own space, so they behave the same whatever the scene/layer poses are:
    //   global: world = Scene * Global * p                      (p in scene space)
    //   layer:  world = Scene * Global * LayerLocal * Layer * p  (p in canvas space)
    public static class PostProductionAlignment
    {
        public const int Global = -1;

        private static readonly Dictionary<int, string> s_Readouts = new Dictionary<int, string>();
        private static readonly object s_ReadoutLock = new object();

        private static int s_CentreStrokeCount = -1;
        private static Vector3 s_AllCentreScene;
        private static readonly Dictionary<CanvasScript, Vector3> s_LayerCentreCanvas =
            new Dictionary<CanvasScript, Vector3>();

        // Editor Play sessions keep static state (no domain reload).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            lock (s_ReadoutLock) { s_Readouts.Clear(); }
            s_CentreStrokeCount = -1;
            s_AllCentreScene = Vector3.zero;
            s_LayerCentreCanvas.Clear();
        }

        public static void Nudge(int target, float forwardMetres, float rightMetres, float yawDegrees)
        {
            if (!TryGetFrame(target, out TrTransform frame, out Vector3 centre)) { return; }
            TrTransform offset = GetOffset(target);
            GetCameraAxes(out Vector3 forward, out Vector3 right);

            Vector3 pivot = (frame * offset).MultiplyPoint(centre);
            pivot.y = 0f;
            Vector3 move = (forward * forwardMetres + right * rightMetres) * App.METERS_TO_UNITS;
            TrTransform delta = TrTransform.T(move) * TrTransform.T(pivot) *
                TrTransform.R(yawDegrees, Vector3.up) * TrTransform.T(-pivot);

            SetOffset(target, frame.inverse * delta * frame * offset);
            RefreshReadouts();
        }

        public static void Revert(int target)
        {
            SetOffset(target, target == Global
                ? PostProductionSettings.SavedGlobalSpatialOffset
                : PostProductionSettings.GetSavedLayerSpatialOffset(target));
            RefreshReadouts();
        }

        public static void Reset(int target)
        {
            SetOffset(target, TrTransform.identity);
            RefreshReadouts();
        }

        // Plain strings for the HTTP listener thread (computing them touches Unity
        // objects, so it happens on the main thread: after each change and periodically).
        public static string GetReadout(int target)
        {
            lock (s_ReadoutLock)
            {
                return s_Readouts.TryGetValue(target, out var s) ? s : "no offset";
            }
        }

        public static void RefreshReadouts()
        {
            var fresh = new Dictionary<int, string> { [Global] = Readout(Global) };
            int count = App.Scene != null ? App.Scene.LayerCanvases.Count() : 0;
            for (int i = 0; i < count; i++) { fresh[i] = Readout(i); }
            lock (s_ReadoutLock)
            {
                s_Readouts.Clear();
                foreach (var kv in fresh) { s_Readouts[kv.Key] = kv.Value; }
            }
        }

        private static string Readout(int target)
        {
            TrTransform offset = GetOffset(target);
            if (offset == TrTransform.identity) { return "no offset"; }
            if (!TryGetFrame(target, out TrTransform frame, out Vector3 centre)) { return "unknown layer"; }
            GetCameraAxes(out Vector3 forward, out Vector3 right);

            Vector3 moved = (frame * offset).MultiplyPoint(centre) - frame.MultiplyPoint(centre);
            moved.y = 0f;
            Vector3 turned = (frame * offset * frame.inverse).rotation * Vector3.forward;
            float yaw = Mathf.Atan2(turned.x, turned.z) * Mathf.Rad2Deg;
            // "back" = away from the camera, matching the page's buttons (the nudge itself is
            // along the camera's view direction, so the value is unchanged).
            return $"back {Vector3.Dot(moved, forward) * App.UNITS_TO_METERS:F3}m, " +
                $"right {Vector3.Dot(moved, right) * App.UNITS_TO_METERS:F3}m, yaw {yaw:F2} deg";
        }

        private static TrTransform GetOffset(int target) => target == Global
            ? PostProductionSettings.GlobalSpatialOffset
            : PostProductionSettings.GetLayerSpatialOffset(target);

        private static void SetOffset(int target, TrTransform value)
        {
            if (target == Global) { PostProductionSettings.GlobalSpatialOffset = value; }
            else { PostProductionSettings.SetLayerSpatialOffset(target, value); }
        }

        // The transform the target's offset is applied after (world = frame * offset * p),
        // and the target's stroke centre in the offset's own space.
        private static bool TryGetFrame(int target, out TrTransform frame, out Vector3 centre)
        {
            RefreshCentresIfNeeded();
            TrTransform sceneAndGlobal = App.Scene.Pose * PostProductionSettings.GlobalSpatialOffset;
            if (target == Global)
            {
                frame = App.Scene.Pose;
                centre = s_AllCentreScene;
                return true;
            }
            CanvasScript canvas = GetLayerCanvas(target);
            if (canvas == null) { frame = TrTransform.identity; centre = Vector3.zero; return false; }
            frame = sceneAndGlobal * Coords.AsLocal[canvas.transform];
            s_LayerCentreCanvas.TryGetValue(canvas, out centre);
            return true;
        }

        private static CanvasScript GetLayerCanvas(int index)
        {
            if (App.Scene == null || index < 0) { return null; }
            int i = 0;
            foreach (var canvas in App.Scene.LayerCanvases)
            {
                if (i++ == index) { return canvas; }
            }
            return null;
        }

        // Camera view direction flattened onto the floor, and its horizontal right axis, in
        // room space. Uses the calibrated pose when set (it is stored in scene space).
        private static void GetCameraAxes(out Vector3 forward, out Vector3 right)
        {
            Quaternion camRotation;
            if (PostProductionPreviewController.HasTargetPose)
            {
                camRotation = (App.Scene.Pose * TrTransform.R(PostProductionPreviewController.TargetRotation)).rotation;
            }
            else
            {
                Camera cam = App.VrSdk.GetVrCamera();
                camRotation = cam != null ? cam.transform.rotation : Quaternion.identity;
            }
            forward = camRotation * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-8f) { forward = Vector3.forward; }
            forward.Normalize();
            right = Vector3.Cross(Vector3.up, forward);
        }

        // Centres of the strokes' control-point bounds: all strokes in scene space (global
        // pivot) and each layer's strokes in that layer's canvas space (layer pivots). Uses
        // the full memory list, so pivots don't move when scrubbing or hiding layers.
        // Recomputed when the stroke count changes.
        private static void RefreshCentresIfNeeded()
        {
            var strokes = SketchMemoryScript.m_Instance.GetMemoryList;
            if (strokes.Count == s_CentreStrokeCount) { return; }

            var all = new Bounds();
            bool anyAll = false;
            var perCanvas = new Dictionary<CanvasScript, Bounds>();
            foreach (var stroke in strokes)
            {
                if (stroke.m_ControlPoints == null) { continue; }
                CanvasScript canvas = stroke.m_IntendedCanvas != null ? stroke.m_IntendedCanvas : App.Scene.MainCanvas;
                TrTransform canvasPose = Coords.AsLocal[canvas.transform];
                bool haveLayer = perCanvas.TryGetValue(canvas, out Bounds layerBounds);
                foreach (var cp in stroke.m_ControlPoints)
                {
                    if (!haveLayer) { layerBounds = new Bounds(cp.m_Pos, Vector3.zero); haveLayer = true; }
                    else { layerBounds.Encapsulate(cp.m_Pos); }
                    Vector3 p = canvasPose.MultiplyPoint(cp.m_Pos);
                    if (!anyAll) { all = new Bounds(p, Vector3.zero); anyAll = true; }
                    else { all.Encapsulate(p); }
                }
                if (haveLayer) { perCanvas[canvas] = layerBounds; }
            }
            s_AllCentreScene = anyAll ? all.center : Vector3.zero;
            s_LayerCentreCanvas.Clear();
            foreach (var kv in perCanvas) { s_LayerCentreCanvas[kv.Key] = kv.Value.center; }
            s_CentreStrokeCount = strokes.Count;
        }
    }
}
