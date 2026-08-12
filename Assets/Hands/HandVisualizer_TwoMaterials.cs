using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Hands;

namespace TiltBrush
{
    /// <summary>
    /// Displays tracked hand meshes using a depth-only material followed by a
    /// transparent material. The hand models come from Unity's XR Hands Hand
    /// Visualizer sample; debug-joint and velocity visualization are intentionally
    /// omitted.
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

        private XRHandSubsystem m_Subsystem;
        private HandGameObject m_LeftHand;
        private HandGameObject m_RightHand;
        private bool m_PreviousDrawMeshes;

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
            SetMeshVisibility(m_DrawMeshes);
        }

        private void OnDisable()
        {
            SetMeshVisibility(false);
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
                return;
            }

            EnsureHandObjects();

            if (m_PreviousDrawMeshes != m_DrawMeshes)
            {
                SetMeshVisibility(m_DrawMeshes);
            }
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

            SetMeshVisibility(m_DrawMeshes);
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

        private void SetMeshVisibility(bool visible)
        {
            m_LeftHand?.SetVisible(visible);
            m_RightHand?.SetVisible(visible);
            m_PreviousDrawMeshes = visible;
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
                {
                    return;
                }

                m_MeshController.enabled = visible;
                if (!visible && m_MeshController.handMeshRenderer != null)
                {
                    m_MeshController.handMeshRenderer.enabled = false;
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
                SkinnedMeshRenderer renderer,
                Material depthMaterial,
                Material transparentMaterial)
            {
                if (renderer == null)
                {
                    return;
                }

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
