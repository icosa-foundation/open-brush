// Copyright 2022 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System.Linq;
using Polyhydra.Core;
using TiltBrush.DocumentImport;
using TiltBrush.MeshEditing;
using UnityEngine;

namespace TiltBrush
{
    // Runtime subset ported from the 3D Shapes branch. The authoring recipe now uses
    // the shared generator/operations contract rather than the branch's old serializer.
    public class EditableModelWidget : ModelWidget
    {
        public GeometryRecipeData m_PolyRecipe;
        public PolyMesh m_PolyMesh;
        public Color m_Color = Color.white;
        public static EditableModelWidget LastActivated { get; private set; }
        private Mesh m_OwnedGeneratedMesh;
        // Retain stretches already stored in sketches. New axis gestures edit the
        // recipe instead, so newly created shapes keep this at Vector3.one.
        private Vector3 m_AxisScale = Vector3.one;
        private Axis? m_LockedManipulationAxis;
        private object m_AxisResizeGesture;
        private bool m_Ellipse;

        // Session-only editing mode. A loaded ellipse is inferred from its geometry;
        // checking the toggle on a circle doesn't introduce a recipe field.
        public bool EllipseEnabled
        {
            get => m_Ellipse || ProceduralShapes.IsElliptical(m_PolyRecipe);
            set => m_Ellipse = value;
        }

        public override Vector3 CustomDimension
        {
            get => m_AxisScale;
            set
            {
                for (int i = 0; i < 3; ++i)
                {
                    if (!(value[i] > 0) || float.IsInfinity(value[i]))
                    {
                        throw new System.ArgumentOutOfRangeException(nameof(value), "Shape axis scales must be finite and positive");
                    }
                }
                m_AxisScale = value;
                UpdateScale();
            }
        }

        protected override void UpdateScale()
        {
            if (this == null) { return; }
            base.UpdateScale();
            transform.localScale = m_Size * m_AxisScale;
        }

        public override Vector2 GetAxisSizeRange()
        {
            float parentScale = Mathf.Abs(TrTransform.FromTransform(transform.parent).scale);
            Vector2 range = new Vector2(m_MinSize_CS, m_MaxSize_CS) * parentScale;
            if (GetScaleAxis(Vector3.zero, Vector3.zero, out _, out float extent) != Axis.Invalid)
            {
                range.x = Mathf.Max(range.x, extent * ProceduralShapes.MinimumAxisResizeRatio(m_PolyRecipe, m_LockedManipulationAxis.Value));
            }
            return range;
        }

        protected override Vector3 HomeSnapOffset => Vector3.Scale(base.HomeSnapOffset, m_AxisScale);

        protected override Vector3 GetHomeSnapLocation(Quaternion snapOrient)
        {
            Vector3 center = Model.m_MeshBounds.center;
            return base.GetHomeSnapLocation(snapOrient) + snapOrient *
                (App.Scene.Pose.scale * m_Size * (center - Vector3.Scale(center, m_AxisScale)));
        }

        protected override void OnUserBeginTwoHandGrab(Vector3 primaryHand, Vector3 secondaryHand, bool secondaryHandInObject)
        {
            base.OnUserBeginTwoHandGrab(primaryHand, secondaryHand, secondaryHandInObject);
            m_AxisResizeGesture = new object();
            // Match box guides: two hands inside scale uniformly; an outside hand chooses an axis.
            Vector3 hands = transform.InverseTransformDirection(primaryHand - secondaryHand).Abs();
            m_LockedManipulationAxis = secondaryHandInObject ? Axis.Invalid
                : hands.x > hands.y && hands.x > hands.z ? Axis.X
                : hands.y > hands.z ? Axis.Y : Axis.Z;
        }

        protected override void OnUserEndTwoHandGrab()
        {
            base.OnUserEndTwoHandGrab();
            m_LockedManipulationAxis = null;
            m_AxisResizeGesture = null;
        }

