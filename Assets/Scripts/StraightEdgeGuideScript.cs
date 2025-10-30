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
        private readonly List<Vector3> m_PreviousLineEndpoints = new List<Vector3>();
        private readonly HashSet<Vector3Int> m_EndpointHashSet = new HashSet<Vector3Int>();
        private const float kEndpointQuantization = 0.0001f; // 0.1mm grid for duplicate detection

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
            }
        }

        void OnDestroy()
        {
            if (SketchMemoryScript.m_Instance != null)
            {
                SketchMemoryScript.m_Instance.CommandPerformed -= OnCommandPerformed;
                SketchMemoryScript.m_Instance.CommandUndo -= OnCommandUndo;
            }
        }

        private void OnCommandPerformed(BaseCommand command)
        {
            // Record endpoints when a straight edge line is created
            if (command is BrushStrokeCommand brushCommand &&
                PointerManager.m_Instance.StraightEdgeModeEnabled &&
                m_CurrentShape == Shape.Line)
            {
                Stroke stroke = brushCommand.m_Stroke;
                if (stroke != null && stroke.m_ControlPoints != null && stroke.m_ControlPoints.Length >= 2)
                {
                    // Endpoints are in canvas space
                    Vector3 origin_CS = stroke.m_ControlPoints[0].m_Pos;
                    Vector3 target_CS = stroke.m_ControlPoints[stroke.m_ControlPoints.Length - 1].m_Pos;

                    AddEndpoint(origin_CS);
                    AddEndpoint(target_CS);
                }
            }
        }

        private void OnCommandUndo(BaseCommand command)
        {
            // With history length = 1 (max 2 endpoints), just clear on any undo
            // This is simple and correct: undoing removes the most recent line's endpoints
            if (command is BrushStrokeCommand)
            {
                ClearEndpointHistory();
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

            // Simple linear search - efficient for small endpoint counts (2-16 points)
            for (int i = 0; i < m_PreviousLineEndpoints.Count; i++)
            {
                // Convert endpoint from canvas space to world space for distance check
                Vector3 endpoint_WS = Coords.CanvasPose * m_PreviousLineEndpoints[i];
                float distanceSqr = (endpoint_WS - position_WS).sqrMagnitude;

                if (distanceSqr <= closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
                    closest_WS = endpoint_WS;
                    found = true;
                }
            }

            snapped_WS = found ? closest_WS : position_WS;
            return found;
        }

        public void ClearEndpointHistory()
        {
            m_PreviousLineEndpoints.Clear();
            m_EndpointHashSet.Clear();
        }

        private Vector3Int QuantizeEndpoint(Vector3 endpoint)
        {
            // Quantize to fine grid for duplicate detection (0.1mm)
            float invQuant = 1f / kEndpointQuantization;
            return new Vector3Int(
                Mathf.RoundToInt(endpoint.x * invQuant),
                Mathf.RoundToInt(endpoint.y * invQuant),
                Mathf.RoundToInt(endpoint.z * invQuant)
            );
        }

        private void AddEndpoint(Vector3 endpoint)
        {
            // Check for duplicates using quantized hash set - O(1) lookup
            Vector3Int quantized = QuantizeEndpoint(endpoint);
            if (m_EndpointHashSet.Contains(quantized))
            {
                return;
            }

            // With history length = 1, max = 2 endpoints
            // Remove oldest if at capacity
            if (m_PreviousLineEndpoints.Count >= 2)
            {
                Vector3Int oldQuantized = QuantizeEndpoint(m_PreviousLineEndpoints[0]);
                m_PreviousLineEndpoints.RemoveAt(0);
                m_EndpointHashSet.Remove(oldQuantized);
            }

            // Add the new endpoint
            m_PreviousLineEndpoints.Add(endpoint);
            m_EndpointHashSet.Add(quantized);
        }
    }
} // namespace TiltBrush
