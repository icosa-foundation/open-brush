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
using System.Linq;
using IsoMesh;
using UnityEngine;

namespace TiltBrush
{
    /// A normal, axis-scalable guide backed by one analytic IsoMesh primitive.
    /// Used for guide shapes that do not have a legacy mesh/collider implementation.
    public sealed class SdfPrimitiveStencil : SdfStencil
    {
        private const float k_MinimumAspectRatio = 0.05f;
        private const float k_TorusMaximumTubeRatio = 0.9f;
        private const float k_BoxFrameThicknessRatio = 0.08f;

        [SerializeField] private StencilType m_PrimitiveGuideType = StencilType.Cylinder;
        [SerializeField] private SDFPrimitiveType m_PrimitiveType = SDFPrimitiveType.Cylinder;
        [SerializeField] private Vector3 m_AspectRatio = Vector3.one;

        public override Vector3 Extents
        {
            get => m_Size * m_AspectRatio;
            set
            {
                ValidateExtents(value);
                m_Size = 1f;
                m_AspectRatio = value;
                UpdateScale();
            }
        }

        public override Vector3 CustomDimension
        {
            get => m_AspectRatio;
            set
            {
                ValidateExtents(value);
                m_AspectRatio = value;
                UpdateScale();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            ValidateConfiguration();
            m_Type = m_PrimitiveGuideType;
            ReplacePrimitives(new[]
            {
                new PrimitiveDefinition(
                    m_PrimitiveType, GeometryFromAspectRatio(), TrTransform.identity,
                    SDFCombineType.SmoothUnion, 0f, false),
            });
            UpdateScale();
        }

        protected override void UpdateScale()
        {
            float maximumAspect = m_AspectRatio.Max();
            if (maximumAspect <= Mathf.Epsilon)
            {
                m_AspectRatio = Vector3.one;
                maximumAspect = 1f;
            }
            m_AspectRatio /= maximumAspect;
            m_Size *= maximumAspect;

            if (UsesRadialDimensions)
            {
                float radialAspect = Mathf.Max(
                    k_MinimumAspectRatio, Mathf.Max(m_AspectRatio.x, m_AspectRatio.z));
                float heightAspect = Mathf.Max(k_MinimumAspectRatio, m_AspectRatio.y);
                if (m_PrimitiveType == SDFPrimitiveType.Torus)
                {
                    heightAspect = Mathf.Min(heightAspect, radialAspect * k_TorusMaximumTubeRatio);
                }
                m_AspectRatio = new Vector3(radialAspect, heightAspect, radialAspect);
            }
            else
            {
                m_AspectRatio = new Vector3(
                    Mathf.Max(k_MinimumAspectRatio, m_AspectRatio.x),
                    Mathf.Max(k_MinimumAspectRatio, m_AspectRatio.y),
                    Mathf.Max(k_MinimumAspectRatio, m_AspectRatio.z));
            }
            transform.localScale = Vector3.one * m_Size;

            if (PrimitiveCount > 0)
            {
                SetComponentPrimitiveGeometry(0, GeometryFromAspectRatio());
            }
            UpdateMaterialScale();
        }

        public override GrabWidget Clone(Vector3 position, Quaternion rotation, float size)
        {
            SdfPrimitiveStencil clone = Instantiate(
                WidgetManager.m_Instance.GetStencilPrefab(Type)) as SdfPrimitiveStencil;
            if (clone == null)
            {
                throw new InvalidOperationException(
                    $"Guide type {Type} is not mapped to an SDF primitive guide prefab.");
            }
            clone.m_PreviousCanvas = m_PreviousCanvas;
            clone.transform.position = position;
            clone.transform.rotation = rotation;
            clone.m_SkipIntroAnim = true;
            clone.m_ShowTimer = clone.m_ShowDuration;
            clone.transform.parent = transform.parent;
            clone.Show(true, false);
            clone.SetSignedWidgetSize(size);
            clone.CloneInitialMaterials(this);
            clone.Extents = Extents;
            HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);
            return clone;
        }

        protected override Axis GetInferredManipulationAxis(
            Vector3 primaryHand, Vector3 secondaryHand, bool secondaryHandInside)
        {
            if (secondaryHandInside)
            {
                return Axis.Invalid;
            }
            if (UsesRadialDimensions)
            {
                Vector3 secondary_OS = transform.InverseTransformPoint(secondaryHand);
                float radialDistance = new Vector2(secondary_OS.x, secondary_OS.z).magnitude;
                return Mathf.Abs(secondary_OS.y) > radialDistance ? Axis.Y : Axis.XZ;
            }

            Vector3 hands_OS = transform.InverseTransformDirection(primaryHand - secondaryHand);
            Vector3 absoluteHands = hands_OS.Abs();
            if (absoluteHands.x > absoluteHands.y && absoluteHands.x > absoluteHands.z)
            {
                return Axis.X;
            }
            return absoluteHands.y > absoluteHands.z ? Axis.Y : Axis.Z;
        }