        public override Axis GetScaleAxis(Vector3 handA, Vector3 handB, out Vector3 axisDirection, out float axisExtent)
        {
            Axis axis = m_LockedManipulationAxis ?? Axis.Invalid;
            axisDirection = Vector3.zero;
            axisExtent = 0;
            if (axis == Axis.Invalid || Model == null || !Model.m_Valid) { return Axis.Invalid; }
            if (!ProceduralShapes.CanResizeAxis(m_PolyRecipe, axis)) { return Axis.Invalid; }
            int index = (int)axis;
            Vector3 size = m_OwnedGeneratedMesh != null ? m_OwnedGeneratedMesh.bounds.size : Model.m_MeshBounds.size;
            axisExtent = Mathf.Abs(TrTransform.FromTransform(transform.parent).scale * m_Size) *
                m_AxisScale[index] * size[index];
            // A flat face cannot gain thickness through scaling.
            if (axisExtent <= Mathf.Epsilon) { return Axis.Invalid; }
            Vector3 direction = Vector3.zero;
            direction[index] = 1;
            axisDirection = transform.TransformDirection(direction);
            return axis;
        }

        public override void RecordAndApplyScaleToAxis(float deltaScale, Axis axis)
        {
            if (!ProceduralShapes.CanResizeAxis(m_PolyRecipe, axis) || !(deltaScale > 0) ||
                float.IsInfinity(deltaScale) || deltaScale == 1f) { return; }
            var recipe = ProceduralShapes.ResizeAxis(m_PolyRecipe, axis, deltaScale, EllipseEnabled);
            if (m_RecordMovements)
            {
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                    new ModifyShapeCommand(this, recipe, m_AxisResizeGesture, mergeWidgetMovement: true));
            }
            else
            {
                EditableModelManager.GetOrCreate().RegenerateMesh(this, EditableModelManager.Evaluate(recipe));
                m_PolyRecipe = recipe;
            }
        }

        internal void TakeGeneratedMeshOwnership(Mesh mesh)
        {
            Mesh previousMesh = m_OwnedGeneratedMesh;
            m_OwnedGeneratedMesh = mesh;
            if (previousMesh != null && previousMesh != mesh) { Destroy(previousMesh); }
        }

        internal void RefreshGeneratedGeometry(Mesh mesh)
        {
            if (m_SnapGhost != null)
            {
                foreach (var filter in m_SnapGhost.GetComponentsInChildren<MeshFilter>(true)) { filter.sharedMesh = mesh; }
            }
            RefreshModelGeometry();
        }

        public override void OnPreDestroy()
        {
            if (m_OwnedGeneratedMesh != null) { Destroy(m_OwnedGeneratedMesh); }
            m_OwnedGeneratedMesh = null;
            base.OnPreDestroy();
        }

        public override void Activate(bool active)
        {
            base.Activate(active);
            if (active && m_PolyRecipe != null) { LastActivated = this; }
        }

        public override GrabWidget Clone() => Clone(transform.position, transform.rotation, m_Size);

        public override GrabWidget Clone(Vector3 position, Quaternion rotation, float size)
        {
            var poly = m_PolyMesh.Duplicate();
            poly.ScalingFactor = m_PolyMesh.ScalingFactor;
            var clone = EditableModelManager.GetOrCreate().GeneratePolyMesh(poly,
                new GeometryRecipeData(m_PolyRecipe.ToJson()), TrTransform.TRS(position, rotation, size), recordCommand: false, color: m_Color);
            clone.m_PreviousCanvas = m_PreviousCanvas;
            clone.transform.SetParent(transform.parent, true);
            clone.SetSignedWidgetSize(size);
            clone.CustomDimension = CustomDimension;
            clone.CloneInitialMaterials(this);
            HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);
            clone.TrySetCanvasKeywordsFromObject(transform);
            return clone;
        }

        protected override void CloneInitialMaterials(GrabWidget other)
        {
            m_WidgetRenderers = GetComponentsInChildren<Renderer>()
                .Where(renderer => renderer.gameObject != GetModelGameObject()).ToArray();
            m_InitialMaterials = m_WidgetRenderers.ToDictionary(renderer => renderer, renderer => renderer.sharedMaterials);
            m_NewMaterials = m_WidgetRenderers.ToDictionary(renderer => renderer, renderer => renderer.materials);
        }

        public GameObject GetModelGameObject() => GetComponentInChildren<ObjModelScript>().gameObject;
    }
}
