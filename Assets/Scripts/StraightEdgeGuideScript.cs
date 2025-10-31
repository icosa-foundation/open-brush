// Copyright 2020 The Tilt Brush Authors
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

    public class StraightEdgeGuideScript : MonoBehaviour
    {
        [SerializeField] private float m_MinDisplayLength;
        [SerializeField] private float m_SnapDisabledDelay = 0.1f;
        [SerializeField] private Texture2D[] m_ShapeTextures;
        [SerializeField] private float m_MeterYOffset = 0.75f;
        [SerializeField] private float m_EndpointSnapDistance = 0.05f;
        [SerializeField] private int m_EndpointHistoryLength = 8;

        public enum Shape
        {
            None = -1,
            Line,
            Circle,
            Sphere,
        }

        private TMPro.TextMeshPro m_MeterDisplay;
        private bool m_ShowMeter;

        private Vector3 m_vOrigin_CS;
        private Vector3 m_TargetPos_CS;
        private float m_SnapEnabledTimeStamp;
        private bool m_SnapActive;
        private Shape m_CurrentShape;
        private Shape m_TempShape;
        // Stack of line endpoints for undo support (most recent at end)
        private readonly List<(Vector3 origin, Vector3 target)> m_LineHistory = new List<(Vector3, Vector3)>();
        // Track all straight edge strokes to support undo/redo properly
        private readonly Dictionary<Stroke, (Vector3 origin, Vector3 target)> m_StrokeToLine = new Dictionary<Stroke, (Vector3, Vector3)>();

        public Shape CurrentShape { get { return m_CurrentShape; } }
        public Shape TempShape { get { return m_TempShape; } }

        // Returns origin pos in Canvas space
        public Vector3 GetOriginPos() { return m_vOrigin_CS; }

        // Returns target pos in Canvas space
        public Vector3 GetTargetPos() { return m_TargetPos_CS; }

        public bool IsShowingMeter() { return m_ShowMeter; }
        public void FlipMeter() { m_ShowMeter = !m_ShowMeter; }

        public void ForceSnapDisabled()
        {
            m_SnapEnabledTimeStamp = 0.0f;
        }

        public bool SnapEnabled
        {
            get
            {
                // TODO: This is no good. Value should be stable during the frame,
                // unless explicitly changed.
                return Time.realtimeSinceStartup - m_SnapEnabledTimeStamp < m_SnapDisabledDelay;
            }
            set
            {
                if (value)
                {
                    m_SnapEnabledTimeStamp = Time.realtimeSinceStartup;
                }
            }
        }

        void Awake()
        {
            m_MeterDisplay = GetComponentInChildren<TMPro.TextMeshPro>();
            HideGuide();
            ClearEndpointHistory();
        }

        void Start()
        {
            // Subscribe to command events for endpoint tracking
            // Using Start() instead of Awake() ensures SketchMemoryScript.m_Instance exists
            if (SketchMemoryScript.m_Instance != null)
            {
                SketchMemoryScript.m_Instance.CommandPerformed += OnCommandPerformed;
                SketchMemoryScript.m_Instance.CommandUndo += OnCommandUndo;
                SketchMemoryScript.m_Instance.CommandRedo += OnCommandRedo;
            }
        }

        void OnDestroy()
        {
            if (SketchMemoryScript.m_Instance != null)
            {
                SketchMemoryScript.m_Instance.CommandPerformed -= OnCommandPerformed;
                SketchMemoryScript.m_Instance.CommandUndo -= OnCommandUndo;
                SketchMemoryScript.m_Instance.CommandRedo -= OnCommandRedo;
            }
        }

        private void OnCommandPerformed(BaseCommand command)
        {
            // Record endpoints when a straight edge line is created
            if (command is BrushStrokeCommand brushCommand)
            {
                bool isStraightEdge = PointerManager.m_Instance.StraightEdgeModeEnabled;
                bool isLine = m_CurrentShape == Shape.Line;

                if (isStraightEdge && isLine)
                {
                    Stroke stroke = brushCommand.m_Stroke;
                    if (stroke != null && stroke.m_ControlPoints != null && stroke.m_ControlPoints.Length >= 2)
                    {
                        // Endpoints are in canvas space
                        Vector3 origin_CS = stroke.m_ControlPoints[0].m_Pos;
                        Vector3 target_CS = stroke.m_ControlPoints[stroke.m_ControlPoints.Length - 1].m_Pos;

                        // Store mapping from stroke to its line endpoints for undo tracking
                        m_StrokeToLine[stroke] = (origin_CS, target_CS);

                        // Add directly to history (don't rebuild - stroke isn't in AllStrokes yet)
                        m_LineHistory.Add((origin_CS, target_CS));

                        // Keep only last N lines (m_EndpointHistoryLength)
                        while (m_LineHistory.Count > m_EndpointHistoryLength)
                        {
                            m_LineHistory.RemoveAt(0);
                        }
                    }
                }
            }
        }

        private void OnCommandUndo(BaseCommand command)
        {
            // Rebuild history from current sketch state after undo
            if (command is BrushStrokeCommand brushCommand)
            {
                // Remove the stroke mapping since it's being undone
                if (m_StrokeToLine.ContainsKey(brushCommand.m_Stroke))
                {
                    m_StrokeToLine.Remove(brushCommand.m_Stroke);
                }
                RebuildHistory();
            }
        }

        private void OnCommandRedo(BaseCommand command)
        {
            // Rebuild history from current sketch state after redo
            if (command is BrushStrokeCommand brushCommand)
            {
                // Re-add the stroke mapping since it's being redone
                bool isStraightEdge = PointerManager.m_Instance.StraightEdgeModeEnabled;
                bool isLine = m_CurrentShape == Shape.Line;

                Stroke stroke = brushCommand.m_Stroke;
                if (stroke != null && stroke.m_ControlPoints != null && stroke.m_ControlPoints.Length >= 2)
                {
                    // Check if this was a straight edge stroke by seeing if we can extract endpoints
                    Vector3 origin_CS = stroke.m_ControlPoints[0].m_Pos;
                    Vector3 target_CS = stroke.m_ControlPoints[stroke.m_ControlPoints.Length - 1].m_Pos;

                    // Re-add to mapping
                    m_StrokeToLine[stroke] = (origin_CS, target_CS);
                }
                RebuildHistory();
            }
        }

        private void RebuildHistory()
        {
            m_LineHistory.Clear();

            // Get all strokes that are currently in the sketch (not undone)
            var currentStrokes = SketchMemoryScript.AllStrokes();
            var currentStrokesSet = new HashSet<Stroke>(currentStrokes);

            // Find straight edge strokes and add their endpoints to history
            foreach (var kvp in m_StrokeToLine)
            {
                if (currentStrokesSet.Contains(kvp.Key))
                {
                    m_LineHistory.Add(kvp.Value);
                    Debug.Log($"[SNAP_UNDO] RebuildHistory: Added stroke to history");
                }
            }

            // Keep only last N lines (m_EndpointHistoryLength)
            while (m_LineHistory.Count > m_EndpointHistoryLength)
            {
                m_LineHistory.RemoveAt(0);
            }
        }

        public void ShowGuide(Vector3 vOrigin)
        {
            // Snap is not active when we first start
            m_SnapActive = false;

            // Place widgets at the origin
            Vector3 origin = vOrigin;
            if (m_CurrentShape == Shape.Line &&
                TryGetEndpointSnap(vOrigin, out Vector3 snappedOrigin))
            {
                origin = snappedOrigin;
            }
            m_vOrigin_CS = Coords.CanvasPose.inverse * origin;
        }

        public void HideGuide()
        {
            m_MeterDisplay.text = "";
            m_MeterDisplay.gameObject.SetActive(false);
        }

        public void SetTempShape(Shape s)
        {
            if (m_TempShape == s)
            {
                m_TempShape = Shape.None;
            }
            else
            {
                m_TempShape = s;
            }
        }

        public void ResolveTempShape()
        {
            if (m_TempShape != Shape.None)
            {
                m_CurrentShape = m_TempShape;
                PointerManager.m_Instance.StraightEdgeModeEnabled = true;
                m_TempShape = Shape.None;
            }
        }

        public Texture2D GetCurrentButtonTexture()
        {
            return m_ShapeTextures[(int)m_CurrentShape];
        }

        /// Snap v to the surface of 0, 45, and 90-degree cones along the Y axis.
        /// v will be the closest point on the surface of the cones.
        public static Vector3 ApplySnap(Vector3 v)
        {
            if (v.magnitude < 1e-4f)
            {
                return v;
            }
            // angle is in [0, 180]
            float angle = Vector3.Angle(new Vector3(0, 1, 0), v);
            // put it into [0, 90]
            if (angle > 90)
            {
                angle = 180 - angle;
            }
            if (angle < 45f / 2)
            {
                // Snap to 0 degree cone.
                return new Vector3(0, v.y, 0);
            }
            else if (angle > 90f - 45f / 2)
            {
                // Snap to 90 degree cone.
                return new Vector3(v.x, 0, v.z);
            }
            else
            {
                // Snap to 45 degree cone.
                Vector3 line = new Vector3(v.x, 0, v.z).normalized;
                line.y = Mathf.Sign(v.y);
                line = line.normalized; // or could multiply by sqrt(2)/2
                return Vector3.Dot(v, line) * line;
            }
        }

        // Displays the length of the straight edge aligned with the vector
        // Origin and target are in room space
        private void UpdateMeter(Vector3 vOrigin, Vector3 vTarget)
        {
            Vector3 vOriginToTarget = vTarget - vOrigin;
            float distToTarget = vOriginToTarget.magnitude;
            // Find midpoint and set line position
            if (distToTarget > 0.01f)
            {
                // Orient line
                vOriginToTarget.Normalize();
            }

            float fMetersToTarget = distToTarget * App.UNITS_TO_METERS;
            float scale = App.Scene.AsScene[m_MeterDisplay.transform].scale;
            float scaledDistance = scale * fMetersToTarget * 100;
            scaledDistance = Mathf.Floor(scaledDistance) / 100;
            if (fMetersToTarget > m_MinDisplayLength)
            {
                m_MeterDisplay.text = scaledDistance.ToString("F2") + "m";
                m_MeterDisplay.transform.position = vTarget;

                var head = ViewpointScript.Head;
                Vector3 vCameraRight = head.right;
                Vector3 pCameraPosition = head.position;
                Vector3 vNormal = vOriginToTarget.normalized;
                Vector3 vCameraToTarget = (vTarget - pCameraPosition).normalized;
                Vector3 vPlaneProj = vCameraToTarget - Vector3.Dot(vCameraToTarget, vNormal) * vNormal;
                vPlaneProj.Normalize();
                Vector3 vUp = Vector3.Cross(vPlaneProj, vNormal);

                // Reverse writing if line pulled right to left
                if (Vector3.Dot(vCameraRight, vOriginToTarget) < 0)
                {
                    vUp *= -1;
                }

                var brushSize = PointerManager.m_Instance.MainPointer.BrushSizeAbsolute;
                m_MeterDisplay.transform.rotation = Quaternion.LookRotation(vPlaneProj, vUp);
                m_MeterDisplay.transform.position += vUp *
                    (m_MeterDisplay.rectTransform.rect.height * m_MeterYOffset + brushSize * 0.5f);
                m_MeterDisplay.gameObject.SetActive(true);
            }
            else
            {
                m_MeterDisplay.text = "";
                m_MeterDisplay.gameObject.SetActive(false);
            }
        }

        // Pass pointer position in room space
        public void UpdateTarget(Vector3 vPointer)
        {
            // Everything is done in room coordinates, so the _RS suffixes are omitted
            TrTransform xfWorldFromCanvas = Coords.CanvasPose;
            Vector3 vTarget = vPointer;
            Vector3 vOrigin = xfWorldFromCanvas * m_vOrigin_CS;

            // Optionally snap target pos.
            // TODO: Make this work with non-line shapes.
            m_SnapActive = SnapEnabled;
            if (m_SnapActive && m_CurrentShape == Shape.Line)
            {
                vTarget = vOrigin + ApplySnap(vTarget - vOrigin);
            }

            if (m_CurrentShape == Shape.Line &&
                TryGetEndpointSnap(vTarget, out Vector3 snappedTarget))
            {
                // Avoid snapping to the origin which can create degenerate strokes.
                if ((snappedTarget - vOrigin).sqrMagnitude > 1e-6f)
                {
                    vTarget = snappedTarget;
                }
            }

            if (m_ShowMeter)
            {
                UpdateMeter(vOrigin, vTarget);
            }

            m_TargetPos_CS = xfWorldFromCanvas.inverse * vTarget;
        }


        public bool TryGetEndpointSnap(Vector3 position_WS, out Vector3 snapped_WS)
        {
            // Snap distance must be in world space to be consistent regardless of canvas scale/rotation
            float maxDistanceSqr = m_EndpointSnapDistance * m_EndpointSnapDistance;
            float closestDistanceSqr = maxDistanceSqr;
            bool found = false;
            Vector3 closest_WS = Vector3.zero;

            // Check all lines in history (each line has 2 endpoints)
            foreach (var line in m_LineHistory)
            {
                // Check origin
                Vector3 origin_WS = Coords.CanvasPose * line.origin;
                float distSqr = (origin_WS - position_WS).sqrMagnitude;
                if (distSqr <= closestDistanceSqr)
                {
                    closestDistanceSqr = distSqr;
                    closest_WS = origin_WS;
                    found = true;
                }

                // Check target
                Vector3 target_WS = Coords.CanvasPose * line.target;
                distSqr = (target_WS - position_WS).sqrMagnitude;
                if (distSqr <= closestDistanceSqr)
                {
                    closestDistanceSqr = distSqr;
                    closest_WS = target_WS;
                    found = true;
                }
            }

            snapped_WS = found ? closest_WS : position_WS;
            return found;
        }

        public void ClearEndpointHistory()
        {
            m_LineHistory.Clear();
            m_StrokeToLine.Clear();
        }
    }
} // namespace TiltBrush
