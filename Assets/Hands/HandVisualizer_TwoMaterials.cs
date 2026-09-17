using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Hands;

namespace TiltBrush
{
    /// <summary>
    /// Displays fully articulated XR Hands meshes, while AndroidXRHandBridge
    /// remains responsible for high-level pinch/poke/grasp input.
    ///
    /// This deliberately does NOT request Android permissions. For development,
    /// install the APK with adb -g (or grant HAND_TRACKING separately) before launch.
    ///
    /// Visibility is hybrid per side:
    /// - hand source active on a side  -> show that articulated hand
    /// - physical controller active    -> hide that hand
    /// </summary>
    public class HandVisualizer_TwoMaterials : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Enable the Input System's optimized controls feature flag.")]
        private bool m_UseOptimizedControls;

        [SerializeField, FormerlySerializedAs("m_LeftHandMesh")]
        private GameObject m_MetaQuestLeftHandMesh;

        [SerializeField, FormerlySerializedAs("m_RightHandMesh")]
        private GameObject m_MetaQuestRightHandMesh;

        [SerializeField]
        private GameObject m_AndroidXRLeftHandMesh;

        [SerializeField]
        private GameObject m_AndroidXRRightHandMesh;

        [SerializeField]
        [Tooltip("Material that writes hand depth without writing color.")]
        private Material m_HandMeshDepthMaterial;

        [SerializeField]
        [Tooltip("Transparent hand material drawn after the depth material.")]
        private Material m_HandMeshTransparentMaterial;

        [SerializeField]
        private bool m_DrawMeshes = true;

        [Header("Hybrid source visibility")]
        [SerializeField]
        [Tooltip("Hide a hand mesh whenever AndroidXRHandBridge routes that side to a physical controller.")]
        private bool m_FollowHybridSourceSelection = true;

        [Header("Debug")]
        [SerializeField]
        private bool m_DebugLogging = true;

        [SerializeField]
        [Min(0.1f)]
        private float m_DebugInterval = 1.0f;

        private XRHandSubsystem m_Subsystem;
        private HandGameObject m_LeftHand;
        private HandGameObject m_RightHand;

        private float m_NextDebugTime;
        private bool m_LastLeftVisible;
        private bool m_LastRightVisible;

        private static readonly List<XRHandSubsystem> s_Subsystems = new();

        public bool DrawMeshes
        {
            get => m_DrawMeshes;
            set => m_DrawMeshes = value;
        }

        private void Awake()
        {
#if ENABLE_INPUT_SYSTEM
            if (m_UseOptimizedControls)
            {
                UnityEngine.InputSystem.InputSystem.settings.SetInternalFeatureFlag(
                    "USE_OPTIMIZED_CONTROLS",
                    true);
            }
#endif
        }

        private void OnEnable()
        {
            FindRunningSubsystem();
            RefreshVisibility(force: true);
        }

        private void OnDisable()
        {
            SetBothVisible(false);
            m_Subsystem = null;
        }

        private void OnDestroy()
        {
            m_LeftHand?.Destroy();
            m_RightHand?.Destroy();
            m_LeftHand = null;
            m_RightHand = null;
        }

        private void Update()
        {
            if (m_Subsystem == null || !m_Subsystem.running)
            {
                FindRunningSubsystem();
            }

            if (m_Subsystem == null || !m_Subsystem.running)
            {
                SetBothVisible(false);
                DebugStateIfNeeded();
                return;
            }

            EnsureHandObjects();
            RefreshVisibility(force: false);
            DebugStateIfNeeded();
        }

        private void FindRunningSubsystem()
        {
            m_Subsystem = null;
            s_Subsystems.Clear();
            SubsystemManager.GetSubsystems(s_Subsystems);

            foreach (XRHandSubsystem subsystem in s_Subsystems)
            {
                if (subsystem != null && subsystem.running)
                {
                    m_Subsystem = subsystem;

                    if (m_DebugLogging)
                    {
                        Debug.Log(
                            "ANDROIDXR_HAND_VIS: Found running XRHandSubsystem. " +
                            $"layout={subsystem.detectedHandMeshLayout}",
                            this);
                    }

                    return;
                }
            }
        }

