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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TiltBrush
{

    public abstract class StencilWidget : ShapeWidget
    {
        [SerializeField] private Color m_TintColor;
        [SerializeField] protected float m_StencilGrabDistance = 1.0f;
        [SerializeField] protected float m_PointerLiftSlope;
        protected StencilType m_Type;
        protected Vector3 m_KahanSummationC;
        protected bool m_StickyTransformEnabled;
        protected TrTransform m_StickyTransformBreakDelta;

        // null means not locked. Invalid means "locked, but no axis"
        protected Axis? m_LockedManipulationAxis;

        /// The full extent along each axis, to support non-uniform scale.
        /// Some subclasses (eg spheres) may not support assignment of non-uniform extent.
        public abstract Vector3 Extents
        {
            get;
            set;
        }

        // Currently used for:
        // - undo/redo (which treats it as opaque)
        // - scale (which treats it as an extent in order to calculate delta-size range)
        // Previously used for:
        // - save/load (which treats it as opaque but also requires that the meaning not change)
        //
        // It should really only be used for undo/redo
        public override Vector3 CustomDimension
        {
            get { return Vector3.one; }
            set { }
        }

        /// Data that is saved to the .tilt file.
        /// Be very careful when changing this, because it affects the save file format.
        /// This does not really need to be virtual, except to implement the temporary
        /// backwards-compatibility code.
        public Guides.State GetSaveState(GroupIdMapping groupIdMapping)
        {
            return new Guides.State
            {
                Transform = TrTransform.TRS(transform.localPosition, transform.localRotation, 0),
                Extents = Extents,
                Pinned = m_Pinned,
                GroupId = groupIdMapping.GetId(Group)
            };
        }
        public Guides.State SaveState
        {
            set
            {
                transform.localPosition = value.Transform.translation;
                transform.localRotation = value.Transform.rotation;
                Extents = value.Extents;
                if (value.Pinned)
                {
                    PinFromSave();
                }
                Group = App.GroupManager.GetGroupFromId(value.GroupId);
                SetCanvas(App.Scene.GetOrCreateLayer(value.LayerId));
            }
        }

        /// Returns the axis the user probably means to modify.
        /// Subclasses are free to return any Axis value, including Invalid (no preferred axis)
        /// Pass:
        ///   primaryHand - the hand that first grabbed the object. Guaranteed to be inside.
        ///   secondaryHand - the other hand grabbing the object. Not guaranteed to be inside.
        protected abstract Axis GetInferredManipulationAxis(
            Vector3 primaryHand, Vector3 secondaryHand, bool secondaryHandInside);

        /// Implementation must handle any axes returned by GetInferredManipulationAxis(),
        /// except for Invalid which is handled by StencilWidget.RegisterHighlight
        protected abstract void RegisterHighlightForSpecificAxis(Axis highlightAxis);

        /// All StencilWidgets are expected to comply with axis locking,
        /// so the base GrabWidget implementation is inappropriate.
        ///
        /// Implementations should ignore handA and handB, and return results
        /// for m_LockedManipulationAxis, which is guaranteed to be non-null.
        public abstract override Axis GetScaleAxis(
            Vector3 handA, Vector3 handB,
            out Vector3 axisVec, out float extent);

        override protected void Awake()
        {
            base.Awake();

            // Custom pin scalar for stencils.
            m_PinScalar = 0.5f;

            // Set a new batchId on this image so it can be picked up in GPU intersections.
            m_BatchId = GpuIntersector.GetNextBatchId();
            WidgetManager.m_Instance.AddWidgetToBatchMap(this, m_BatchId);
            HierarchyUtils.RecursivelySetMaterialBatchID(transform, m_BatchId);
            RestoreGameObjectLayer(App.Scene.MainCanvas.gameObject.layer);
        }

        public override GrabWidget Clone()
        {
            return Clone(transform.position, transform.rotation, m_Size);
        }
        public override GrabWidget Clone(Vector3 position, Quaternion rotation, float size)
        {
            StencilWidget clone = Instantiate(WidgetManager.m_Instance.GetStencilPrefab(this.Type));
            clone.m_PreviousCanvas = m_PreviousCanvas;
            clone.transform.position = position;
            clone.transform.rotation = rotation;
            clone.m_SkipIntroAnim = true;
            // We want to lie about our intro transition amount.
            clone.m_ShowTimer = clone.m_ShowDuration;
            clone.transform.parent = transform.parent;
            clone.Show(true, false);
            clone.SetSignedWidgetSize(size);
            clone.CloneInitialMaterials(this);
            clone.Extents = this.Extents;
            HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);

            CanvasScript canvas = transform.parent.GetComponent<CanvasScript>();
            if (canvas != null)
            {
                var materials = clone.GetComponentsInChildren<Renderer>().SelectMany(x => x.materials);
                foreach (var material in materials)
                {
                    foreach (string keyword in canvas.BatchManager.MaterialKeywords)
                    {
                        material.EnableKeyword(keyword);
                    }
                }
            }

            return clone;
        }

        // Given a pos, find the closest position and surface normal of the stencil widget's collider.
        //   - surfacePos is the closest position on the surface, but in the case of ambiguity, will
        //     return a position most appropriate for the user experience.
        //   - surfaceNorm is always outward facing, and in cases of ambiguity, will return a vector
        //     most appropriate for the user experience.
        public virtual void FindClosestPointOnSurface(Vector3 pos,
                                                      out Vector3 surfacePos, out Vector3 surfaceNorm)
        {
            surfacePos = transform.position;
            surfaceNorm = transform.forward;
        }

        override protected void OnUserBeginTwoHandGrab(
            Vector3 primaryHand, Vector3 secondaryHand, bool secondaryHandInObject)
        {
            base.OnUserBeginTwoHandGrab(primaryHand, secondaryHand, secondaryHandInObject);
            m_LockedManipulationAxis = GetInferredManipulationAxis(
                primaryHand, secondaryHand, secondaryHandInObject);
        }

        override protected void OnUserEndTwoHandGrab()
        {
            base.OnUserEndTwoHandGrab();
            m_LockedManipulationAxis = null;
        }

        override protected void OnShow()
        {
            base.OnShow();

            // Refresh visibility with current state of stencil interaction.
            RefreshVisibility(WidgetManager.m_Instance.StencilsDisabled);
        }

        virtual public void SetInUse(bool bInUse)
        {
            if (m_TintableMeshes != null)
            {
                Color rMatColor = bInUse && !WidgetManager.m_Instance.WidgetsDormant ?
                    m_TintColor : GrabWidget.m_InactiveGrey;
                for (int i = 0; i < m_TintableMeshes.Length; ++i)
                {
                    m_TintableMeshes[i].material.color = rMatColor;
                }
            }
        }

        protected override IEnumerable<StencilWidget> GetStencilsToIgnore()
        {
            return new List<StencilWidget> { this };
        }

        public void RefreshVisibility(bool bStencilDisabled)
        {
            if (m_TintableMeshes != null)
            {
                for (int i = 0; i < m_TintableMeshes.Length; ++i)
                {
                    m_TintableMeshes[i].enabled = !bStencilDisabled;
                }
            }
        }

        // As the user paints on a stencil, the lift offset should grow at a steady rate to allow layers
        // to build up.
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public void AdjustLift(float fDistance_CS)
        {
            // Kahan sum algorithm, https://en.wikipedia.org/wiki/Kahan_summation_algorithm
            // Keep track of the "leftover" that doesn't get applied (as a result of precision issues)
            // and apply it the next time around.
            //   y = input[i] - c
            //   tmp = sum + y
            //   c = (tmp - sum) - y
            //   sum = tmp
            Vector3 liftAmount_CS = m_PointerLiftSlope * fDistance_CS * Vector3.one;
            liftAmount_CS -= m_KahanSummationC;
            Vector3 tmp = Extents + liftAmount_CS;
            // compiler must be prevented from "optimizing" this to zero
            m_KahanSummationC = (tmp - Extents) - liftAmount_CS;
            Extents = tmp;
        }

        public StencilType Type
        {
            get { return m_Type; }
        }

        public static void FromGuideIndex(Guides guide)
        {
            StencilType stencilType = guide.Type;

            foreach (var state in guide.States)
            {
                StencilWidget stencil;
                try
                {
                    stencil = Instantiate(
                        WidgetManager.m_Instance.GetStencilPrefab(stencilType));
                }
                catch (ArgumentException e)
                {
                    Debug.LogException(e);
                    return;
                }

                stencil.m_SkipIntroAnim = true;
                stencil.transform.parent = App.Instance.m_CanvasTransform;
                try
                {
                    stencil.SaveState = state;
                }
                catch (ArgumentException e)
                {
                    Debug.LogException(e, stencil);
                }
                stencil.Show(true, false);
            }
        }

        protected override void SpoofScaleForShowAnim(float showRatio)
        {
            transform.localScale = m_Size * showRatio * Vector3.one;
        }

        override protected void OnUserBeginInteracting()
        {
            base.OnUserBeginInteracting();
            m_LockedManipulationAxis = null;
            if (m_TintableMeshes != null)
            {
                Shader.SetGlobalFloat("_UserIsInteractingWithStencilWidget", 1.0f);
            }
        }

        override protected void OnUserEndInteracting()
        {
            base.OnUserEndInteracting();
            if (m_TintableMeshes != null)
            {
                Shader.SetGlobalFloat("_UserIsInteractingWithStencilWidget", 0.0f);
            }
        }

        public override void RegisterHighlight()
        {
            if (m_Pinned || !m_UserInteracting || App.Config.IsMobileHardware)
            {
                if (!WidgetManager.m_Instance.WidgetsDormant)
                {
                    base.RegisterHighlight();
                }
                return;
            }

            var primaryName = m_InteractingController;
            // Guess at what the other manipulating controller is
            var secondaryName = (m_InteractingController == InputManager.ControllerName.Brush)
                ? InputManager.ControllerName.Wand
                : InputManager.ControllerName.Brush;
            var primary = InputManager.Controllers[(int)primaryName].Transform.position;
            var secondary = InputManager.Controllers[(int)secondaryName].Transform.position;
            bool secondaryInside = GetActivationScore(secondary, secondaryName) >= 0;

            var highlightAxis = m_LockedManipulationAxis
                ?? GetInferredManipulationAxis(primary, secondary, secondaryInside);
            if (highlightAxis == Axis.Invalid)
            {
                base.RegisterHighlight();
            }
            else
            {
                RegisterHighlightForSpecificAxis(highlightAxis);
            }
        }

        override public void RestoreGameObjectLayer(int layer)
        {
            HierarchyUtils.RecursivelySetLayer(transform, layer);

            int layerIndex = Pinned ? WidgetManager.m_Instance.PinnedStencilLayerIndex :
                WidgetManager.m_Instance.StencilLayerIndex;

            // The stencil collider object has to stay in the stencil layer so it can be picked
            // up by physics checks.
            m_Collider.gameObject.layer = WidgetManager.m_Instance.StencilLayerIndex;
            for (int i = 0; i < m_TintableMeshes.Length; ++i)
            {
                m_TintableMeshes[i].gameObject.layer = layerIndex;
            }
        }

        protected override void InitPin()
        {
            base.InitPin();

            int layerIndex = Pinned ? WidgetManager.m_Instance.PinnedStencilLayerIndex :
                WidgetManager.m_Instance.StencilLayerIndex;

            for (int i = 0; i < m_TintableMeshes.Length; ++i)
            {
                m_TintableMeshes[i].gameObject.layer = layerIndex;
            }
        }
    }
} // namespace TiltBrush
