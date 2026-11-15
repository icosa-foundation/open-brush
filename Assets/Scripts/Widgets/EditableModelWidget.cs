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
        public EditableModelDefinitionData m_Definition;
        public PolyMesh m_PolyMesh;
        public Color m_Color = Color.white;
        private EditableModelMaterialData m_MaterialSettings;
        public EditableModelMaterialData MaterialSettings => m_MaterialSettings;
        public static EditableModelWidget LastActivated { get; private set; }
        // Retain stretches already stored in sketches. New axis gestures edit the
        // recipe instead, so newly created shapes keep this at Vector3.one.
        private Vector3 m_AxisScale = Vector3.one;
        private Axis? m_LockedManipulationAxis;
        private object m_AxisResizeGesture;
        private bool m_Ellipse;
        private MeshFilter m_ScalePreview;
        private int m_ScalePreviewAxes;
        private Bounds m_ScalePreviewBounds;

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
                range.y = Mathf.Min(range.y, extent * ProceduralShapes.MaximumAxisResizeRatio(m_PolyRecipe, m_LockedManipulationAxis.Value));
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
            m_LockedManipulationAxis = InferManipulationAxis(primaryHand, secondaryHand, secondaryHandInObject);
        }

        protected override void OnUserEndTwoHandGrab()
        {
            base.OnUserEndTwoHandGrab();
            m_LockedManipulationAxis = null;
            m_AxisResizeGesture = null;
        }

        public override Axis GetScaleAxis(Vector3 handA, Vector3 handB, out Vector3 axisDirection, out float axisExtent)
            => GetScaleAxis(m_LockedManipulationAxis ?? Axis.Invalid, out axisDirection, out axisExtent);

        private Axis InferManipulationAxis(Vector3 primaryHand, Vector3 secondaryHand, bool secondaryHandInside)
        {
            // Use the same decision for the live preview and the eventual grab.
            if (secondaryHandInside) { return Axis.Invalid; }
            Vector3 hands = transform.InverseTransformDirection(primaryHand - secondaryHand).Abs();
            Axis axis = hands.x > hands.y && hands.x > hands.z ? Axis.X
                : hands.y > hands.z ? Axis.Y : Axis.Z;
            return GetScaleAxis(axis, out _, out _);
        }

        private Axis GetScaleAxis(Axis axis, out Vector3 axisDirection, out float axisExtent)
        {
            axisDirection = Vector3.zero;
            axisExtent = 0;
            if (axis == Axis.Invalid || Model == null || !Model.m_Valid) { return Axis.Invalid; }
            if (!ProceduralShapes.CanResizeAxis(m_PolyRecipe, axis)) { return Axis.Invalid; }
            int index = (int)axis;
            Vector3 size = Model.GeneratedMesh != null ? Model.GeneratedMesh.bounds.size : Model.m_MeshBounds.size;
            axisExtent = Mathf.Abs(TrTransform.FromTransform(transform.parent).scale * m_Size) *
                m_AxisScale[index] * size[index];
            // A flat face cannot gain thickness through scaling.
            if (axisExtent <= Mathf.Epsilon) { return Axis.Invalid; }
            Vector3 direction = Vector3.zero;
            direction[index] = 1;
            axisDirection = transform.TransformDirection(direction);
            return axis;
        }

        public override void RegisterHighlight()
        {
            Axis axis = GetScalePreviewAxis();
            if (axis == Axis.Invalid)
            {
                UnregisterScalePreview();
                base.RegisterHighlight();
                return;
            }

            base.UnregisterHighlight();
            UpdateScalePreview(axis);
            App.Instance.SelectionEffect.RegisterMesh(m_ScalePreview);
        }

        private Axis GetScalePreviewAxis()
        {
            if (Pinned || m_PolyRecipe == null) { return Axis.Invalid; }
            if (m_LockedManipulationAxis.HasValue)
            {
                return GetScaleAxis(m_LockedManipulationAxis.Value, out _, out _);
            }

            Vector3 brush = InputManager.Brush.Transform.position;
            Vector3 wand = InputManager.Wand.Transform.position;
            var primaryName = m_InteractingController;
            if (!m_UserInteracting)
            {
                // Hover must preview the gesture before either grip is pressed.
                // The controller inside the grab box would become the first hand.
                if (GetActivationScore(brush, InputManager.ControllerName.Brush) >= 0)
                {
                    primaryName = InputManager.ControllerName.Brush;
                }
                else if (GetActivationScore(wand, InputManager.ControllerName.Wand) >= 0)
                {
                    primaryName = InputManager.ControllerName.Wand;
                }
                else { return Axis.Invalid; }
            }
            bool brushIsPrimary = primaryName == InputManager.ControllerName.Brush;
            var secondaryName = brushIsPrimary ? InputManager.ControllerName.Wand : InputManager.ControllerName.Brush;
            Vector3 primary = brushIsPrimary ? brush : wand;
            Vector3 secondary = brushIsPrimary ? wand : brush;
            float previewDistance = Mathf.Max(0f, App.UserConfig.Widgets.ShapeScalePreviewDistance ?? .15f)
                * App.METERS_TO_UNITS;
            Vector3 closestPoint = m_BoxCollider.ClosestPoint(secondary);
            if ((secondary - closestPoint).sqrMagnitude > previewDistance * previewDistance)
            {
                return Axis.Invalid;
            }
            return InferManipulationAxis(primary, secondary, GetActivationScore(secondary, secondaryName) >= 0);
        }

        protected override void UnregisterHighlight()
        {
            UnregisterScalePreview();
            base.UnregisterHighlight();
        }

        protected override void OnUserEndInteracting()
        {
            UnregisterScalePreview();
            base.OnUserEndInteracting();
        }

        private void UnregisterScalePreview()
        {
            if (m_ScalePreview != null && App.Instance != null && App.Instance.SelectionEffect != null)
            {
                App.Instance.SelectionEffect.UnregisterMesh(m_ScalePreview);
            }
        }

        private void UpdateScalePreview(Axis axis)
        {
            if (m_ScalePreview == null)
            {
                var preview = new GameObject("Shape Scale Preview") { hideFlags = HideFlags.HideAndDontSave };
                m_ScalePreview = preview.AddComponent<MeshFilter>();
                m_ScalePreview.sharedMesh = new Mesh
                {
                    name = "Shape Scale Preview",
                    hideFlags = HideFlags.HideAndDontSave
                };
                // SelectionEffect draws it on desktop and temporarily enables it on mobile.
                preview.AddComponent<MeshRenderer>().enabled = false;
            }
            // Keep the helper outside the widget so model bounds, mesh accounting, and
            // duplication cannot mistake the preview for generated shape geometry.
            Transform previewTransform = m_ScalePreview.transform;
            previewTransform.SetParent(transform.parent, false);
            previewTransform.localPosition = transform.localPosition;
            previewTransform.localRotation = transform.localRotation;
            previewTransform.localScale = transform.localScale;
            m_ScalePreview.gameObject.layer = gameObject.layer;

            // Match the geometry, not the padded grab collider. Generated meshes use
            // widget-local coordinates, including any offset in their bounds centre.
            Bounds bounds = Model.GeneratedMesh != null ? Model.GeneratedMesh.bounds : Model.m_MeshBounds;
            int axes = ProceduralShapes.ResizeAxisMask(m_PolyRecipe, axis, EllipseEnabled);
            if (axes == m_ScalePreviewAxes && bounds == m_ScalePreviewBounds) { return; }
            m_ScalePreviewAxes = axes;
            m_ScalePreviewBounds = bounds;
            int faceCount = 0;
            for (int along = 0; along < 3; ++along)
            {
                if ((axes & (1 << along)) != 0) { faceCount += 2; }
            }
            const int coneSegments = 8;
            const int verticesPerFace = 4 + 2 + coneSegments;
            const int indicesPerFace = 6 + coneSegments * 6;
            // Size in model space so the cones follow widget scaling. Keep thin shapes
            // readable without letting large models produce oversized indicators.
            float coneLength = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * .12f,
                .01f * App.METERS_TO_UNITS, .05f * App.METERS_TO_UNITS);
            float coneRadius = coneLength * .4f;
            var vertices = new Vector3[faceCount * verticesPerFace];
            var triangles = new int[faceCount * indicesPerFace];
            int quad = 0;
            for (int along = 0; along < 3; ++along)
            {
                if ((axes & (1 << along)) == 0) { continue; }
                int across = (along + 1) % 3;
                int up = (along + 2) % 3;
                for (int face = 0; face < 2; ++face, ++quad)
                {
                    int start = quad * verticesPerFace;
                    float sign = face == 0 ? -1 : 1;
                    for (int corner = 0; corner < 4; ++corner)
                    {
                        Vector3 vertex = bounds.center;
                        vertex[along] += bounds.extents[along] * (face == 0 ? -1 : 1);
                        vertex[across] += bounds.extents[across] * (corner == 0 || corner == 3 ? -1 : 1);
                        vertex[up] += bounds.extents[up] * (corner < 2 ? -1 : 1);
                        vertices[start + corner] = vertex;
                    }
                    int index = quad * indicesPerFace;
                    triangles[index] = start;
                    triangles[index + 1] = start + (face == 0 ? 2 : 1);
                    triangles[index + 2] = start + (face == 0 ? 1 : 2);
                    triangles[index + 3] = start;
                    triangles[index + 4] = start + (face == 0 ? 3 : 2);
                    triangles[index + 5] = start + (face == 0 ? 2 : 3);

                    // A capped cone starts at the face centre and points away from
                    // the bounds centre, including on shapes with an offset origin.
                    Vector3 faceCenter = bounds.center;
                    faceCenter[along] += bounds.extents[along] * sign;
                    int tip = start + 4;
                    int capCenter = start + 5;
                    int ring = start + 6;
                    vertices[tip] = faceCenter;
                    vertices[tip][along] += coneLength * sign;
                    vertices[capCenter] = faceCenter;
                    for (int segment = 0; segment < coneSegments; ++segment)
                    {
                        float angle = segment * (2f * Mathf.PI / coneSegments);
                        Vector3 vertex = faceCenter;
                        vertex[across] += Mathf.Cos(angle) * coneRadius;
                        vertex[up] += Mathf.Sin(angle) * coneRadius;
                        vertices[ring + segment] = vertex;
                        int current = ring + segment;
                        int next = ring + (segment + 1) % coneSegments;
                        int side = index + 6 + segment * 6;
                        triangles[side] = tip;
                        triangles[side + 1] = face == 0 ? next : current;
                        triangles[side + 2] = face == 0 ? current : next;
                        triangles[side + 3] = capCenter;
                        triangles[side + 4] = face == 0 ? current : next;
                        triangles[side + 5] = face == 0 ? next : current;
                    }
                }
            }
            Mesh mesh = m_ScalePreview.sharedMesh;
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
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
                EditableModelManager.GetOrCreate().RegenerateMeshes(WidgetPropertyCommand.GetTargets(this),
                    EditableModelManager.Evaluate(recipe), recipe);
            }
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
            UnregisterScalePreview();
            if (m_ScalePreview != null)
            {
                Destroy(m_ScalePreview.sharedMesh);
                Destroy(m_ScalePreview.gameObject);
                m_ScalePreview = null;
            }
            base.OnPreDestroy();
        }

        public override void Activate(bool active)
        {
            base.Activate(active);
            if (active && m_PolyRecipe != null) { LastActivated = this; }
        }

        public void SetColor(Color color)
        {
            if (m_MaterialSettings != null) color.a = m_MaterialSettings.Opacity;
            m_Color = color;
            Color visible = color;
            if (m_MaterialSettings != null && !m_MaterialSettings.Transparent) visible.a = 1f;
            TintModel(GetModelGameObject(), visible);
            if (Model != null && Model.m_ModelParent != null) { TintModel(Model.m_ModelParent.gameObject, visible); }
        }

        public EditableModelMaterialData GetMaterialSettings() => m_MaterialSettings?.Clone() ??
            EditableModelMaterialRenderer.Capture(GetModelGameObject().GetComponent<Renderer>().sharedMaterial, m_Color);

        public void SetMaterialSettings(EditableModelMaterialData settings, Color? color = null)
        {
            settings?.Validate();
            m_MaterialSettings = settings?.Clone();
            if (color.HasValue) m_Color = color.Value;
            if (settings != null) m_Color.a = settings.Opacity;
            var materials = new System.Collections.Generic.HashSet<Material>();
            foreach (var root in new[] { GetModelGameObject(), Model?.m_ModelParent?.gameObject })
            {
                if (root == null) continue;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var material in renderer.sharedMaterials) materials.Add(material);
                    if (m_InitialMaterials != null && m_InitialMaterials.TryGetValue(renderer, out var initial))
                        foreach (var material in initial) materials.Add(material);
                    if (m_NewMaterials != null && m_NewMaterials.TryGetValue(renderer, out var highlighted))
                        foreach (var material in highlighted) materials.Add(material);
                }
            }
            var fallback = settings != null ? null : m_Color.a < 1 ? ModelCatalog.m_Instance.m_ObjLoaderTransparentMaterial
                : ModelCatalog.m_Instance.m_ObjLoaderStandardMaterial;
            foreach (var material in materials)
                if (material != null) EditableModelMaterialRenderer.Apply(material, settings, m_Color, fallback);
        }

        private void TintModel(GameObject root, Color color)
        {
            if (root == null) { return; }
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                TintMaterials(renderer.sharedMaterials, color);
                // Highlight keywords swap between cached material instances. Keep
                // both sets current so deselection cannot restore the old colour.
                if (m_InitialMaterials != null && m_InitialMaterials.TryGetValue(renderer, out var initial))
                {
                    TintMaterials(initial, color);
                }
                if (m_NewMaterials != null && m_NewMaterials.TryGetValue(renderer, out var highlighted))
                {
                    TintMaterials(highlighted, color);
                }
            }
        }

        private static void TintMaterials(Material[] materials, Color color)
        {
            foreach (var material in materials)
            {
                if (material == null) { continue; }
                if (material.HasProperty("_BaseColor")) { material.SetColor("_BaseColor", color); }
                if (material.HasProperty("_Color")) { material.SetColor("_Color", color); }
            }
        }

        public override GrabWidget Clone() => Clone(transform.position, transform.rotation, m_Size);

        public override GrabWidget Clone(Vector3 position, Quaternion rotation, float size)
        {
            var poly = m_PolyMesh.Duplicate();
            poly.ScalingFactor = m_PolyMesh.ScalingFactor;
            var clone = EditableModelManager.GetOrCreate().GeneratePolyMesh(poly,
                new GeometryRecipeData(m_PolyRecipe.ToJson()), TrTransform.TRS(position, rotation, size),
                recordCommand: false, color: m_Color, skipIntroAnimation: true);
            clone.m_PreviousCanvas = m_PreviousCanvas;
            clone.m_Definition = m_Definition?.WithRecipe(clone.m_PolyRecipe);
            clone.transform.SetParent(transform.parent, true);
            clone.SetSignedWidgetSize(size);
            clone.CustomDimension = CustomDimension;
            clone.CloneInitialMaterials(this);
            if (m_MaterialSettings != null) clone.SetMaterialSettings(m_MaterialSettings);
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
