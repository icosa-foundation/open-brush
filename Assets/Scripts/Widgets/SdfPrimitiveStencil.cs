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

            float radialAspect = Mathf.Max(
                k_MinimumAspectRatio, Mathf.Max(m_AspectRatio.x, m_AspectRatio.z));
            m_AspectRatio = new Vector3(
                radialAspect,
                Mathf.Max(k_MinimumAspectRatio, m_AspectRatio.y),
                radialAspect);
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
            Vector3 secondary_OS = transform.InverseTransformPoint(secondaryHand);
            float radialDistance = new Vector2(secondary_OS.x, secondary_OS.z).magnitude;
            return Mathf.Abs(secondary_OS.y) > radialDistance ? Axis.Y : Axis.XZ;
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
                case Axis.Y:
                    axisVec = transform.up;
                    extent = parentScale * Extents.y;
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
            float radius = 0.5f * Mathf.Max(m_AspectRatio.x, m_AspectRatio.z);
            float halfHeight = 0.5f * m_AspectRatio.y;
            return new Vector4(radius, halfHeight, 0f, 0f);
        }

        private void ValidateConfiguration()
        {
            bool valid =
                m_PrimitiveGuideType == StencilType.Cylinder &&
                m_PrimitiveType == SDFPrimitiveType.Cylinder ||
                m_PrimitiveGuideType == StencilType.Cone &&
                m_PrimitiveType == SDFPrimitiveType.Cone ||
                m_PrimitiveGuideType == StencilType.Pyramid &&
                m_PrimitiveType == SDFPrimitiveType.Pyramid;
            if (!valid)
            {
                throw new InvalidOperationException(
                    $"Unsupported SDF primitive guide mapping: " +
                    $"{m_PrimitiveGuideType} -> {m_PrimitiveType}.");
            }
        }

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
