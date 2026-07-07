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

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace TiltBrush
{
    public class ControllerGeometry : MonoBehaviour
    {
        // -------------------------------------------------------------------------------------------- //
        // Inspector Data
        // -------------------------------------------------------------------------------------------- //
        [SerializeField] private ControllerStyle m_ControllerStyle = ControllerStyle.None;

        [SerializeField] private Transform m_PointerAttachAnchor;
        [SerializeField] private Transform m_PointerAttachPoint;
        [SerializeField] private Transform m_ToolAttachAnchor;
        [SerializeField] private Transform m_ToolAttachPoint;
        [SerializeField] private Transform m_PinCushionSpawn;
        [SerializeField] private Transform m_MainAxisAttachPoint;
        [SerializeField] private Transform m_CameraAttachPoint;
        [SerializeField] private Transform m_ConsoleAttachPoint;
        [SerializeField] private Transform m_BaseAttachPoint;
        [SerializeField] private Transform m_GripAttachPoint;
        [SerializeField] private Transform m_DropperDescAttachPoint;

        [SerializeField] private Renderer m_MainMesh;
        [SerializeField] private Renderer m_TriggerMesh;
        [SerializeField] private Renderer[] m_OtherMeshes;
        [SerializeField] private Renderer m_LeftGripMesh;
        [SerializeField] private Renderer m_RightGripMesh;
        [SerializeField] private Transform m_PadTouchLocator;
        [SerializeField] private Transform m_TriggerAnchor;
        [SerializeField] private HintObjectScript m_PinHint;
        [SerializeField] private HintObjectScript m_UnpinHint;
        [SerializeField] private HintObjectScript m_PreviewKnotHint;
        [SerializeField] private Renderer m_TransformVisualsRenderer;
        [SerializeField] private GameObject m_ActivateEffectPrefab;
        [SerializeField] private GameObject m_HighlightEffectPrefab;
        [SerializeField] private GameObject m_XRayVisuals;

        [Header("Controller Animations")]
        [Tooltip("Range of rotation for TriggerAnchor, in degrees. Rotation is about the right axis")]
        [SerializeField] private Vector2 m_TriggerRotation;
        [SerializeField] private float m_TouchLocatorTranslateScale = 0.27f;
        [SerializeField] private float m_TouchLocatorTranslateClamp = 0.185f;
        [SerializeField] private Material m_GripReadyMaterial;
        [SerializeField] private Material m_GrippedMaterial;
        [SerializeField] private Vector3 m_LeftGripPopInVector;
        [SerializeField] private Vector3 m_LeftGripPopOutVector;


        [Header("Pad Controls")]
        // This number is difficult to tune if there is any offset between the anchor and its child,
        // because the scale amplifies that offset. The easiest thing to do is to have no offset
        // which means the height of the popup is purely specified by PadPopUpAmount.
        // Note that this value is currently used for both the thumbstick and touchpad popups.
        [SerializeField] float m_PadPopUpAmount;
        [SerializeField] float m_PadScaleAmount;
        [SerializeField] float m_PadSpeed;

        [Header("Haptics")]
        [SerializeField] float m_HapticPulseOn;
        [SerializeField] float m_HapticPulseOff;

        [Header("Vive Pad")]
        [SerializeField] private Transform m_PadAnchor;
        [SerializeField] private Renderer m_PadMesh;

        [Header("Oculus Touch Buttons")]
        [SerializeField] private Transform m_Joystick;
        [SerializeField] private Renderer m_JoystickMesh;
        [SerializeField] private Renderer m_JoystickPad;
        [SerializeField] private Renderer m_Button01Mesh;
        [SerializeField] private Renderer m_Button02Mesh;

        [Header("Wmr Button")]
        [SerializeField] private Renderer m_PinCushion;

        [Header("Steam Frame")]
        [SerializeField] private Renderer[] m_SteamFrameRenderers;
        [SerializeField] private Transform m_SteamFrameTrigger;
        [SerializeField] private Transform m_SteamFrameGrip;
        [SerializeField] private Transform m_SteamFrameThumbstick;
        [SerializeField] private Transform m_SteamFrameThumbstickPivot;
        [SerializeField] private Transform m_SteamFrameDpad;
        [SerializeField] private Transform m_SteamFrameDpadPivot;
        [SerializeField] private Transform m_SteamFrameAButton;
        [SerializeField] private Transform m_SteamFrameBButton;
        [SerializeField] private Transform m_SteamFrameXButton;
        [SerializeField] private Transform m_SteamFrameYButton;
        [SerializeField] private Transform m_SteamFrameMenuButton;
        [SerializeField] private Transform m_SteamFrameViewButton;
        [SerializeField] private Transform m_SteamFrameSystemButton;
        [SerializeField] private Transform m_SteamFrameBumper;

        [Header("Wand objects")]
        [SerializeField] private HintObjectScript m_MenuPanelHintObject;
        [SerializeField] private HintObjectScript m_QuickLoadHintObject;
        [SerializeField] private HintObjectScript m_SwipeHint;

        [Header("Brush objects")]
        [SerializeField] private LineRenderer m_GuideLine;
        [SerializeField] private HintObjectScript m_PaintHintObject;
        [SerializeField] private HintObjectScript m_BrushSizeHintObject;
        [SerializeField] private HintObjectScript m_PointAtPanelsHintObject;
        [SerializeField] private HintObjectScript m_ShareSketchHintObject;
        [SerializeField] private HintObjectScript m_FloatingPanelHintObject;
        [SerializeField] private HintObjectScript m_AdvancedModeHintObject;
        [SerializeField] private HintObjectScript m_SelectionHint;
        [SerializeField] private GameObject m_SelectionHintButton;
        [SerializeField] private HintObjectScript m_DeselectionHint;
        [SerializeField] private GameObject m_DeselectionHintButton;
        [SerializeField] private HintObjectScript m_DuplicateHint;
        [SerializeField] private HintObjectScript m_SaveIconHint;

        // -------------------------------------------------------------------------------------------- //
        // Public Properties
        // -------------------------------------------------------------------------------------------- //
        public Vector2 TriggerRotation { get { return m_TriggerRotation; } }
        public float TouchLocatorTranslateScale { get { return m_TouchLocatorTranslateScale; } }
        public float TouchLocatorTranslateClamp { get { return m_TouchLocatorTranslateClamp; } }
        public Material GripReadyMaterial { get { return m_GripReadyMaterial; } }
        public Material GrippedMaterial { get { return m_GrippedMaterial; } }
        public Material BaseGrippedMaterial { get { return m_BaseGrippedMaterial; } }
        public Vector3 LeftGripPopInVector { get { return m_LeftGripPopInVector; } }
        public Vector3 LeftGripPopOutVector { get { return m_LeftGripPopOutVector; } }

        public Transform PointerAttachAnchor { get { return m_PointerAttachAnchor; } }
        public Transform PointerAttachPoint { get { return m_PointerAttachPoint; } }
        public Transform ToolAttachAnchor { get { return m_ToolAttachAnchor; } }
        public Transform ToolAttachPoint { get { return m_ToolAttachPoint; } }
        public Transform PinCushionSpawn { get { return m_PinCushionSpawn; } }
        public Transform MainAxisAttachPoint { get { return m_MainAxisAttachPoint; } }
        public Transform CameraAttachPoint { get { return m_CameraAttachPoint; } }
        public Transform ConsoleAttachPoint { get { return m_ConsoleAttachPoint; } }
        public Transform BaseAttachPoint { get { return m_BaseAttachPoint; } }
        public Transform GripAttachPoint { get { return m_GripAttachPoint; } }
        public Transform DropperDescAttachPoint { get { return m_DropperDescAttachPoint; } }

        public Renderer MainMesh { get { return m_MainMesh; } }
        public Renderer TriggerMesh { get { return m_TriggerMesh; } }
        public Renderer[] OtherMeshes { get { return m_OtherMeshes; } }
        public Renderer LeftGripMesh { get { return m_LeftGripMesh; } }
        public Renderer RightGripMesh { get { return m_RightGripMesh; } }
        public Transform PadTouchLocator { get { return m_PadTouchLocator; } }
        public Transform TriggerAnchor { get { return m_TriggerAnchor; } }
        public HintObjectScript PinHint { get { return m_PinHint; } }
        public HintObjectScript UnpinHint { get { return m_UnpinHint; } }
        public HintObjectScript PreviewKnotHint { get { return m_PreviewKnotHint; } }
        public HintObjectScript SelectionHint { get { return m_SelectionHint; } }
        public GameObject SelectionHintButton { get { return m_SelectionHintButton; } }
        public HintObjectScript DeselectionHint { get { return m_DeselectionHint; } }
        public GameObject DeselectionHintButton { get { return m_DeselectionHintButton; } }
        public HintObjectScript DuplicateHint { get { return m_DuplicateHint; } }
        public HintObjectScript SaveIconHint { get { return m_SaveIconHint; } }
        public Renderer TransformVisualsRenderer { get { return m_TransformVisualsRenderer; } }
        public GameObject ActivateEffectPrefab { get { return m_ActivateEffectPrefab; } }
        public GameObject HighlightEffectPrefab { get { return m_HighlightEffectPrefab; } }
        public GameObject XRayVisuals { get { return m_XRayVisuals; } }

        // Vive controller components.
        public Transform PadAnchor { get { return m_PadAnchor; } }
        public Renderer PadMesh { get { return m_PadMesh; } }

        // Rift & Knuckles controller components.
        public Transform Joystick { get { return m_Joystick; } }
        public Renderer JoystickMesh { get { return m_JoystickMesh; } }
        public Renderer JoystickPad { get { return m_JoystickPad; } }
        public Renderer Button01Mesh { get { return m_Button01Mesh; } }
        public Renderer Button02Mesh { get { return m_Button02Mesh; } }

        // Wmr controller components.
        public Renderer PinCushionMesh { get { return m_PinCushion; } }

        // Wand objects
        public HintObjectScript QuickLoadHintObject { get { return m_QuickLoadHintObject; } }
        public HintObjectScript SwipeHintObject { get { return m_SwipeHint; } }
        public HintObjectScript MenuPanelHintObject { get { return m_MenuPanelHintObject; } }

        // Brush objects
        public LineRenderer GuideLine { get { return m_GuideLine; } }
        public HintObjectScript PaintHintObject { get { return m_PaintHintObject; } }
        public HintObjectScript BrushSizeHintObject { get { return m_BrushSizeHintObject; } }
        public HintObjectScript PointAtPanelsHintObject { get { return m_PointAtPanelsHintObject; } }
        public HintObjectScript ShareSketchHintObject { get { return m_ShareSketchHintObject; } }
        public HintObjectScript FloatingPanelHintObject { get { return m_FloatingPanelHintObject; } }
        public HintObjectScript AdvancedModeHintObject { get { return m_AdvancedModeHintObject; } }

        public bool PadEnabled { get; set; }

        public BaseControllerBehavior Behavior { get => m_Behavior; }

        public InputManager.ControllerName ControllerName { get => m_ControllerName; }

        public ControllerStyle Style { get => m_ControllerStyle; }

        public Renderer[] SteamFrameRenderers
        {
            get
            {
                if (m_SteamFrameRenderers != null && m_SteamFrameRenderers.Length > 0)
                {
                    return m_SteamFrameRenderers;
                }

                if (m_CachedSteamFrameRenderers == null)
                {
                    var renderRoot = FindSteamFrameRenderRoot();
                    if (renderRoot == null)
                    {
                        Debug.LogWarning($"STEAM_FRAME_GEOM_MISSING_RENDER_MODEL {name}");
                    }
                    m_CachedSteamFrameRenderers = renderRoot != null
                        ? renderRoot.GetComponentsInChildren<Renderer>(true)
                        : new Renderer[0];
                }
                return m_CachedSteamFrameRenderers;
            }
        }

        public void SetControllerEmission(Color color)
        {
            if (Style == ControllerStyle.SteamFrame)
            {
                foreach (var renderer in SteamFrameRenderers)
                {
                    SetRendererEmission(renderer, color);
                }
                return;
            }

            SetRendererEmission(MainMesh, color);
            SetRendererEmission(TriggerMesh, color);
            if (OtherMeshes == null)
            {
                return;
            }
            for (int i = 0; i < OtherMeshes.Length; ++i)
            {
                SetRendererEmission(OtherMeshes[i], color);
            }
        }

        public void SetTransformVisualsTint(Color color)
        {
            if (TransformVisualsRenderer != null)
            {
                TransformVisualsRenderer.material.SetColor("_Color", color);
            }
        }

        public void SetTransformVisualsActive(bool active, float intensity)
        {
            if (TransformVisualsRenderer == null)
            {
                return;
            }

            // Steam Frame still carries the legacy Quest transform visual renderer as backing data.
            // Keep it inactive; Steam Frame highlight meshes are registered through
            // RegisterTransformVisualMeshes().
            if (Style == ControllerStyle.SteamFrame)
            {
                TransformVisualsRenderer.gameObject.SetActive(false);
                return;
            }

            TransformVisualsRenderer.gameObject.SetActive(active);
            TransformVisualsRenderer.material.SetFloat("_Intensity", intensity);
        }

        public void RegisterTransformVisualMeshes(SelectionEffect selectionEffect)
        {
            if (Style == ControllerStyle.SteamFrame)
            {
                foreach (var renderer in SteamFrameRenderers)
                {
                    RegisterRendererMesh(selectionEffect, renderer);
                }
                return;
            }

            RegisterRendererMesh(selectionEffect, TransformVisualsRenderer);
            switch (Style)
            {
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Neo3:
                case ControllerStyle.Zapbox:
                    RegisterRendererMesh(selectionEffect, JoystickPad);
                    break;
                case ControllerStyle.Vive:
                    RegisterRendererMesh(selectionEffect, PadMesh);
                    break;
            }
        }

        public void SetGripVisualState(BaseControllerBehavior.GripState state, Color tint)
        {
            if (Style == ControllerStyle.SteamFrame ||
                Style == ControllerStyle.InitializingUnityXR ||
                Style == ControllerStyle.None ||
                Style == ControllerStyle.Unset ||
                LeftGripMesh == null ||
                RightGripMesh == null)
            {
                return;
            }

            bool manuallyAnimateGrips = Style == ControllerStyle.Vive || Style == ControllerStyle.Wmr;
            switch (state)
            {
                case BaseControllerBehavior.GripState.Standard:
                    if (manuallyAnimateGrips)
                    {
                        LeftGripMesh.transform.localPosition = Vector3.zero;
                        RightGripMesh.transform.localPosition = Vector3.zero;
                    }
                    SetGripRendererMaterial(LeftGripMesh, BaseGrippedMaterial, tint, false);
                    SetGripRendererMaterial(RightGripMesh, BaseGrippedMaterial, tint, false);
                    break;
                case BaseControllerBehavior.GripState.ReadyToGrip:
                    if (manuallyAnimateGrips)
                    {
                        LeftGripMesh.transform.localPosition = LeftGripPopOutVector;
                        Vector3 rightPopOut = LeftGripPopOutVector;
                        rightPopOut.x *= -1.0f;
                        RightGripMesh.transform.localPosition = rightPopOut;
                    }
                    SetGripRendererMaterial(LeftGripMesh, GripReadyMaterial, tint, true);
                    SetGripRendererMaterial(RightGripMesh, GripReadyMaterial, tint, true);
                    break;
                case BaseControllerBehavior.GripState.Gripped:
                    if (manuallyAnimateGrips)
                    {
                        LeftGripMesh.transform.localPosition = LeftGripPopInVector;
                        Vector3 rightPopIn = LeftGripPopInVector;
                        rightPopIn.x *= -1.0f;
                        RightGripMesh.transform.localPosition = rightPopIn;
                    }
                    SetGripRendererMaterial(LeftGripMesh, GrippedMaterial, tint, true);
                    SetGripRendererMaterial(RightGripMesh, GrippedMaterial, tint, true);
                    break;
            }
        }

        private static void SetRendererEmission(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            var material = renderer.material;
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", color);
            }
        }

        private static void RegisterRendererMesh(SelectionEffect selectionEffect, Renderer renderer)
        {
            if (renderer != null && renderer.TryGetComponent<MeshFilter>(out var meshFilter))
            {
                selectionEffect.RegisterMesh(meshFilter);
            }
        }

        private static void SetGripRendererMaterial(
            Renderer renderer, Material material, Color tint, bool applyTint)
        {
            if (renderer == null || material == null)
            {
                return;
            }

            renderer.material = material;
            if (applyTint && renderer.material.HasProperty("_Color"))
            {
                renderer.material.SetColor("_Color", tint);
            }
        }

        // Style is meant to be read-only and immutable, but there is currently one situation
        // that requires it to be writable. TODO: remove when possible?
        public ControllerStyle TempWritableStyle
        {
            set
            {
                if (m_ControllerStyle == value)
                { /* no warning */
                }
                // This is kind of a hack, because the same prefab is used for both "empty geometry"
                // and "initializing steam vr". In all other cases, m_ControllerStyle is expected
                // to be set properly in the prefab. Perhaps we can remove this last mutable case
                // and detect the initializing case differently.
                else if (m_ControllerStyle == ControllerStyle.None && value == ControllerStyle.InitializingUnityXR)
                {
                    /* no warning */
                }
                else
                {
                    Debug.LogWarningFormat(
                        "Unity bug? Prefab had incorrect m_ControllerStyle {0} != {1}; try re-importing it.",
                        m_ControllerStyle, value);
                }
                m_ControllerStyle = value;
            }
        }

        /// Returns null if the ControllerName is invalid, or the requested controller does not exist.
        public ControllerInfo ControllerInfo { get => m_Behavior.ControllerInfo; }

        private bool EmptyGeometry
        {
            get => m_ControllerStyle == ControllerStyle.None
                || m_ControllerStyle == ControllerStyle.InitializingUnityXR;
        }

        // -------------------------------------------------------------------------------------------- //
        // Private Fields
        // -------------------------------------------------------------------------------------------- //

        class PopupAnimState
        {
            public readonly VrInput input;
            public readonly Transform anchor;
            public readonly float initialY;
            public readonly float initialScale;
            public float current;

            public PopupAnimState(Transform anchor, VrInput input)
            {
                this.anchor = anchor;
                this.input = input;
                this.current = 0;
                if (anchor != null)
                {
                    this.initialY = anchor.localPosition.y;
                    this.initialScale = anchor.localScale.x;
                }
            }
        }

        private struct SteamFramePose
        {
            public readonly Vector3 position;
            public readonly Vector3 eulerAngles;

            public SteamFramePose(Vector3 position, Vector3 eulerAngles)
            {
                this.position = position;
                this.eulerAngles = eulerAngles;
            }
        }

        private struct SteamFramePartState
        {
            public readonly Transform transform;
            public readonly Vector3 initialLocalPosition;
            public readonly Quaternion initialLocalRotation;

            public SteamFramePartState(Transform transform)
            {
                this.transform = transform;
                this.initialLocalPosition = transform != null ? transform.localPosition : Vector3.zero;
                this.initialLocalRotation = transform != null ? transform.localRotation : Quaternion.identity;
            }

            public bool IsValid => transform != null;
        }

        private PopupAnimState m_JoyAnimState;
        private PopupAnimState m_PadAnimState;
        private int m_LastPadButton;
        private Material m_BaseGrippedMaterial;
        private float m_LogitechPenHandednessHysteresis = 10.0f;
        // True if we're the default orientation, false if we need to be rotated 180 degrees.
        private bool m_LogitechPenHandedness;
        private Renderer[] m_CachedSteamFrameRenderers;
        private bool m_SteamFramePartsCached;
        private bool m_SteamFrameIsRight;
        private SteamFramePartState m_SteamFrameTriggerPart;
        private SteamFramePartState m_SteamFrameGripPart;
        private SteamFramePartState m_SteamFrameThumbstickPart;
        private SteamFramePartState m_SteamFrameThumbstickPivotPart;
        private SteamFramePartState m_SteamFrameDpadPart;
        private SteamFramePartState m_SteamFrameDpadPivotPart;
        private SteamFramePartState m_SteamFramePrimaryButtonPart;
        private SteamFramePartState m_SteamFrameSecondaryButtonPart;
        private SteamFramePartState m_SteamFrameTertiaryButtonPart;
        private SteamFramePartState m_SteamFrameQuaternaryButtonPart;
        private SteamFramePartState m_SteamFrameMenuButtonPart;
        private SteamFramePartState m_SteamFrameSystemButtonPart;
        private SteamFramePartState m_SteamFrameBumperPart;

        private static Transform FindDeepChild(Transform root, string childName)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                var match = FindDeepChild(child, childName);
                if (match != null)
                {
                    return match;
                }
            }
            return null;
        }

        private static Transform ResolveSteamFramePart(
            Transform assignedPart, Transform renderRoot, string fallbackName)
        {
            return assignedPart != null
                ? assignedPart
                : FindDeepChild(renderRoot, fallbackName);
        }

        private Transform FindSteamFrameRenderRoot()
        {
            var originOffset = transform.Find("OriginOffset");
            if (originOffset == null)
            {
                return null;
            }

            var componentRoot = FindDeepChild(originOffset, "SteamFrameComponentModel");
            if (componentRoot != null)
            {
                return componentRoot;
            }

            return FindDeepChild(originOffset, "SteamFrameRenderModel");
        }

        private Transform FindSteamFrameFullRenderModelRoot()
        {
            var originOffset = transform.Find("OriginOffset");
            return originOffset != null
                ? FindDeepChild(originOffset, "SteamFrameRenderModel")
                : null;
        }

        private void ApplySteamFrameAttachPointOverrides()
        {
            var renderRoot = FindSteamFrameRenderRoot();
            if (renderRoot == null)
            {
                Debug.LogWarning($"STEAM_FRAME_GEOM_MISSING_ATTACH_ROOT {name}");
                return;
            }

            bool isRight = name.Contains("Right");
            bool isLeft = name.Contains("Left");
            if (!isLeft && !isRight)
            {
                isRight = m_ControllerName == InputManager.ControllerName.Brush;
            }

            SteamFramePose aimPose = isRight
                ? new SteamFramePose(
                    new Vector3(-0.012694f, -0.02522f, 0.020687f),
                    new Vector3(-40.0f, 0.0f, 0.0f))
                : new SteamFramePose(
                    new Vector3(0.012694f, -0.02522f, 0.020687f),
                    new Vector3(-40.0f, 0.0f, 0.0f));
            SteamFramePose gripPose = isRight
                ? new SteamFramePose(
                    new Vector3(0.003117f, -0.004277f, 0.099501f),
                    new Vector3(2.8091f, 0.0f, 0.0f))
                : new SteamFramePose(
                    new Vector3(-0.003117f, -0.004277f, 0.099501f),
                    new Vector3(2.8091f, 0.0f, 0.0f));
            SteamFramePose basePose = isRight
                ? new SteamFramePose(
                    new Vector3(0.0024f, -0.0024f, 0.1531f),
                    new Vector3(-0.4f, -180.0f, 0.0f))
                : new SteamFramePose(
                    new Vector3(-0.0024f, -0.0024f, 0.1531f),
                    new Vector3(-0.4f, 180.0f, 0.0f));

            ApplySteamFramePose(renderRoot, m_PointerAttachAnchor, aimPose);
            ApplySteamFramePose(renderRoot, m_ToolAttachAnchor, aimPose);
            ApplySteamFramePose(renderRoot, m_GripAttachPoint, gripPose);
            ApplySteamFramePose(renderRoot, m_BaseAttachPoint, basePose);
            SetAttachPointDirection(m_PointerAttachPoint, 0.225f);
            SetAttachPointDirection(m_ToolAttachPoint, 0.325f);

            Debug.Log(
                $"STEAM_FRAME_GEOM_ATTACH_RETARGET geometry={name} side={(isRight ? "Right" : "Left")} renderRoot={renderRoot.name}");
        }

        private static void ApplySteamFramePose(
            Transform renderRoot, Transform target, SteamFramePose pose)
        {
            if (target == null)
            {
                return;
            }

            target.position = renderRoot.TransformPoint(pose.position);
            target.rotation = renderRoot.rotation * Quaternion.Euler(pose.eulerAngles);
        }

        private static void SetAttachPointDirection(Transform target, float distance)
        {
            if (target == null)
            {
                return;
            }

            target.localPosition = Vector3.forward * distance;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
        }

        // Cached value of transform.parent.GetComponent<BaseControllerBehavior>()
        private BaseControllerBehavior m_Behavior;
        // Cached value of transform.parent.GetComponent<BaseControllerBehavior>().ControllerName
        private InputManager.ControllerName m_ControllerName;

        // -------------------------------------------------------------------------------------------- //
        // Unity Events
        // -------------------------------------------------------------------------------------------- //

        private void Awake()
        {
            if (Style == ControllerStyle.SteamFrame)
            {
                _ = SteamFrameRenderers;
                CacheSteamFrameAnimatedParts();
                DisableSteamFrameFullRenderModelIfComponentModelExists();
                DisableSteamFrameLegacyRenderers();
            }

            if (LeftGripMesh != null)
            {
                m_BaseGrippedMaterial = LeftGripMesh.material;
            }
            m_JoyAnimState = new PopupAnimState(Joystick, VrInput.Thumbstick);
            m_PadAnimState = new PopupAnimState(PadAnchor, VrInput.Touchpad);
        }

        // -------------------------------------------------------------------------------------------- //
        // Private Helper Methods & Properties
        // -------------------------------------------------------------------------------------------- //

        // Quick access to the Material Catalog.
        private ControllerMaterialCatalog Materials
        {
            get { return ControllerMaterialCatalog.m_Instance; }
        }

        // Returns the ratio of the given controller input.
        private float GetPadRatio(VrInput input)
        {
            return SketchControlsScript.m_Instance.GetControllerPadShaderRatio(ControllerName, input);
        }

        // Animates the pad popping out of the controller.
        // Pass:
        //   active   if null, drives the animation to zero (presumably because there is nothing
        //            to put on the pad)
        private void UpdatePadAnimation(PopupAnimState state, Material active)
        {
            if (EmptyGeometry || state.anchor == null) { return; }

            float target = 0.0f;

            InputManager.ControllerName name = ControllerName;
            if (active != null && PadEnabled &&
                SketchControlsScript.m_Instance.ShouldRespondToPadInput(name) &&
                ControllerInfo.GetVrInputTouch(state.input))
            {
                target = 1.0f;
            }

            if (target > state.current)
            {
                if (state.current == 0)
                {
                    InputManager.m_Instance.TriggerHaptics(name, m_HapticPulseOn); // Leaving 0
                }
                state.current = Mathf.Min(target, state.current + m_PadSpeed * Time.deltaTime);
            }
            else if (target < state.current)
            {
                state.current = Mathf.Max(target, state.current - m_PadSpeed * Time.deltaTime);
                if (state.current == 0)
                {
                    InputManager.m_Instance.TriggerHaptics(name, m_HapticPulseOff); // Arriving at 0
                }
            }
            else
            {
                return; // No real need to mess with the transform
            }

            Vector3 vPos = state.anchor.localPosition;
            vPos.y = state.initialY + (state.current * m_PadPopUpAmount);
            state.anchor.localPosition = vPos;

            state.anchor.localScale =
                Vector3.one * (state.initialScale + (state.current * m_PadScaleAmount));
        }

        private void UpdateLogitechPadHandedness(Transform padXf)
        {
            Vector3 headUp = ViewpointScript.Head.up;
            Vector3 controllerRight = transform.right;
            float angle = Vector3.Angle(headUp, controllerRight);
            float flipAngle = m_LogitechPenHandedness ?
                90.0f - m_LogitechPenHandednessHysteresis :
                90.0f + m_LogitechPenHandednessHysteresis;
            m_LogitechPenHandedness = angle > flipAngle;
        }

        private void DisableSteamFrameLegacyRenderers()
        {
            var originOffset = transform.Find("OriginOffset");
            if (originOffset == null)
            {
                return;
            }

            DisableRenderersInChild(originOffset, "monterey_controller_L");
            DisableRenderersInChild(originOffset, "monterey_controller_R");
            DisableRenderersInChild(originOffset, "Pad Offset Rotation");
            DisableRenderersInChild(originOffset, "SteamFramePart_status");
        }

        private void CacheSteamFrameAnimatedParts()
        {
            if (m_SteamFramePartsCached)
            {
                return;
            }

            m_SteamFramePartsCached = true;
            var renderRoot = FindSteamFrameRenderRoot();
            if (renderRoot == null)
            {
                return;
            }

            bool isRight = name.Contains("Right");
            bool isLeft = name.Contains("Left");
            if (!isLeft && !isRight)
            {
                isRight = m_ControllerName == InputManager.ControllerName.Brush;
            }
            m_SteamFrameIsRight = isRight;

            Transform trigger = ResolveSteamFramePart(
                m_SteamFrameTrigger, renderRoot, "SteamFramePart_trigger");
            Transform grip = ResolveSteamFramePart(
                m_SteamFrameGrip, renderRoot, "SteamFramePart_grip_button");
            Transform thumbstick = ResolveSteamFramePart(
                m_SteamFrameThumbstick, renderRoot, "SteamFramePart_thumbstick");
            Transform thumbstickPivot = ResolveSteamFramePart(
                m_SteamFrameThumbstickPivot, renderRoot, "SteamFrameThumbstickPivot");
            Transform dpad = ResolveSteamFramePart(
                m_SteamFrameDpad, renderRoot, "SteamFramePart_dpad");
            Transform dpadPivot = ResolveSteamFramePart(
                m_SteamFrameDpadPivot, renderRoot, "SteamFrameDpadPivot");
            Transform aButton = ResolveSteamFramePart(
                m_SteamFrameAButton, renderRoot, "SteamFramePart_a_button");
            Transform bButton = ResolveSteamFramePart(
                m_SteamFrameBButton, renderRoot, "SteamFramePart_b_button");
            Transform xButton = ResolveSteamFramePart(
                m_SteamFrameXButton, renderRoot, "SteamFramePart_x_button");
            Transform yButton = ResolveSteamFramePart(
                m_SteamFrameYButton, renderRoot, "SteamFramePart_y_button");
            Transform menuButton = ResolveSteamFramePart(
                m_SteamFrameMenuButton, renderRoot, "SteamFramePart_menu_button");
            Transform viewButton = ResolveSteamFramePart(
                m_SteamFrameViewButton, renderRoot, "SteamFramePart_view_button");
            Transform systemButton = ResolveSteamFramePart(
                m_SteamFrameSystemButton, renderRoot, "SteamFramePart_system_button");
            Transform bumper = ResolveSteamFramePart(
                m_SteamFrameBumper, renderRoot, "SteamFramePart_bumper");

            m_SteamFrameTriggerPart = new SteamFramePartState(trigger);
            m_SteamFrameGripPart = new SteamFramePartState(grip);
            m_SteamFrameThumbstickPart = new SteamFramePartState(thumbstick);
            m_SteamFrameThumbstickPivotPart = new SteamFramePartState(thumbstickPivot);
            m_SteamFrameDpadPart = new SteamFramePartState(dpad);
            m_SteamFrameDpadPivotPart = new SteamFramePartState(dpadPivot);
            m_SteamFramePrimaryButtonPart = new SteamFramePartState(isRight ? aButton : dpad);
            m_SteamFrameSecondaryButtonPart = new SteamFramePartState(isRight ? bButton : viewButton);
            m_SteamFrameTertiaryButtonPart = new SteamFramePartState(isRight ? xButton : systemButton);
            m_SteamFrameQuaternaryButtonPart = new SteamFramePartState(isRight ? yButton : bumper);
            m_SteamFrameMenuButtonPart = new SteamFramePartState(isRight ? menuButton : viewButton);
            m_SteamFrameSystemButtonPart = new SteamFramePartState(systemButton);
            m_SteamFrameBumperPart = new SteamFramePartState(bumper);
        }

        private void UpdateSteamFramePartAnimation()
        {
            if (EmptyGeometry) { return; }

            var info = ControllerInfo;
            if (info == null)
            {
                return;
            }

            CacheSteamFrameAnimatedParts();

            InputDevice steamFrameDevice = FindSteamFrameInputDevice();
            InputDevice handDevice = FindHandInputDevice();
            Vector2 thumbstick = ReadSteamFrameVector2(steamFrameDevice, "thumbstick");
            if (thumbstick == Vector2.zero)
            {
                thumbstick = ReadFirstVector2Control(
                    handDevice, "thumbstick", "joystick", "primary2DAxis");
            }
            if (thumbstick == Vector2.zero)
            {
                thumbstick = info.GetThumbStickValue();
            }

            float grip = Mathf.Max(
                info.GetGripValue(),
                ReadSteamFrameAxis(steamFrameDevice, "grip"),
                ReadSteamFrameButton(steamFrameDevice, "gripPressed") ? 1.0f : 0.0f,
                ReadFirstAxisControl(handDevice, "grip", "gripPressed", "gripForce"));

            ApplySteamFrameTriggerAnimation(Mathf.Clamp01(
                Mathf.Max(
                    info.GetTriggerRatio(),
                    ReadSteamFrameAxis(steamFrameDevice, "trigger"),
                    ReadFirstAxisControl(handDevice, "trigger", "triggerPressed"))));
            ApplySteamFrameGripAnimation(Mathf.Clamp01(grip));
            ApplySteamFrameThumbstickAnimation(
                Vector2.ClampMagnitude(thumbstick, 1.0f),
                ReadSteamFrameButton(steamFrameDevice, "thumbstickClicked") ||
                    ReadFirstButtonControl(
                        handDevice, "thumbstickClicked", "thumbstickClick", "primary2DAxisClick"));

            bool faceBottom = info.GetVrInput(VrInput.Button01) ||
                ReadSteamFrameButton(steamFrameDevice, "faceButtonBottom") ||
                ReadFirstButtonControl(handDevice, "primaryButton");
            bool faceInside = ReadSteamFrameButton(steamFrameDevice, "faceButtonInside");
            bool faceOutside = info.GetVrInput(VrInput.Button02) ||
                ReadSteamFrameButton(steamFrameDevice, "faceButtonOutside") ||
                ReadFirstButtonControl(handDevice, "secondaryButton");
            bool faceTop = ReadSteamFrameButton(steamFrameDevice, "faceButtonTop");
            bool menu = ReadSteamFrameButton(steamFrameDevice, "menu");
            bool bumper = ReadSteamFrameButton(steamFrameDevice, "bumper");

            if (m_SteamFrameIsRight)
            {
                ApplySteamFrameButtonPress(
                    m_SteamFramePrimaryButtonPart,
                    faceBottom,
                    GetSteamFrameRightFaceButtonAxis(),
                    0.0014f);
                ApplySteamFrameButtonPress(
                    m_SteamFrameSecondaryButtonPart,
                    faceOutside,
                    GetSteamFrameRightFaceButtonAxis(),
                    0.0014f);
                ApplySteamFrameButtonPress(
                    m_SteamFrameTertiaryButtonPart,
                    faceInside,
                    GetSteamFrameRightFaceButtonAxis(),
                    0.0014f);
                ApplySteamFrameButtonPress(
                    m_SteamFrameQuaternaryButtonPart,
                    faceTop,
                    GetSteamFrameRightFaceButtonAxis(),
                    0.0014f);
            }
            else
            {
                Vector2 dpad = new Vector2(
                    (faceInside ? 1.0f : 0.0f) - (faceOutside ? 1.0f : 0.0f),
                    (faceTop ? 1.0f : 0.0f) - (faceBottom ? 1.0f : 0.0f));
                ApplySteamFrameDpadAnimation(
                    Vector2.ClampMagnitude(dpad, 1.0f),
                    faceBottom || faceInside || faceOutside || faceTop);
                ApplySteamFrameButtonPress(
                    m_SteamFrameSystemButtonPart,
                    false,
                    GetSteamFrameLeftMenuButtonAxis(),
                    0.00085f);
            }

            ApplySteamFrameButtonPress(
                m_SteamFrameMenuButtonPart,
                menu,
                m_SteamFrameIsRight
                    ? GetSteamFrameRightFaceButtonAxis()
                    : GetSteamFrameLeftMenuButtonAxis(),
                0.00085f);
            ApplySteamFrameBumperAnimation(bumper);
        }

        private InputDevice FindSteamFrameInputDevice()
        {
            string wantedUsage = m_SteamFrameIsRight ? "RightHand" : "LeftHand";
            foreach (InputDevice device in InputSystem.devices)
            {
                if (!IsSteamFrameInputDevice(device))
                {
                    continue;
                }

                foreach (var usage in device.usages)
                {
                    if (usage.ToString() == wantedUsage)
                    {
                        return device;
                    }
                }
            }

            return null;
        }

        private InputDevice FindHandInputDevice()
        {
            string wantedUsage = m_SteamFrameIsRight ? "RightHand" : "LeftHand";
            foreach (InputDevice device in InputSystem.devices)
            {
                foreach (var usage in device.usages)
                {
                    if (usage.ToString() == wantedUsage)
                    {
                        return device;
                    }
                }
            }

            return null;
        }

        private static bool IsSteamFrameInputDevice(InputDevice device)
        {
            string layout = device.layout.ToString();
            string name = device.name ?? string.Empty;
            string displayName = device.displayName ?? string.Empty;
            return layout.Contains("SteamFrameController") ||
                displayName.Contains("Steam Frame") ||
                name.Contains("SteamFrame");
        }

        private static bool ReadSteamFrameButton(InputDevice device, string controlName)
        {
            ButtonControl control = device?.TryGetChildControl<ButtonControl>(controlName);
            return control != null && control.isPressed;
        }

        private static float ReadSteamFrameAxis(InputDevice device, string controlName)
        {
            AxisControl control = device?.TryGetChildControl<AxisControl>(controlName);
            return control != null ? control.ReadValue() : 0.0f;
        }

        private static Vector2 ReadSteamFrameVector2(InputDevice device, string controlName)
        {
            Vector2Control control = device?.TryGetChildControl<Vector2Control>(controlName);
            return control != null ? control.ReadValue() : Vector2.zero;
        }

        private static bool ReadFirstButtonControl(InputDevice device, params string[] controlNames)
        {
            if (device == null)
            {
                return false;
            }

            for (int i = 0; i < controlNames.Length; i++)
            {
                ButtonControl control =
                    device.TryGetChildControl<ButtonControl>(controlNames[i]);
                if (control != null && control.isPressed)
                {
                    return true;
                }
            }

            return false;
        }

        private static float ReadFirstAxisControl(InputDevice device, params string[] controlNames)
        {
            if (device == null)
            {
                return 0.0f;
            }

            float value = 0.0f;
            for (int i = 0; i < controlNames.Length; i++)
            {
                AxisControl axisControl =
                    device.TryGetChildControl<AxisControl>(controlNames[i]);
                if (axisControl != null)
                {
                    value = Mathf.Max(value, axisControl.ReadValue());
                    continue;
                }

                ButtonControl buttonControl =
                    device.TryGetChildControl<ButtonControl>(controlNames[i]);
                if (buttonControl != null && buttonControl.isPressed)
                {
                    value = 1.0f;
                }
            }

            return value;
        }

        private static Vector2 ReadFirstVector2Control(InputDevice device, params string[] controlNames)
        {
            if (device == null)
            {
                return Vector2.zero;
            }

            for (int i = 0; i < controlNames.Length; i++)
            {
                Vector2Control control =
                    device.TryGetChildControl<Vector2Control>(controlNames[i]);
                if (control == null)
                {
                    continue;
                }

                Vector2 value = control.ReadValue();
                if (value != Vector2.zero)
                {
                    return value;
                }
            }

            return Vector2.zero;
        }

        private void ApplySteamFrameTriggerAnimation(float trigger)
        {
            Vector3 pivot = m_SteamFrameIsRight
                ? new Vector3(-0.008507383f, -0.025102803f, 0.043208465f)
                : new Vector3(0.008507593f, -0.025103247f, 0.043208539f);
            Vector3 axis = m_SteamFrameIsRight
                ? new Vector3(0.981060743f, 0.075684428f, -0.178302228f)
                : new Vector3(0.986111045f, -0.126526967f, 0.107590787f);

            ApplySteamFramePartRotationAroundPivot(
                m_SteamFrameTriggerPart, pivot, axis, Mathf.Lerp(0.0f, -12.5f, trigger));
        }

        private void ApplySteamFrameGripAnimation(float grip)
        {
            Vector3 pivot = m_SteamFrameIsRight
                ? new Vector3(-0.015135353f, -0.002164294f, 0.056575201f)
                : new Vector3(0.015135575f, -0.002164703f, 0.056575201f);
            Vector3 axis = m_SteamFrameIsRight
                ? new Vector3(0.165419415f, -0.806284010f, 0.567928314f)
                : new Vector3(0.165418714f, 0.806282401f, -0.567930639f);

            ApplySteamFramePartRotationAroundPivot(
                m_SteamFrameGripPart, pivot, axis, Mathf.Lerp(0.0f, 9.5f, grip));
        }

        private void ApplySteamFrameThumbstickAnimation(Vector2 value, bool clicked)
        {
            Vector3 pivot;
            Quaternion axisFrame;
            if (m_SteamFrameThumbstickPivotPart.IsValid)
            {
                pivot = m_SteamFrameThumbstickPivotPart.initialLocalPosition;
                axisFrame = m_SteamFrameThumbstickPivotPart.initialLocalRotation;
            }
            else
            {
                pivot = m_SteamFrameIsRight
                    ? new Vector3(-0.021772077f, -0.004882282f, 0.056458842f)
                    : new Vector3(0.021772299f, -0.004882690f, 0.056458838f);
                Vector3 euler = m_SteamFrameIsRight
                    ? new Vector3(-35.252246f, 10.270902f, 4.411401f)
                    : new Vector3(-35.252409f, -10.270845f, -4.411360f);
                axisFrame = SteamFrameRotateXYZ(euler);
            }

            Vector3 axisX = axisFrame * Vector3.right;
            Vector3 axisY = axisFrame * Vector3.forward;
            Quaternion rotation =
                Quaternion.AngleAxis(-value.y * 20.0f, axisX.normalized) *
                Quaternion.AngleAxis(value.x * 20.0f, axisY.normalized);

            if (m_SteamFrameThumbstickPivotPart.IsValid)
            {
                m_SteamFrameThumbstickPivotPart.transform.localRotation =
                    rotation * m_SteamFrameThumbstickPivotPart.initialLocalRotation;
            }

            ApplySteamFramePartRotationAroundPivot(m_SteamFrameThumbstickPart, pivot, rotation);
            if (m_SteamFrameThumbstickPart.IsValid)
            {
                m_SteamFrameThumbstickPart.transform.localPosition +=
                    clicked ? new Vector3(0.0f, -0.0005f, 0.0f) : Vector3.zero;
            }
        }

        private void ApplySteamFrameDpadAnimation(Vector2 value, bool pressed)
        {
            if (!m_SteamFrameDpadPart.IsValid)
            {
                return;
            }

            Vector3 pivot;
            Quaternion axisFrame;
            if (m_SteamFrameDpadPivotPart.IsValid)
            {
                pivot = m_SteamFrameDpadPivotPart.initialLocalPosition;
                axisFrame = m_SteamFrameDpadPivotPart.initialLocalRotation;
            }
            else
            {
                pivot = new Vector3(-0.003449810f, 0.000989080f, 0.040305495f);
                axisFrame = SteamFrameRotateXYZ(
                    new Vector3(-35.252409f, -10.270845f, -4.411360f));
            }

            Vector3 axisX = axisFrame * Vector3.right;
            Vector3 axisY = axisFrame * Vector3.forward;
            Quaternion rotation =
                Quaternion.AngleAxis(-value.y * 7.0f, axisX.normalized) *
                Quaternion.AngleAxis(-value.x * 7.0f, axisY.normalized);

            if (m_SteamFrameDpadPivotPart.IsValid)
            {
                m_SteamFrameDpadPivotPart.transform.localRotation =
                    rotation * m_SteamFrameDpadPivotPart.initialLocalRotation;
            }

            ApplySteamFramePartRotationAroundPivot(m_SteamFrameDpadPart, pivot, rotation);
            m_SteamFrameDpadPart.transform.localPosition +=
                pressed ? new Vector3(0.0f, -0.000927f, 0.0f) : Vector3.zero;
        }

        private void ApplySteamFrameBumperAnimation(bool pressed)
        {
            Vector3 pivot = m_SteamFrameIsRight
                ? new Vector3(-0.039387941f, -0.010576765f, 0.051696081f)
                : new Vector3(0.039388161f, -0.010577183f, 0.051696073f);
            Vector3 axis = m_SteamFrameIsRight
                ? new Vector3(-0.165419042f, 0.806284010f, -0.567928374f)
                : new Vector3(-0.165418446f, -0.806282401f, 0.567930698f);

            ApplySteamFramePartRotationAroundPivot(
                m_SteamFrameBumperPart, pivot, axis, pressed ? -2.6f : 0.0f);
        }

        private static Vector3 GetSteamFrameRightFaceButtonAxis()
        {
            return new Vector3(0.165419310f, -0.806284010f, 0.567928255f);
        }

        private static Vector3 GetSteamFrameLeftMenuButtonAxis()
        {
            return new Vector3(-0.165418640f, -0.806282520f, 0.567930639f);
        }

        private static Quaternion SteamFrameRotateXYZ(Vector3 euler)
        {
            return Quaternion.AngleAxis(euler.z, Vector3.forward) *
                Quaternion.AngleAxis(euler.y, Vector3.up) *
                Quaternion.AngleAxis(euler.x, Vector3.right);
        }

        private static void ApplySteamFramePartRotationAroundPivot(
            SteamFramePartState part, Vector3 pivot, Vector3 axis, float degrees)
        {
            ApplySteamFramePartRotationAroundPivot(
                part, pivot, Quaternion.AngleAxis(degrees, axis.normalized));
        }

        private static void ApplySteamFramePartRotationAroundPivot(
            SteamFramePartState part, Vector3 pivot, Quaternion rotation)
        {
            if (!part.IsValid)
            {
                return;
            }

            part.transform.localPosition =
                pivot + (rotation * (part.initialLocalPosition - pivot));
            part.transform.localRotation = rotation * part.initialLocalRotation;
        }

        private static void ApplySteamFrameButtonPress(
            SteamFramePartState part, bool isPressed, Vector3 axis, float distance)
        {
            if (!part.IsValid)
            {
                return;
            }

            part.transform.localPosition = part.initialLocalPosition +
                (axis.normalized * (isPressed ? distance : 0.0f));
            part.transform.localRotation = part.initialLocalRotation;
        }

        private void DisableSteamFrameFullRenderModelIfComponentModelExists()
        {
            var componentRoot = FindSteamFrameRenderRoot();
            if (componentRoot == null || componentRoot.name != "SteamFrameComponentModel")
            {
                return;
            }

            var fullRenderRoot = FindSteamFrameFullRenderModelRoot();
            if (fullRenderRoot == null)
            {
                return;
            }

            Renderer[] renderers = fullRenderRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }
        }

        private static void DisableRenderersInChild(Transform parent, string childName)
        {
            var child = FindDeepChild(parent, childName);
            if (child == null)
            {
                return;
            }

            var renderers = child.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }
        }

        // Returns the active material when the pad is touched, else returns inactive.
        private Material SelectPadTouched(Material active, Material inactive)
        {
            return SelectIfTouched(VrInput.Touchpad, active, inactive);
        }

        // Returns the active material when the thumbstick is activated, else returns inactive material.
        private Material SelectThumbStickTouched(Material active, Material inactive)
        {
            return SelectIfTouched(VrInput.Thumbstick, active, inactive);
        }

        // Returns the active parameter when the pad is down/clicked, else returns inactive.
        private Material SelectBasedOn(Material active, Material inactive)
        {
            var info = ControllerInfo;
            // TODO: we should remove this MenuContextClick command in favor of calling it Button04;
            // (and potentially rename button04 to something more descriptive). The extra indirection isn't
            // buying us anything, and it prevents us from using GetVrInputTouch(button04) which is
            // actually what we mean here.
            if (info != null && info.GetCommand(InputManager.SketchCommands.MenuContextClick))
            {
                return active;
            }
            else
            {
                return inactive;
            }
        }

        // Returns the active material when the input is touched, else returns inactive.
        private T SelectIfTouched<T>(VrInput input, T active, T inactive)
        {
            var info = ControllerInfo;
            if (info != null && info.GetVrInputTouch(input))
            {
                return active;
            }
            else
            {
                return inactive;
            }
        }

        private void SetColor(Renderer obj,
                              VrInput input,
                              string colorName,
                              Color activeColor,
                              Color inactiveColor)
        {
            if (obj != null)
            {
                obj.material.SetColor(colorName, SelectIfTouched(input, activeColor, inactiveColor));
            }
        }

        private void RefreshMaterialTint(Color tintColor)
        {
            switch (Style)
            {
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    JoystickMesh.material.SetColor("_EmissionColor", tintColor);
                    JoystickPad.material.SetColor("_EmissionColor", tintColor);
                    Button01Mesh.material.SetColor("_EmissionColor", tintColor);
                    Button02Mesh.material.SetColor("_EmissionColor", tintColor);
                    break;
                case ControllerStyle.Vive:
                case ControllerStyle.LogitechPen:
                    PadMesh.material.SetColor("_EmissionColor", tintColor);
                    break;
                case ControllerStyle.Wmr:
                    JoystickMesh.material.SetColor("_EmissionColor", tintColor);
                    PinCushionMesh.material.SetColor("_EmissionColor", tintColor);
                    PadMesh.material.SetColor("_EmissionColor", tintColor);
                    break;
            }
        }

        private void UpdateButtonColor()
        {
            if (EmptyGeometry) { return; }
            if (Style == ControllerStyle.SteamFrame) { return; }
            if (!App.VrSdk.AnalogIsStick(ControllerName))
            {
                return;
            }

            Color m_LitButtonColor = new Color(.65f, .65f, .65f);
            Color m_DarkButtonColor = new Color(.2f, .2f, .2f);
            var currentColor = PointerManager.m_Instance.MainPointer.GetCurrentColor();
            var darkColor = currentColor * m_DarkButtonColor;

            SetColor(Button01Mesh, VrInput.Button01, "_EmissionColor",
                currentColor, darkColor);
            SetColor(Button02Mesh, VrInput.Button02, "_EmissionColor",
                currentColor, darkColor);
            SetColor(JoystickPad, VrInput.Thumbstick, "_EmissionColor",
                currentColor, darkColor);
            SetColor(JoystickMesh, VrInput.Thumbstick, "_EmissionColor",
                currentColor, darkColor);

            currentColor = m_LitButtonColor;
            darkColor = m_DarkButtonColor;

            SetColor(Button01Mesh, VrInput.Button01, "_Color",
                currentColor, darkColor);
            SetColor(Button02Mesh, VrInput.Button02, "_Color",
                currentColor, darkColor);
            SetColor(JoystickPad, VrInput.Thumbstick, "_Color",
                currentColor, darkColor);
            SetColor(JoystickMesh, VrInput.Thumbstick, "_Color",
                currentColor, darkColor);
        }

        // -------------------------------------------------------------------------------------------- //
        // Public Event API
        // -------------------------------------------------------------------------------------------- //

        // Called after the behavior associated with this geometry (ie, its transform.parent) changes.
        // Only really for use by BaseControllerBehavior.
        public void OnBehaviorChanged()
        {
            m_Behavior = transform.parent.GetComponent<BaseControllerBehavior>();

            // Cache ControllerName, since we use it pretty much everywhere.
            if (m_Behavior == null)
            {
                Debug.LogWarning("Unexpected: Geometry has no behavior");
                m_ControllerName = InputManager.ControllerName.None;
            }
            else
            {
                m_ControllerName = m_Behavior.ControllerName;
            }

            if (Style == ControllerStyle.SteamFrame)
            {
                ApplySteamFrameAttachPointOverrides();
            }
        }

        // Called after materials are assigned, allowing the controller geometry to apply state to the
        // assigned materials.
        public void OnMaterialsAssigned(Color tintColor)
        {
            if (EmptyGeometry) { return; }

            switch (Style)
            {
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    // TODO: This code is generic enough to be used for all "real" controllers; merge?
                    UpdatePadAnimation(m_JoyAnimState, JoystickPad.material);
                    UpdatePadAnimation(m_PadAnimState, PadMesh.material);
                    break;
                case ControllerStyle.Wmr:
                    // TODO: I'm pretty sure that the only reason Wmr is different (here and everywhere else) is that
                    // its prefab accidentally swapped the meaning of JoystickPad and JoystickMesh. And now we're
                    // fixing that up in code rather than in data.
                    UpdatePadAnimation(m_JoyAnimState, JoystickMesh.material);
                    UpdatePadAnimation(m_PadAnimState, PadMesh.material);
                    break;
                case ControllerStyle.Vive:
                case ControllerStyle.LogitechPen:
                    UpdatePadAnimation(m_PadAnimState, PadMesh.material);
                    break;
                case ControllerStyle.SteamFrame:
                    UpdateSteamFramePartAnimation();
                    break;
            }

            RefreshMaterialTint(tintColor);
            UpdateButtonColor();
        }

        // -------------------------------------------------------------------------------------------- //
        // Material Assignment Controls
        // -------------------------------------------------------------------------------------------- //

        // When adding new material controls:
        // -------------------------------------------------------------------------------------------- //
        //   * Prefer ToggleFoo(bool) or ShowFoo() patterns.                                            //
        //   * NEVER expose hardware specific commands (SetButton01, etc)                               //
        //   * Add comments to document what the action is doing.                                       //
        //   * Only assign the necessary materials, assume the controller is in a clean state.          //
        //   * Test/noodle with rift, then test/noodle Vive (or vice versa).                            //
        //   * Dont add comments about callers, use "find references" instead.                          //
        // -------------------------------------------------------------------------------------------- //

        // Configures the controller for the intro tutorial.
        // Controller should send a strong "you cant do anything" signal, by either unassigning all
        // materials or by explicitly displaing an "X" etc.
        public void ShowTutorialMode()
        {
            switch (Style)
            {
                case ControllerStyle.Vive:
                case ControllerStyle.LogitechPen:
                    Materials.Assign(PadMesh, Materials.TutorialPad);
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    Materials.Assign(JoystickPad, Materials.Blank);
                    Materials.Assign(Button01Mesh, Materials.Blank);
                    Materials.Assign(Button02Mesh, Materials.Blank);
                    Materials.Assign(JoystickMesh, Materials.Blank);
                    break;
                case ControllerStyle.Wmr:
                    Materials.Assign(PadMesh, Materials.Standard);
                    Materials.Assign(JoystickMesh, Materials.Blank);
                    Materials.Assign(JoystickPad, Materials.Blank);
                    break;
            }
        }

        // Toggles the little auxillary "lock" icon for snapping.
        // Enabled indicates if the icon should be shown at all, where snappingOn indicates whether the
        // open lock or closed lock icon should be shown.
        public void TogglePadSnapHint(bool snappingOn, bool enabled)
        {
            Material padMat = snappingOn ? Materials.SnapOn : Materials.SnapOff;
            switch (Style)
            {
                case ControllerStyle.Vive:
                case ControllerStyle.Wmr:
                case ControllerStyle.LogitechPen:
                    Materials.Assign(PadMesh, enabled ? padMat : Materials.Standard);
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    Materials.Assign(Button01Mesh, enabled ? padMat : Materials.Blank);
                    break;
            }
        }

        // Shows a single cancel button, e.g. SaveIconTool snapshot.
        public void ToggleCancelOnly(bool enabled, bool enableFillTimer = true)
        {
            Material mat = enabled ? Materials.Cancel : Materials.Blank;
            float ratio = 0f;
            switch (Style)
            {
                case ControllerStyle.Vive:
                case ControllerStyle.Wmr:
                case ControllerStyle.LogitechPen:
                    Materials.Assign(PadMesh, mat);
                    if (enableFillTimer)
                    {
                        ratio = GetPadRatio(VrInput.Directional);
                    }
                    PadMesh.material.SetFloat("_Ratio", ratio);
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    Materials.Assign(Button01Mesh, mat);
                    if (enableFillTimer)
                    {
                        ratio = GetPadRatio(VrInput.Button01);
                    }
                    Button01Mesh.material.SetFloat("_Ratio", ratio);
                    break;
            }
        }

        // Toggles the little auxillary "lock" icon for snapping.
        // Enabled indicates if the icon should be shown at all, where snappingOn indicates whether the
        // open lock or closed lock icon should be shown.
        public void TogglePadLazyInputHint(bool lazyInput, bool altMode, bool enabled)
        {
            Material padMat = lazyInput ? altMode ? Materials.LazyInputAltOn : Materials.LazyInputOn : Materials.LazyInputOff;
            switch (Style)
            {
                case ControllerStyle.Vive:
                case ControllerStyle.Wmr:
                case ControllerStyle.LogitechPen:
                    Materials.Assign(PadMesh, enabled ? padMat : Materials.Standard);
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    Materials.Assign(Button01Mesh, enabled ? padMat : Materials.Blank);
                    break;
            }
        }


        // Toggles the little auxillary "lock" icon for snapping.
        // Enabled indicates if the icon should be shown at all, where snappingOn indicates whether the
        // open lock or closed lock icon should be shown.
        public void TogglePadRevolverHint(bool revolverActive, bool enabled)
        {
            Material padMat = revolverActive ? Materials.RevolverRadius : Materials.WorldTransformReset;
            switch (Style)
            {
                case ControllerStyle.Vive:
                case ControllerStyle.Wmr:
                case ControllerStyle.LogitechPen:
                    Materials.Assign(PadMesh, enabled ? padMat : Materials.Standard);
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    Materials.Assign(Button01Mesh, enabled ? padMat : Materials.Blank);
                    break;
            }
        }

        // Toggles the little auxillary "lock" icon for snapping.
        // Enabled indicates if the icon should be shown at all, where snappingOn indicates whether the
        // open lock or closed lock icon should be shown.
        public void ShowWorldTransformReset()
        {

            Material padMat = App.Scene.disableTiltProtection ? Materials.SnapOff : Materials.SnapOn;
            switch (Style)
            {
                case ControllerStyle.Vive:
                case ControllerStyle.Wmr:
                case ControllerStyle.LogitechPen:
                    bool undoHover = InputManager.Wand.GetPadValue().x < 0.0f;
                    padMat = Materials.WorldTransformReset;
                    Materials.Assign(PadMesh, padMat);
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    Materials.Assign(Button02Mesh, padMat);
                    Materials.Assign(Button01Mesh, Materials.WorldTransformReset);
                    break;
            }
        }

        public void ShowBrushSizer()
        {
            float ratio = GetPadRatio(VrInput.Directional);
            switch (Style)
            {
                case ControllerStyle.LogitechPen:
                    Materials.Assign(PadMesh, SelectPadTouched(
                        Materials.BrushSizerActive_LogitechPen, Materials.BrushSizer_LogitechPen));
                    PadMesh.material.SetFloat("_Ratio", ratio);
                    break;
                case ControllerStyle.Vive:
                    Materials.Assign(PadMesh, SelectPadTouched(Materials.BrushSizerActive, Materials.BrushSizer));
                    PadMesh.material.SetFloat("_Ratio", ratio);
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    Materials.Assign(JoystickPad,
                        SelectThumbStickTouched(Materials.BrushSizerActive, Materials.BrushSizer));
                    Materials.Assign(JoystickMesh,
                        SelectThumbStickTouched(Materials.Blank, Materials.BrushSizer));
                    JoystickPad.material.SetFloat("_Ratio", ratio);
                    break;
                case ControllerStyle.Wmr:
                    Materials.Assign(JoystickMesh,
                        SelectThumbStickTouched(Materials.BrushSizerActive, Materials.BrushSizer));
                    JoystickMesh.material.SetFloat("_Ratio", ratio);
                    break;
            }
        }

        public void ShowSelectionToggle()
        {
            Material toggleSelectionMat = Materials.Blank;
            if (!SelectionManager.m_Instance.SelectionToolIsHot &&
                (SelectionManager.m_Instance.HasSelection ||
                SelectionManager.m_Instance.ShouldRemoveFromSelection()))
            {
                toggleSelectionMat = SelectionManager.m_Instance.ShouldRemoveFromSelection() ?
                    Materials.ToggleSelectionOn : Materials.ToggleSelectionOff;
            }

            switch (Style)
            {
                case ControllerStyle.Vive:
                case ControllerStyle.LogitechPen:
                    Materials.Assign(PadMesh, toggleSelectionMat);
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    Materials.Assign(JoystickPad, SelectPadTouched(Materials.BrushSizerActive, Materials.BrushSizer));
                    Materials.Assign(JoystickMesh, SelectPadTouched(Materials.Blank, Materials.BrushSizer));
                    float ratio = GetPadRatio(VrInput.Directional);
                    JoystickPad.material.SetFloat("_Ratio", ratio);
                    Materials.Assign(Button01Mesh, toggleSelectionMat);
                    break;
                case ControllerStyle.Wmr:
                    Materials.Assign(PadMesh, toggleSelectionMat);
                    ShowBrushSizer();
                    break;
            }
        }

        public void ShowPinToggle(bool isPinning)
        {
            Material togglePinMat = Materials.Blank;
            PinTool pinTool = SketchSurfacePanel.m_Instance.ActiveTool as PinTool;
            if (pinTool != null)
            {
                if (pinTool.CanToggle())
                {
                    togglePinMat = pinTool.InPinMode ? Materials.ToggleSelectionOff :
                        Materials.ToggleSelectionOn;
                }
            }

            switch (Style)
            {
                case ControllerStyle.Vive:
                case ControllerStyle.LogitechPen:
                    Materials.Assign(PadMesh, togglePinMat);
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    Materials.Assign(JoystickPad, SelectPadTouched(Materials.BrushSizerActive, Materials.BrushSizer));
                    Materials.Assign(JoystickMesh, SelectPadTouched(Materials.Blank, Materials.BrushSizer));
                    float ratio = GetPadRatio(VrInput.Directional);
                    JoystickPad.material.SetFloat("_Ratio", ratio);
                    Materials.Assign(Button01Mesh, togglePinMat);
                    break;
                case ControllerStyle.Wmr:
                    Materials.Assign(PadMesh, togglePinMat);
                    ShowBrushSizer();
                    break;
            }
        }

        public void ShowDuplicateOption()
        {
            switch (Style)
            {
                case ControllerStyle.Vive:
                case ControllerStyle.Wmr:
                case ControllerStyle.LogitechPen:
                    Materials.Assign(PadMesh, Materials.SelectionOptions);
                    PadMesh.material.SetFloat("_Ratio", GetPadRatio(VrInput.Button04));
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    Materials.Assign(Button01Mesh, Materials.SelectionOptions);
                    Button01Mesh.material.SetFloat("_Ratio",
                        GetPadRatio(VrInput.Button01));
                    break;
            }
        }

        public void ShowSelectionOptions()
        {
            switch (Style)
            {
                case ControllerStyle.Vive:
                case ControllerStyle.Wmr:
                case ControllerStyle.LogitechPen:
                    Materials.Assign(PadMesh, ControllerMaterialCatalog.m_Instance.SelectionOptions);
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    Materials.Assign(Button01Mesh, ControllerMaterialCatalog.m_Instance.SelectionOptions);
                    break;
            }
        }

        public void ToggleTrash(bool enabled)
        {
            float ratio = GetPadRatio(VrInput.Button04);
            switch (Style)
            {
                case ControllerStyle.Vive:
                case ControllerStyle.Wmr:
                case ControllerStyle.LogitechPen:
                    if (enabled)
                    {
                        Materials.Assign(PadMesh, Materials.Trash);
                        PadMesh.material.SetFloat("_Ratio", ratio);
                    }
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    Materials.Assign(Button01Mesh, enabled ? Materials.Trash : Materials.Blank);
                    Button01Mesh.material.SetFloat("_Ratio", ratio);
                    break;
            }
        }

        // Shows the swipe gesture hint to rotate the multi-cam.
        // The enableHint param enables an additional swipe hint shown before the user touches the pad.
        public void ShowMulticamSwipe(bool showHint)
        {
            Material padMat = SelectPadTouched(Materials.MulticamActive, Materials.Multicam);
            float ratio = GetPadRatio(VrInput.Directional);

            switch (Style)
            {
                case ControllerStyle.Vive:
                case ControllerStyle.LogitechPen:
                    // Show the camera on the "joystick mesh" only when not active.
                    // When the thumb IS on the joystick, show the hint if requested, else the active camera icon.
                    padMat = showHint ? Materials.MulticamSwipeHint : padMat;
                    Materials.Assign(PadMesh, padMat);
                    PadMesh.material.SetFloat("_Ratio", ratio);
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    // Show the camera on the "joystick mesh" only when not active.
                    // When the thumb IS on the joystick, show the hint if requested, else the active camera icon.
                    Material hint = SelectPadTouched(Materials.Blank,
                        showHint ? Materials.MulticamSwipeHint : padMat);
                    Materials.Assign(JoystickMesh, hint);
                    Materials.Assign(JoystickPad, padMat);
                    JoystickMesh.material.SetFloat("_Ratio", ratio);
                    JoystickPad.material.SetFloat("_Ratio", ratio);
                    break;
                case ControllerStyle.Wmr:
                    // Wmr does not has the capability to detect touch on thumbstick.
                    padMat = showHint ? Materials.MulticamSwipeHint : Materials.MulticamActive;
                    Materials.Assign(JoystickMesh, padMat);
                    JoystickMesh.material.SetFloat("_Ratio", ratio);
                    break;
            }
        }

        // Show nothing while user is capturing.
        public void ShowCapturingVideo()
        {
            switch (Style)
            {
                case ControllerStyle.Vive:
                case ControllerStyle.Wmr:
                case ControllerStyle.LogitechPen:
                    Materials.Assign(PadMesh, Materials.Blank);
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    Materials.Assign(Button01Mesh, Materials.Blank);
                    break;
            }
        }

        // Show video sharing icon.
        public void ShowShareVideo()
        {
            float ratio = GetPadRatio(VrInput.Button04);

            switch (Style)
            {
                case ControllerStyle.LogitechPen:
                    UpdateLogitechPadHandedness(PadMesh.transform);
                    Material active = m_LogitechPenHandedness ?
                        Materials.ShareYtActive : Materials.ShareYtActive_Rot180;
                    Materials.Assign(PadMesh, SelectPadTouched(active, Materials.ShareYt));
                    PadMesh.material.SetFloat("_Ratio", ratio);
                    break;
                case ControllerStyle.Vive:
                case ControllerStyle.Wmr:
                    Materials.Assign(PadMesh, SelectPadTouched(Materials.ShareYtActive, Materials.ShareYt));
                    PadMesh.material.SetFloat("_Ratio", ratio);
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    Materials.Assign(Button01Mesh, SelectIfTouched(VrInput.Button01,
                        Materials.ShareYtActive, Materials.ShareYt));

                    // The button is animated when the user holds it down.
                    Button01Mesh.material.SetFloat("_Ratio", ratio);
                    break;
            }
        }

        // Show "share or cancel" icons.
        public void ShowShareOrCancel()
        {
            switch (Style)
            {
                case ControllerStyle.LogitechPen:
                    {
                        float padValue = InputManager.Brush.GetPadValue().y;
                        Material cancelAnimate = Materials.Cancel;
                        Material shareAnimate = Materials.Yes;
                        Material cancel = Materials.YesOrCancel_Cancel;
                        Material share = Materials.YesOrCancel_Yes;
                        Material padMat = Materials.YesOrCancel;

                        UpdateLogitechPadHandedness(PadMesh.transform);
                        if (!m_LogitechPenHandedness)
                        {
                            cancelAnimate = Materials.Cancel_Rot180;
                            shareAnimate = Materials.Yes_Rot180;
                            cancel = Materials.YesOrCancel_Cancel_Rot180;
                            share = Materials.YesOrCancel_Yes_Rot180;
                            padMat = Materials.YesOrCancel_Rot180;
                        }

                        // Buzz if switched from right to left
                        int selected = (int)Mathf.Sign(padValue);
                        if (m_LastPadButton != selected)
                        {
                            InputManager.m_Instance.TriggerHapticsPulse(InputManager.ControllerName.Brush,
                                2, 0.15f, 0.1f);
                        }
                        m_LastPadButton = selected;

                        if (padValue > 0f)
                        {
                            padMat = cancel;
                        }
                        else if (padValue < 0f)
                        {
                            padMat = share;
                        }

                        padMat = SelectPadTouched(padValue > 0f ? cancel : share, padMat);
                        padMat = SelectBasedOn(padValue > 0f ? cancelAnimate : shareAnimate, padMat);
                        Materials.Assign(PadMesh, padMat);

                        // We use padValueRaw here because VrInput.Button01 and 02 don't case about
                        // handedness.
                        PadMesh.material.SetFloat("_Ratio",
                            GetPadRatio(padValue > 0f ? VrInput.Button02 : VrInput.Button01));
                    }
                    break;
                case ControllerStyle.Vive:
                case ControllerStyle.Wmr:
                    {
                        Material padMat = Materials.YesOrCancel;
                        float padX = InputManager.Brush.GetPadValue().x;

                        // Buzz if switched from right to left
                        int selected = (int)Mathf.Sign(padX);
                        if (m_LastPadButton != selected)
                        {
                            InputManager.m_Instance.TriggerHapticsPulse(InputManager.ControllerName.Brush,
                                2, 0.15f, 0.1f);
                        }
                        m_LastPadButton = selected;

                        if (padX > 0f)
                        {
                            padMat = Materials.YesOrCancel_Cancel;
                        }
                        else if (padX < 0f)
                        {
                            padMat = Materials.YesOrCancel_Yes;
                        }

                        padMat = SelectPadTouched(padX > 0f ? Materials.YesOrCancel_Cancel
                                : Materials.YesOrCancel_Yes,
                            padMat);

                        padMat = SelectBasedOn(padX > 0f ? Materials.Cancel : Materials.Yes, padMat);
                        Materials.Assign(PadMesh, padMat);
                        PadMesh.material.SetFloat("_Ratio",
                            GetPadRatio(padX > 0f ? VrInput.Button02 : VrInput.Button01));
                    }
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    Materials.Assign(Button01Mesh, Materials.Yes);
                    Materials.Assign(Button02Mesh, Materials.Cancel);

                    Button01Mesh.material.SetFloat("_Ratio",
                        GetPadRatio(VrInput.Button01));
                    Button02Mesh.material.SetFloat("_Ratio",
                        GetPadRatio(VrInput.Button02));
                    break;
            }
        }

        // Show the rotation icons to indicate that the wand panels can be rotated.
        public void ShowRotatePanels()
        {
            Material mat = Materials.PanelsRotate;
            switch (Style)
            {
                case ControllerStyle.Vive:
                case ControllerStyle.LogitechPen:
                    Materials.Assign(PadMesh, mat);
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    Materials.Assign(JoystickMesh, SelectThumbStickTouched(Materials.Blank, mat));
                    Materials.Assign(JoystickPad, mat);
                    break;
                case ControllerStyle.Wmr:
                    // Wmr does not has the capability to detect touch on thumbstick.
                    Materials.Assign(JoystickMesh, mat);
                    break;
            }
        }

        // Show the undo/redo icons, conditionally based on undo availability.
        public void ShowUndoRedo(bool canUndo, bool canRedo)
        {
            switch (Style)
            {
                case ControllerStyle.Vive:
                case ControllerStyle.Wmr:
                case ControllerStyle.LogitechPen:
                    bool redoHover = InputManager.Wand.GetPadValue().x > 0.0f;
                    bool undoHover = InputManager.Wand.GetPadValue().x < 0.0f;

                    Material mat = Materials.UndoRedo;
                    if (redoHover && canRedo)
                    {
                        mat = Materials.UndoRedo_Redo;
                    }
                    else if (undoHover && canUndo)
                    {
                        mat = Materials.UndoRedo_Undo;
                    }

                    // Only show Undo/Redo on Vive if the pad is touched.
                    Material defaultMat = (Style == ControllerStyle.Wmr) ?
                        Materials.UndoRedo : PadMesh.material;
                    Materials.Assign(PadMesh, SelectPadTouched(mat, defaultMat));
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    if (canUndo)
                    {
                        Materials.Assign(Button01Mesh, Materials.Undo);
                    }

                    if (canRedo)
                    {
                        Materials.Assign(Button02Mesh, Materials.Redo);
                    }
                    break;
            }
        }

        // Show the "pin cushion" icon.
        public void ShowPinCushion()
        {
            switch (Style)
            {
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    Materials.Assign(Button02Mesh, Materials.PinCushion);
                    break;
                case ControllerStyle.Wmr:
                    Materials.Assign(PinCushionMesh, Materials.PinCushion);
                    break;
            }
        }

        // Show the current brush page number, e.g. while interacting with the brush page panel.
        public void ShowBrushPage(bool active)
        {
            Material mat = active ? Materials.BrushPageActive : Materials.BrushPage;
            float ratio = GetPadRatio(VrInput.Directional);

            switch (Style)
            {
                case ControllerStyle.LogitechPen:
                    mat = active ? Materials.BrushPageActive_LogitechPen : Materials.BrushPage;
                    Materials.Assign(PadMesh, mat);
                    PadMesh.material.SetFloat("_Ratio", ratio);
                    break;
                case ControllerStyle.Vive:
                    Materials.Assign(PadMesh, mat);
                    PadMesh.material.SetFloat("_Ratio", ratio);
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    Materials.Assign(JoystickMesh, SelectPadTouched(Materials.Blank, mat));
                    Materials.Assign(JoystickPad, mat);
                    JoystickPad.material.SetFloat("_Ratio", ratio);
                    JoystickMesh.material.SetFloat("_Ratio", ratio);
                    break;
                case ControllerStyle.Wmr:
                    Materials.Assign(JoystickMesh, mat);
                    JoystickMesh.material.SetFloat("_Ratio", ratio);
                    break;
            }
        }

        // Show the Slider value when interacting with advanced sliders
        public void ShowSliderValue(bool active, float value)
        {
            Material mat = active ? Materials.ChangeSliderValueActive : Materials.ChangeSliderValue;

            switch (Style)
            {
                case ControllerStyle.LogitechPen:
                    mat = active ? Materials.BrushPageActive_LogitechPen : Materials.BrushPage;
                    Materials.Assign(PadMesh, mat);
                    PadMesh.material.SetFloat("_Ratio", value);
                    break;
                case ControllerStyle.Vive:
                    Materials.Assign(PadMesh, mat);
                    PadMesh.material.SetFloat("_Ratio", value);
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Knuckles:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                    Materials.Assign(JoystickMesh, SelectPadTouched(Materials.Blank, mat));
                    Materials.Assign(JoystickPad, mat);
                    JoystickPad.material.SetFloat("_Ratio", value);
                    JoystickMesh.material.SetFloat("_Ratio", value);
                    break;
                case ControllerStyle.Wmr:
                    Materials.Assign(JoystickMesh, mat);
                    JoystickMesh.material.SetFloat("_Ratio", value);
                    break;
            }
        }

        // Resets all materials to the default state.
        public void ResetAll()
        {
            PadEnabled = true;

            switch (Style)
            {
                case ControllerStyle.Vive:
                    Materials.Assign(PadMesh, Materials.Standard);
                    break;
                case ControllerStyle.LogitechPen:
                    Materials.Assign(PadMesh, Materials.Blank);
                    break;
                case ControllerStyle.OculusTouch:
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                case ControllerStyle.Zapbox:
                case ControllerStyle.Knuckles:
                    Materials.Assign(JoystickMesh, Materials.Blank);
                    Materials.Assign(JoystickPad, Materials.Blank);
                    Materials.Assign(Button01Mesh, Materials.Blank);
                    Materials.Assign(Button02Mesh, Materials.Blank);
                    break;
                case ControllerStyle.Wmr:
                    Materials.Assign(PadMesh, Materials.Standard);
                    Materials.Assign(JoystickMesh, Materials.Blank);
                    Materials.Assign(PinCushionMesh, Materials.Blank);
                    break;
            }
        }
    }
} // namespace TiltBrush