        public override void RecordAndApplyScaleToAxis(float deltaScale, Axis axis)
        {
            Vector3 newDimensions = CustomDimension;
            if (UsesRadialDimensions)
            {
                switch (axis)
                {
                    case Axis.XZ:
                        newDimensions.x *= deltaScale;
                        newDimensions.z *= deltaScale;
                        break;
                    case Axis.Y:
                        newDimensions.y *= deltaScale;
                        break;
                    default:
                        throw new ArgumentException(nameof(axis));
                }
            }
            else if (axis == Axis.X || axis == Axis.Y || axis == Axis.Z)
            {
                newDimensions[(int)axis] *= deltaScale;
            }
            else
            {
                throw new ArgumentException(nameof(axis));
            }

            if (m_RecordMovements)
            {
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                    new MoveWidgetCommand(this, LocalTransform, newDimensions));
            }
            else
            {
                m_AspectRatio = newDimensions;
                UpdateScale();
            }
        }

        protected override void RegisterHighlightForSpecificAxis(Axis highlightAxis)
        {
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null)
                {
                    App.Instance.SelectionEffect.RegisterMesh(filter);
                }
            }
        }

        public override Axis GetScaleAxis(
            Vector3 handA, Vector3 handB, out Vector3 axisVec, out float extent)
        {
            Axis axis = m_LockedManipulationAxis ?? Axis.Invalid;
            float parentScale = TrTransform.FromTransform(transform.parent).scale;
            switch (axis)
            {
                case Axis.X:
                case Axis.Y:
                case Axis.Z:
                    if (UsesRadialDimensions && axis != Axis.Y)
                    {
                        throw new NotImplementedException(axis.ToString());
                    }
                    Vector3 localAxis = Vector3.zero;
                    localAxis[(int)axis] = 1f;
                    axisVec = transform.TransformDirection(localAxis);
                    extent = parentScale * Extents[(int)axis];
                    break;
                case Axis.XZ:
                    Vector3 hands = handB - handA;
                    hands -= transform.up * Vector3.Dot(transform.up, hands);
                    axisVec = hands.normalized;
                    extent = parentScale * Extents.x;
                    break;
                case Axis.Invalid:
                    axisVec = default;
                    extent = default;
                    break;
                default:
                    throw new NotImplementedException(axis.ToString());
            }
            return axis;
        }

        private Vector4 GeometryFromAspectRatio()
        {
            return GeometryForExtents(m_PrimitiveType, m_AspectRatio);
        }

        internal static Vector4 GeometryForExtents(
            SDFPrimitiveType primitiveType, Vector3 extents)
        {
            switch (primitiveType)
            {
                case SDFPrimitiveType.Cylinder:
                case SDFPrimitiveType.Cone:
                    return new Vector4(
                        0.5f * Mathf.Min(extents.x, extents.z),
                        0.5f * extents.y, 0f, 0f);
                case SDFPrimitiveType.Pyramid:
                    return new Vector4(
                        0.5f * Mathf.Max(extents.x, extents.z),
                        0.5f * extents.y, 0f, 0f);
                case SDFPrimitiveType.Torus:
                    float outerRadius = 0.5f * Mathf.Max(extents.x, extents.z);
                    float minorRadius = 0.5f * extents.y;
                    float majorRadius = Mathf.Max(
                        k_MinimumAspectRatio * 0.5f, outerRadius - minorRadius);
                    return new Vector4(majorRadius, minorRadius, 0f, 0f);
                case SDFPrimitiveType.BoxFrame:
                    float thickness = Mathf.Max(
                        k_MinimumAspectRatio * 0.5f,
                        Mathf.Min(extents.x, Mathf.Min(extents.y, extents.z)) *
                        k_BoxFrameThicknessRatio);
                    return new Vector4(
                        0.5f * extents.x, 0.5f * extents.y, 0.5f * extents.z,
                        thickness);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(primitiveType), primitiveType,
                        "Unsupported standalone SDF guide primitive.");
            }
        }

        private void ValidateConfiguration()
        {
            bool valid =
                (m_PrimitiveGuideType == StencilType.Cylinder &&
                 m_PrimitiveType == SDFPrimitiveType.Cylinder) ||
                (m_PrimitiveGuideType == StencilType.Cone &&
                 m_PrimitiveType == SDFPrimitiveType.Cone) ||
                (m_PrimitiveGuideType == StencilType.Pyramid &&
                 m_PrimitiveType == SDFPrimitiveType.Pyramid) ||
                (m_PrimitiveGuideType == StencilType.Torus &&
                 m_PrimitiveType == SDFPrimitiveType.Torus) ||
                (m_PrimitiveGuideType == StencilType.BoxFrame &&
                 m_PrimitiveType == SDFPrimitiveType.BoxFrame);
            if (!valid)
            {
                throw new InvalidOperationException(
                    $"Unsupported SDF primitive guide mapping: " +
                    $"{m_PrimitiveGuideType} -> {m_PrimitiveType}.");
            }
        }

        private bool UsesRadialDimensions =>
            m_PrimitiveType != SDFPrimitiveType.BoxFrame;

        private static void ValidateExtents(Vector3 value)
        {
            if (value.x <= 0f || value.y <= 0f || value.z <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value, "Guide extents must be positive.");
            }
        }
    }
}