        private void EnsureHandObjects()
        {
            if (m_LeftHand != null && m_RightHand != null)
            {
                return;
            }

            bool useAndroidXRMeshes =
                m_Subsystem.detectedHandMeshLayout == XRDetectedHandMeshLayout.OpenXRAndroidXR;

            GameObject leftMesh = useAndroidXRMeshes
                ? m_AndroidXRLeftHandMesh
                : m_MetaQuestLeftHandMesh;

            GameObject rightMesh = useAndroidXRMeshes
                ? m_AndroidXRRightHandMesh
                : m_MetaQuestRightHandMesh;

            m_LeftHand ??= CreateHand(Handedness.Left, leftMesh);
            m_RightHand ??= CreateHand(Handedness.Right, rightMesh);

            RefreshVisibility(force: true);
        }

        private HandGameObject CreateHand(Handedness handedness, GameObject meshPrefab)
        {
            if (meshPrefab == null)
            {
                Debug.LogError($"No {handedness} hand mesh is configured.", this);
                return null;
            }

            return new HandGameObject(
                handedness,
                transform,
                meshPrefab,
                m_HandMeshDepthMaterial,
                m_HandMeshTransparentMaterial);
        }

        private void RefreshVisibility(bool force)
        {
            bool leftTracked =
                m_Subsystem != null &&
                m_Subsystem.running &&
                m_Subsystem.leftHand.isTracked;

            bool rightTracked =
                m_Subsystem != null &&
                m_Subsystem.running &&
                m_Subsystem.rightHand.isTracked;

            bool leftSelectedAsHand = true;
            bool rightSelectedAsHand = true;

            if (m_FollowHybridSourceSelection && AndroidXRHandBridge.Active)
            {
                leftSelectedAsHand = AndroidXRHandBridge.UseHand(false);
                rightSelectedAsHand = AndroidXRHandBridge.UseHand(true);
            }

            bool leftVisible =
                m_DrawMeshes &&
                leftTracked &&
                leftSelectedAsHand;

            bool rightVisible =
                m_DrawMeshes &&
                rightTracked &&
                rightSelectedAsHand;

            if (force || leftVisible != m_LastLeftVisible)
            {
                m_LeftHand?.SetVisible(leftVisible);
                m_LastLeftVisible = leftVisible;
            }

            if (force || rightVisible != m_LastRightVisible)
            {
                m_RightHand?.SetVisible(rightVisible);
                m_LastRightVisible = rightVisible;
            }
        }

        private void SetBothVisible(bool visible)
        {
            m_LeftHand?.SetVisible(visible);
            m_RightHand?.SetVisible(visible);
            m_LastLeftVisible = visible;
            m_LastRightVisible = visible;
        }

        private void DebugStateIfNeeded()
        {
            if (!m_DebugLogging || Time.unscaledTime < m_NextDebugTime)
                return;

            m_NextDebugTime =
                Time.unscaledTime + Mathf.Max(0.1f, m_DebugInterval);

            bool running = m_Subsystem != null && m_Subsystem.running;
            bool leftTracked = running && m_Subsystem.leftHand.isTracked;
            bool rightTracked = running && m_Subsystem.rightHand.isTracked;

            bool bridgeActive = AndroidXRHandBridge.Active;
            bool sourceLeftHand =
                !bridgeActive || AndroidXRHandBridge.UseHand(false);
            bool sourceRightHand =
                !bridgeActive || AndroidXRHandBridge.UseHand(true);

            Debug.Log(
                "ANDROIDXR_HAND_VIS STATE " +
                $"subsystem={m_Subsystem != null} " +
                $"running={running} " +
                $"trackedL={leftTracked} " +
                $"trackedR={rightTracked} " +
                $"bridge={bridgeActive} " +
                $"useHandL={sourceLeftHand} " +
                $"useHandR={sourceRightHand} " +
                $"visibleL={m_LastLeftVisible} " +
                $"visibleR={m_LastRightVisible}",
                this);
        }

