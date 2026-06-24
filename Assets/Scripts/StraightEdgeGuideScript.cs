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
        static public StraightEdgeGuideScript m_Instance;

        [SerializeField] private float m_MinDisplayLength;
        [SerializeField] private float m_SnapDisabledDelay = 0.1f;
        [SerializeField] private Texture2D[] m_ShapeTextures;
        [SerializeField] private float m_MeterYOffset = 0.75f;
        [SerializeField] private float m_EndpointSnapDistance = 0.2f;

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

        // Spatial hash for efficient snap point queries
        // One hash per canvas, stored in canvas-space coordinates
        private readonly Dictionary<CanvasScript, Dictionary<Vector3Int, List<(Stroke stroke, int pointIndex)>>> m_HashPerCanvas =
            new Dictionary<CanvasScript, Dictionary<Vector3Int, List<(Stroke, int)>>>();

        // Whether to snap only to active canvas or all canvases
        [SerializeField] private bool m_SnapToActiveCanvasOnly = true;

        // Track if we're currently snapped to an endpoint (for haptic feedback)
        private bool m_IsCurrentlySnapped = false;

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
            m_Instance = this;
            m_MeterDisplay = GetComponentInChildren<TMPro.TextMeshPro>();
            HideGuide();
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
            bool foundSnap = m_SnapToActiveCanvasOnly
                ? QuerySingleCanvas(App.Scene.ActiveCanvas, position_WS, out snapped_WS)
                : QueryAllCanvases(position_WS, out snapped_WS);

            // Trigger haptic pulse on snap transition (not-snapped → snapped)
            if (foundSnap && !m_IsCurrentlySnapped)
            {
                InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, 0.05f);
                m_IsCurrentlySnapped = true;
            }
            else if (!foundSnap && m_IsCurrentlySnapped)
            {
                m_IsCurrentlySnapped = false;
            }

            return foundSnap;
        }

        private bool QuerySingleCanvas(CanvasScript canvas, Vector3 position_WS, out Vector3 snapped_WS)
        {
            snapped_WS = position_WS;
            if (canvas == null || !m_HashPerCanvas.TryGetValue(canvas, out var hash))
            {
                return false; // No snap points on this canvas
            }

            // Transform query position to canvas space
            Vector3 queryPos_CS = canvas.Pose.inverse * position_WS;

            return QueryCanvasHash(canvas, hash, queryPos_CS, position_WS, out snapped_WS);
        }

        private bool QueryAllCanvases(Vector3 position_WS, out Vector3 snapped_WS)
        {
            snapped_WS = position_WS;
            float maxDistSqr = m_EndpointSnapDistance * m_EndpointSnapDistance;
            float closestDistSqr = maxDistSqr;
            bool found = false;
            Vector3 closest_WS = Vector3.zero;

            foreach (var (canvas, hash) in m_HashPerCanvas)
            {
                if (canvas == null) continue;

                Vector3 queryPos_CS = canvas.Pose.inverse * position_WS;
                if (QueryCanvasHash(canvas, hash, queryPos_CS, position_WS, out Vector3 result_WS))
                {
                    float distSqr = (result_WS - position_WS).sqrMagnitude;
                    if (distSqr < closestDistSqr)
                    {
                        closestDistSqr = distSqr;
                        closest_WS = result_WS;
                        found = true;
                    }
                }
            }

            if (found) snapped_WS = closest_WS;
            return found;
        }

        private bool QueryCanvasHash(CanvasScript canvas, Dictionary<Vector3Int, List<(Stroke, int)>> hash,
                                      Vector3 queryPos_CS, Vector3 position_WS, out Vector3 snapped_WS)
        {
            snapped_WS = position_WS;
            float maxDistSqr = m_EndpointSnapDistance * m_EndpointSnapDistance;
            float closestDistSqr = maxDistSqr;
            bool found = false;
            Vector3 closest_WS = Vector3.zero;

            // Query 3x3x3 neighborhood in canvas space
            float cellSize = m_EndpointSnapDistance;
            Vector3Int centerCell = SpatialHashPosition(queryPos_CS, cellSize);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        Vector3Int cell = centerCell + new Vector3Int(dx, dy, dz);
                        if (hash.TryGetValue(cell, out var entries))
                        {
                            foreach (var (stroke, pointIndex) in entries)
                            {
                                if (stroke?.m_ControlPoints == null) continue;

                                // Get snap point from control points (already in canvas space)
                                int cpIndex = pointIndex == 0 ? 0 : stroke.m_ControlPoints.Length - 1;
                                Vector3 snapPoint_CS = stroke.m_ControlPoints[cpIndex].m_Pos;

                                // Transform to world space for distance check
                                Vector3 snapPoint_WS = canvas.Pose * snapPoint_CS;
                                float distSqr = (snapPoint_WS - position_WS).sqrMagnitude;

                                if (distSqr < closestDistSqr)
                                {
                                    closestDistSqr = distSqr;
                                    closest_WS = snapPoint_WS;
                                    found = true;
                                }
                            }
                        }
                    }
                }
            }

            if (found) snapped_WS = closest_WS;
            return found;
        }


        private Vector3Int SpatialHashPosition(Vector3 position, float cellSize)
        {
            // Quantize to coarser grid for spatial queries
            float invCellSize = 1f / cellSize;
            return new Vector3Int(
                Mathf.FloorToInt(position.x * invCellSize),
                Mathf.FloorToInt(position.y * invCellSize),
                Mathf.FloorToInt(position.z * invCellSize)
            );
        }

        /// <summary>
        /// Add a stroke's endpoints to the spatial hash.
        /// Called when a straight edge stroke is created.
        /// </summary>
        public void AddStrokeToHash(Stroke stroke)
        {
            if (stroke?.Canvas == null || stroke.m_ControlPoints == null || stroke.m_ControlPoints.Length < 2)
            {
                return;
            }

            var canvas = stroke.Canvas;
            if (!m_HashPerCanvas.TryGetValue(canvas, out var hash))
            {
                hash = new Dictionary<Vector3Int, List<(Stroke, int)>>();
                m_HashPerCanvas[canvas] = hash;
            }

            // Get endpoints in canvas space (already there!)
            Vector3 origin_CS = stroke.m_ControlPoints[0].m_Pos;
            Vector3 target_CS = stroke.m_ControlPoints[stroke.m_ControlPoints.Length - 1].m_Pos;
            float cellSize = m_EndpointSnapDistance;

            // Add origin (pointIndex 0)
            Vector3Int originCell = SpatialHashPosition(origin_CS, cellSize);
            if (!hash.TryGetValue(originCell, out var originList))
            {
                originList = new List<(Stroke, int)>();
                hash[originCell] = originList;
            }
            originList.Add((stroke, 0));

            // Add target (pointIndex 1)
            Vector3Int targetCell = SpatialHashPosition(target_CS, cellSize);
            if (!hash.TryGetValue(targetCell, out var targetList))
            {
                targetList = new List<(Stroke, int)>();
                hash[targetCell] = targetList;
            }
            targetList.Add((stroke, 1));
        }

        /// <summary>
        /// Remove a stroke's endpoints from the spatial hash.
        /// Called when a straight edge stroke is deleted.
        /// </summary>
        public void RemoveStrokeFromHash(Stroke stroke)
        {
            if (stroke?.Canvas == null || !m_HashPerCanvas.TryGetValue(stroke.Canvas, out var hash))
            {
                return;
            }

            if (stroke.m_ControlPoints == null || stroke.m_ControlPoints.Length < 2)
            {
                return;
            }

            // Get endpoints in canvas space
            Vector3 origin_CS = stroke.m_ControlPoints[0].m_Pos;
            Vector3 target_CS = stroke.m_ControlPoints[stroke.m_ControlPoints.Length - 1].m_Pos;
            float cellSize = m_EndpointSnapDistance;

            // Remove origin (pointIndex 0)
            Vector3Int originCell = SpatialHashPosition(origin_CS, cellSize);
            if (hash.TryGetValue(originCell, out var originList))
            {
                originList.Remove((stroke, 0));
                if (originList.Count == 0)
                {
                    hash.Remove(originCell);
                }
            }

            // Remove target (pointIndex 1)
            Vector3Int targetCell = SpatialHashPosition(target_CS, cellSize);
            if (hash.TryGetValue(targetCell, out var targetList))
            {
                targetList.Remove((stroke, 1));
                if (targetList.Count == 0)
                {
                    hash.Remove(targetCell);
                }
            }

            // Clean up empty canvas hash
            if (hash.Count == 0)
            {
                m_HashPerCanvas.Remove(stroke.Canvas);
            }
        }

        /// <summary>
        /// Update a stroke's position in the spatial hash after transformation.
        /// Called when a straight edge stroke is moved/rotated/scaled.
        /// </summary>
        public void UpdateStrokeInHash(Stroke stroke)
        {
            // Simply remove and re-add (efficient for single strokes)
            RemoveStrokeFromHash(stroke);
            AddStrokeToHash(stroke);
        }

        /// <summary>
        /// Rebuild all canvas hashes from scratch.
        /// Called when loading a sketch or for rare full-rebuild scenarios.
        /// </summary>
        public void RebuildAllCanvasHashes()
        {
            m_HashPerCanvas.Clear();

            if (SketchMemoryScript.m_Instance == null)
            {
                return;
            }

            // Iterate through all strokes in memory
            var currentNode = SketchMemoryScript.m_Instance.GetMemoryList.First;
            while (currentNode != null)
            {
                Stroke stroke = currentNode.Value;
                if (stroke != null &&
                    (stroke.m_Flags & SketchMemoryScript.StrokeFlags.CreatedWithStraightEdge) != 0 &&
                    stroke.m_ControlPoints != null && stroke.m_ControlPoints.Length >= 2)
                {
                    AddStrokeToHash(stroke);
                }
                currentNode = currentNode.Next;
            }
        }

    }
} // namespace TiltBrush
