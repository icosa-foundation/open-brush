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
using OpenBrush.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace TiltBrush
{
    public struct ManualColocationCaptureFeedback
    {
        public bool CanConfirm;
        public string Message;
    }

    public class ManualColocationTool : BaseTool
    {
        private enum CaptureStage
        {
            Start,
            End,
            Confirm,
        }

        private const float kMarkerScaleMeters = 0.035f;
        private const float kLineWidthMeters = 0.008f;

        [SerializeField] private GameObject m_CaptureVisualPrefab;

        private CaptureStage m_Stage;
        private Vector3 m_Start_RS;
        private Vector3 m_End_RS;
        private Action<Vector3, Vector3> m_Completed;
        private Action m_Cancelled;
        private Func<Vector3, Vector3, ManualColocationCaptureFeedback>
            m_GetConfirmationFeedback;
        private GameObject m_VisualRoot;
        private GameObject m_StartMarker;
        private GameObject m_EndMarker;
        private LineRenderer m_Line;
        private LineRenderer m_Arrow;
        private TextMeshPro m_StartLabel;
        private TextMeshPro m_EndLabel;
        private Material m_LineMaterial;
        private Material m_StartMaterial;
        private Material m_EndMaterial;

        protected override void Awake()
        {
            base.Awake();
            m_ExitOnAbortCommand = false;
        }

        public void BeginCapture(
            Action<Vector3, Vector3> completed,
            Action cancelled,
            Func<Vector3, Vector3, ManualColocationCaptureFeedback>
                getConfirmationFeedback)
        {
            m_Completed = completed;
            m_Cancelled = cancelled;
            m_GetConfirmationFeedback = getConfirmationFeedback;
            m_Stage = CaptureStage.Start;
            m_RequestExit = false;
            ShowInstruction(Localize("MP_MANUAL_COLOCATION_PLACE_A"));
        }

        private static string Localize(string key)
        {
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                "Strings", key);
        }

        public override void EnableTool(bool enable)
        {
            base.EnableTool(enable);
            if (enable)
            {
                CreateVisuals();
            }
            else
            {
                DestroyVisuals();
                m_Completed = null;
                m_Cancelled = null;
                m_GetConfirmationFeedback = null;
            }
        }

        public override void UpdateTool()
        {
            base.UpdateTool();

            if (InputManager.m_Instance.GetCommandDown(
                InputManager.SketchCommands.Abort))
            {
                Action cancelled = m_Cancelled;
                cancelled?.Invoke();
                return;
            }

            if (InputManager.Brush == null ||
                !InputManager.Brush.IsTrackedObjectValid)
            {
                return;
            }

            Vector3 tipPosition_RS = GetControllerTipPosition();
            UpdateVisuals(tipPosition_RS);

            if (InputManager.Wand != null &&
                InputManager.Wand.GetCommandDown(InputManager.SketchCommands.Activate) &&
                m_Stage == CaptureStage.Confirm)
            {
                SwapEndpoints();
                return;
            }

            if (InputManager.Brush.GetCommandDown(InputManager.SketchCommands.Activate))
            {
                switch (m_Stage)
                {
                    case CaptureStage.Start:
                        m_Start_RS = tipPosition_RS;
                        m_Stage = CaptureStage.End;
                        InputManager.m_Instance.TriggerHaptics(
                            InputManager.ControllerName.Brush, 0.1f);
                        ShowInstruction(Localize(
                            "MP_MANUAL_COLOCATION_PLACE_B"));
                        break;
                    case CaptureStage.End:
                        ManualColocationValidationError error =
                            ManualColocationSolver.ValidateMeasurement(
                                m_Start_RS, tipPosition_RS);
                        if (error != ManualColocationValidationError.None)
                        {
                            ShowInstruction(ValidationMessage(error));
                            return;
                        }
                        m_End_RS = tipPosition_RS;
                        ManualColocationCaptureFeedback feedback =
                            GetConfirmationFeedback();
                        if (!feedback.CanConfirm)
                        {
                            ShowInstruction(feedback.Message);
                            return;
                        }
                        m_Stage = CaptureStage.Confirm;
                        InputManager.m_Instance.TriggerHaptics(
                            InputManager.ControllerName.Brush, 0.2f);
                        ShowInstruction(feedback.Message);
                        break;
                    case CaptureStage.Confirm:
                        Action<Vector3, Vector3> completed = m_Completed;
                        completed?.Invoke(m_Start_RS, m_End_RS);
                        break;
                }
            }

        }

        public override bool AllowWorldTransformation()
        {
            return false;
        }

        public override bool AllowsWidgetManipulation()
        {
            return false;
        }

        public override bool InputBlocked()
        {
            return true;
        }

        public override bool HidePanels()
        {
            return true;
        }

        public override bool BlockPinCushion()
        {
            return true;
        }

        public override bool AllowDefaultToolToggle()
        {
            return false;
        }

        public override bool ShouldShowPointer()
        {
            return false;
        }

        private Vector3 GetControllerTipPosition()
        {
            ControllerGeometry geometry = InputManager.Brush.Geometry;
            if (geometry != null && geometry.ToolAttachPoint != null)
            {
                return geometry.ToolAttachPoint.position;
            }
            return InputManager.m_Instance.GetBrushControllerAttachPoint().position;
        }

        private void SwapEndpoints()
        {
            Vector3 previousStart = m_Start_RS;
            m_Start_RS = m_End_RS;
            m_End_RS = previousStart;
            InputManager.m_Instance.TriggerHaptics(
                InputManager.ControllerName.Wand, 0.1f);
            ShowInstruction(GetConfirmationFeedback().Message);
        }

        private ManualColocationCaptureFeedback GetConfirmationFeedback()
        {
            if (m_GetConfirmationFeedback != null)
            {
                return m_GetConfirmationFeedback(m_Start_RS, m_End_RS);
            }
            return new ManualColocationCaptureFeedback
            {
                CanConfirm = true,
                Message = Localize(
                    "MP_MANUAL_COLOCATION_CONFIRM_CONTROLS"),
            };
        }

        private void CreateVisuals()
        {
            DestroyVisuals();

            m_VisualRoot = m_CaptureVisualPrefab != null
                ? Instantiate(m_CaptureVisualPrefab)
                : new GameObject("Manual Colocation Capture");
            m_VisualRoot.name = "Manual Colocation Capture";
            if (App.Instance != null)
            {
                m_VisualRoot.transform.SetParent(App.Instance.m_RoomTransform, false);
            }

            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            m_LineMaterial = new Material(shader) { color = Color.white };
            m_StartMaterial = new Material(shader) { color = new Color(0.1f, 0.8f, 1f) };
            m_EndMaterial = new Material(shader) { color = new Color(1f, 0.55f, 0.1f) };

            m_StartMarker = CreateMarker("Endpoint A", m_StartMaterial);
            m_EndMarker = CreateMarker("Endpoint B", m_EndMaterial);
            m_Line = CreateLine("Reference Line", 2);
            m_Arrow = CreateLine("Direction Arrow", 3);
            m_StartLabel = CreateLabel("A Label", "A", m_StartMaterial.color);
            m_EndLabel = CreateLabel("B Label", "B", m_EndMaterial.color);

            m_StartMarker.SetActive(false);
            m_EndMarker.SetActive(false);
            m_StartLabel.gameObject.SetActive(false);
            m_EndLabel.gameObject.SetActive(false);
            m_Line.enabled = false;
            m_Arrow.enabled = false;
        }

        private GameObject CreateMarker(string name, Material material)
        {
            Transform existing = m_VisualRoot.transform.Find(name);
            GameObject marker = existing != null
                ? existing.gameObject
                : new GameObject(name);
            marker.transform.SetParent(m_VisualRoot.transform, false);
            marker.transform.localScale =
                Vector3.one * kMarkerScaleMeters * App.METERS_TO_UNITS;
            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
            MeshFilter filter = marker.GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = marker.AddComponent<MeshFilter>();
            }
            if (filter.sharedMesh == null)
            {
                filter.sharedMesh =
                    Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            }
            MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = marker.AddComponent<MeshRenderer>();
            }
            renderer.sharedMaterial = material;
            return marker;
        }

        private LineRenderer CreateLine(string name, int positionCount)
        {
            Transform existing = m_VisualRoot.transform.Find(name);
            GameObject lineObject = existing != null
                ? existing.gameObject
                : new GameObject(name);
            lineObject.transform.SetParent(m_VisualRoot.transform, false);
            LineRenderer line = lineObject.GetComponent<LineRenderer>();
            if (line == null)
            {
                line = lineObject.AddComponent<LineRenderer>();
            }
            line.useWorldSpace = true;
            line.positionCount = positionCount;
            line.startWidth = kLineWidthMeters * App.METERS_TO_UNITS;
            line.endWidth = kLineWidthMeters * App.METERS_TO_UNITS;
            line.numCapVertices = 4;
            line.sharedMaterial = m_LineMaterial;
            return line;
        }

        private TextMeshPro CreateLabel(string name, string text, Color color)
        {
            Transform existing = m_VisualRoot.transform.Find(name);
            GameObject labelObject = existing != null
                ? existing.gameObject
                : new GameObject(name);
            labelObject.transform.SetParent(m_VisualRoot.transform, false);
            TextMeshPro label = labelObject.GetComponent<TextMeshPro>();
            if (label == null)
            {
                label = labelObject.AddComponent<TextMeshPro>();
            }
            label.text = text;
            label.fontSize = 0.12f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.enableAutoSizing = false;
            return label;
        }

        private void UpdateVisuals(Vector3 currentTip_RS)
        {
            if (m_VisualRoot == null)
            {
                return;
            }

            if (m_Stage == CaptureStage.Start)
            {
                return;
            }

            Vector3 end = m_Stage == CaptureStage.End ? currentTip_RS : m_End_RS;
            SetMarker(m_StartMarker, m_StartLabel, m_Start_RS);
            SetMarker(m_EndMarker, m_EndLabel, end);
            m_Line.enabled = true;
            m_Line.SetPosition(0, m_Start_RS);
            m_Line.SetPosition(1, end);
            UpdateArrow(m_Start_RS, end);
        }

        private static void SetMarker(
            GameObject marker,
            TextMeshPro label,
            Vector3 position)
        {
            marker.SetActive(true);
            marker.transform.position = position;
            label.gameObject.SetActive(true);
            label.transform.position = position + Vector3.up * 0.06f;
            if (ViewpointScript.Head != null)
            {
                Vector3 toHead = ViewpointScript.Head.position - label.transform.position;
                if (toHead.sqrMagnitude > Mathf.Epsilon)
                {
                    label.transform.rotation =
                        Quaternion.LookRotation(-toHead.normalized, Vector3.up);
                }
            }
        }

        private void UpdateArrow(Vector3 start, Vector3 end)
        {
            Vector3 delta = end - start;
            if (delta.sqrMagnitude < 0.01f)
            {
                m_Arrow.enabled = false;
                return;
            }

            Vector3 direction = delta.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            if (side.sqrMagnitude < 0.01f)
            {
                side = Vector3.right;
            }
            side.Normalize();

            float wingLength = Mathf.Min(
                0.1f * App.METERS_TO_UNITS,
                delta.magnitude * 0.2f);
            Vector3 arrowBase = end - direction * wingLength;
            m_Arrow.enabled = true;
            m_Arrow.SetPosition(0, arrowBase + side * wingLength * 0.5f);
            m_Arrow.SetPosition(1, end);
            m_Arrow.SetPosition(2, arrowBase - side * wingLength * 0.5f);
        }

        private void DestroyVisuals()
        {
            if (m_VisualRoot != null)
            {
                Destroy(m_VisualRoot);
            }
            if (m_LineMaterial != null)
            {
                Destroy(m_LineMaterial);
            }
            if (m_StartMaterial != null)
            {
                Destroy(m_StartMaterial);
            }
            if (m_EndMaterial != null)
            {
                Destroy(m_EndMaterial);
            }
            m_VisualRoot = null;
        }

        private static string ValidationMessage(ManualColocationValidationError error)
        {
            switch (error)
            {
                case ManualColocationValidationError.LineTooShort:
                    return Localize(
                        "MP_MANUAL_COLOCATION_ERROR_TOO_SHORT");
                case ManualColocationValidationError.InsufficientHorizontalSpan:
                    return Localize(
                        "MP_MANUAL_COLOCATION_ERROR_HORIZONTAL");
                case ManualColocationValidationError.NonFiniteInput:
                    return Localize(
                        "MP_MANUAL_COLOCATION_ERROR_TRACKING");
                default:
                    return Localize(
                        "MP_MANUAL_COLOCATION_ERROR_RETRY");
            }
        }

        private static void ShowInstruction(string message)
        {
            Debug.Log($"[ManualColocation] {message}");
            if (OutputWindowScript.m_Instance != null)
            {
                OutputWindowScript.m_Instance.CreateInfoCardAtController(
                    InputManager.ControllerName.Brush,
                    message,
                    fPopScalar: 0.8f);
            }
        }
    }
}