        private sealed class HandGameObject
        {
            private GameObject m_Root;
            private readonly XRHandMeshController m_MeshController;

            public HandGameObject(
                Handedness handedness,
                Transform parent,
                GameObject meshPrefab,
                Material depthMaterial,
                Material transparentMaterial)
            {
                bool isSceneObject = meshPrefab.scene.IsValid();

                m_Root = isSceneObject
                    ? meshPrefab
                    : Instantiate(meshPrefab, parent);

                m_Root.SetActive(false);
                m_Root.transform.SetLocalPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);

                XRHandTrackingEvents handEvents =
                    m_Root.GetComponent<XRHandTrackingEvents>();

                if (handEvents == null)
                {
                    handEvents = m_Root.AddComponent<XRHandTrackingEvents>();
                    handEvents.updateType = XRHandTrackingEvents.UpdateTypes.Dynamic;
                    handEvents.handedness = handedness;
                }

                m_MeshController = m_Root.GetComponent<XRHandMeshController>();

                if (m_MeshController == null)
                {
                    m_MeshController = m_Root.AddComponent<XRHandMeshController>();
                }

                if (m_MeshController.handMeshRenderer == null)
                {
                    m_MeshController.handMeshRenderer =
                        m_Root.GetComponentInChildren<SkinnedMeshRenderer>(true);
                }

                m_MeshController.handTrackingEvents = handEvents;

                AssignMaterials(
                    m_MeshController.handMeshRenderer,
                    depthMaterial,
                    transparentMaterial);

                XRHandSkeletonDriver skeletonDriver =
                    m_Root.GetComponent<XRHandSkeletonDriver>();

                if (skeletonDriver == null)
                {
                    skeletonDriver = m_Root.AddComponent<XRHandSkeletonDriver>();
                    skeletonDriver.jointTransformReferences =
                        new List<JointToTransformReference>();

                    foreach (Transform child in m_Root.transform)
                    {
                        if (child.name.EndsWith(XRHandJointID.Wrist.ToString()))
                        {
                            skeletonDriver.rootTransform = child;
                            break;
                        }
                    }

                    XRHandSkeletonDriverUtility.FindJointsFromRoot(skeletonDriver);
                    skeletonDriver.InitializeFromSerializedReferences();
                }

                skeletonDriver.handTrackingEvents = handEvents;
                m_Root.SetActive(true);
            }

            public void SetVisible(bool visible)
            {
                if (m_MeshController == null)
                    return;

                // Keep the controller enabled while visible so it continuously
                // receives XRHandTrackingEvents and deforms the skinned mesh.
                m_MeshController.enabled = visible;

                if (m_MeshController.handMeshRenderer != null)
                {
                    m_MeshController.handMeshRenderer.enabled = visible;
                }
            }

            public void Destroy()
            {
                if (m_Root != null)
                {
                    Object.Destroy(m_Root);
                    m_Root = null;
                }
            }

            private static void AssignMaterials(
                Renderer renderer,
                Material depthMaterial,
                Material transparentMaterial)
            {
                if (renderer == null)
                    return;

                if (depthMaterial != null && transparentMaterial != null)
                {
                    renderer.sharedMaterials = new[]
                    {
                        depthMaterial,
                        transparentMaterial
                    };
                }
                else if (depthMaterial != null)
                {
                    renderer.sharedMaterial = depthMaterial;
                }
                else if (transparentMaterial != null)
                {
                    renderer.sharedMaterial = transparentMaterial;
                }
            }
        }
    }
}
