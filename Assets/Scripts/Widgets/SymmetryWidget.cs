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

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiltBrush
{

    [System.Serializable]
    public class GuideBeamPair
    {
        public Transform m_Positive;
        public Transform m_Negative;
    }

    public class SymmetryWidget : GrabWidget
    {
        [SerializeField] private Renderer m_LeftRightMesh;
        [SerializeField] private Renderer m_FrontBackMesh;
        [SerializeField] private Renderer m_SingleAxisMesh;
        [SerializeField] private Renderer m_SmallCubeMesh;
        [SerializeField] private TextMeshPro m_TitleText;
        // The title shows whether the widget stands for a linked mirror.
        private static readonly Color kLinkedTitleColor = new Color(0.45f, 0.9f, 1f);
        private string m_PlainTitle;
        private bool m_ShowingLinked;
        private bool m_TitleActive;
        [SerializeField] private GameObject m_HintText;
        [SerializeField] private GrabWidgetHome m_Home;

        [SerializeField] private Mesh m_CustomSymmetryMesh;
        [SerializeField] private Material m_CustomSymmetryMaterial;

        private class GuideBeam
        {
            public Transform Transform;
            public Renderer Renderer;
            public Vector3 Offset;
            public Vector3 BaseScale;
            public Quaternion BaseRotation;
        }
        [SerializeField] private GuideBeamPair m_GuideBeamPairTemplate;
        private readonly Vector3[] m_GuideBeamDirections = new Vector3[4];
        private readonly Vector3[] m_ScriptedGuideBeamDirections = new Vector3[4];
        private GuideBeam[] m_GuideBeams;
        private bool m_GuideBeamsDirty;
        [SerializeField] private float m_GuideBeamLength;
        private float m_GuideBeamShowRatio;

        [SerializeField] private Color m_SnapColor;
        [SerializeField] private float m_SnapOrientationSpeed = 0.2f;

        [SerializeField] private float m_SnapXZPlaneStickyAmount;

        [SerializeField] private float m_JumpToUserControllerOffsetDistance;
        [SerializeField] private float m_JumpToUserControllerYOffset;
        private static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        // A continuous walk of the twelve cell edges, retracing three edges to join the path.
        [SerializeField] private Transform m_SymmetryDomainPrefab;
        [SerializeField] private Transform m_SymmetryDomainParent;


        public Plane ReflectionPlane
        {
            get
            {
                return new Plane(transform.right, transform.position);
            }
        }

        /// A linked mirror can't spin or drift: its strokes follow it only through a recorded
        /// move, and a mirror in continuous motion has no point at which to record one.
        public static bool CanMoveFreely => SymmetryMirrors.Showing == null;

        public void Spin(float xSpeed, float ySpeed, float zSpeed)
        {
            if (!CanMoveFreely) { return; }
            AngularVelocity_GS = App.Scene.Pose.rotation * new Vector3(xSpeed, ySpeed, zSpeed);
            m_IsSpinningFreely = true;
        }

        /// Released while linked, the mirror stays where it was let go. Tossing it away to hide
        /// it still works.
        public override void SetVelocities(Vector3 vLinVel, Vector3 vAngVel, Vector3 vPivot)
        {
            if (!CanMoveFreely && !IsHideToss(vLinVel, vAngVel, vPivot))
            {
                vLinVel = Vector3.zero;
                vAngVel = Vector3.zero;
            }
            base.SetVelocities(vLinVel, vAngVel, vPivot);
        }

        /// Stops a spin or drift that started before the mirror became linked.
        public void HaltIfLinked()
        {
            if (!CanMoveFreely && !m_UserInteracting && !IsTossed() &&
                (m_IsSpinningFreely || IsMoving()))
            {
                m_IsSpinningFreely = false;
                HaltDrift();
            }
        }

        public Vector3 GetSpin()
        {
            return App.Scene.Pose.rotation.TrueInverse() * AngularVelocity_GS;
        }

        public override Vector3 CustomDimension
        {
            get { return m_AngularVelocity_LS; }
            set
            {
                // Undo and redo restore the spin here.
                m_AngularVelocity_LS = CanMoveFreely ? value : Vector3.zero;
                m_IsSpinningFreely = m_AngularVelocity_LS.magnitude > m_AngVelDampThreshold;
            }
        }

        override protected void Awake()
        {
            base.Awake();

            m_AngVelDampThreshold = 50f;

            // The authored pair points along local Y. Reuse it, then create the other pairs
            // from the same template so appearance and placement have one source.
            m_GuideBeams = new GuideBeam[m_GuideBeamDirections.Length * 2];
            var tintableMeshes = new List<Renderer>(m_TintableMeshes);
            for (int i = 0; i < m_GuideBeams.Length; ++i)
            {
                var template = i % 2 == 0 ? m_GuideBeamPairTemplate.m_Positive : m_GuideBeamPairTemplate.m_Negative;
                var beam = i < 2 ? template : Instantiate(template, template.parent);
                beam.name = $"GuideBeamPair{i / 2}{(i % 2 == 0 ? "Positive" : "Negative")}";
                var renderer = beam.GetComponent<Renderer>();
                m_GuideBeams[i] = new GuideBeam
                {
                    Transform = beam,
                    Renderer = renderer,
                    Offset = Quaternion.Inverse(transform.rotation) * (beam.position - transform.position),
                    BaseScale = beam.localScale,
                    BaseRotation = Quaternion.Inverse(transform.rotation) * beam.rotation
                };
                renderer.enabled = false;
                if (i >= 2) { tintableMeshes.Add(renderer); }
            }
            m_TintableMeshes = tintableMeshes.ToArray();

            m_GuideBeamShowRatio = 0.0f;

            m_Home.Init();
            m_Home.SetOwner(transform);
            m_Home.SetFixedPosition(App.Scene.AsScene[transform].translation);

            m_CustomShowHide = true;
        }

        public void SetAppearance()
        {
            m_SymmetryDomainParent.gameObject.SetActive(
                PointerManager.m_Instance.CurrentSymmetryMode == PointerManager.SymmetryMode.MultiMirror);
            m_FrontBackMesh.enabled = false;
            m_LeftRightMesh.enabled = false;
            m_SingleAxisMesh.enabled = false;
            m_SmallCubeMesh.enabled = false;
            Array.Clear(m_GuideBeamDirections, 0, m_GuideBeamDirections.Length);
            var pm = PointerManager.m_Instance;

            switch (PointerManager.m_Instance.CurrentSymmetryMode)
            {
                case PointerManager.SymmetryMode.SinglePlane:
                    m_FrontBackMesh.enabled = true;
                    // The reflection plane is the widget's local YZ plane.
                    m_GuideBeamDirections[0] = Vector3.up;
                    m_GuideBeamDirections[1] = Vector3.forward;
                    break;
                case PointerManager.SymmetryMode.TwoHanded:
                    m_FrontBackMesh.enabled = true;
                    m_LeftRightMesh.enabled = true;
                    break;
                case PointerManager.SymmetryMode.MultiMirror:
                    switch (PointerManager.m_Instance.m_CustomSymmetryType)
                    {
                        case PointerManager.CustomSymmetryType.Point:
                            // Polyhedral families can also be selected through the API.
                            bool axial = pm.m_PointSymmetryFamily == PointSymmetry.Family.Cn
                                || pm.m_PointSymmetryFamily == PointSymmetry.Family.Cnv
                                || pm.m_PointSymmetryFamily == PointSymmetry.Family.Cnh
                                || pm.m_PointSymmetryFamily == PointSymmetry.Family.Sn
                                || pm.m_PointSymmetryFamily == PointSymmetry.Family.Dn
                                || pm.m_PointSymmetryFamily == PointSymmetry.Family.Dnh
                                || pm.m_PointSymmetryFamily == PointSymmetry.Family.Dnd;
                            m_SingleAxisMesh.enabled = axial;
                            m_SmallCubeMesh.enabled = !axial;
                            if (axial) { m_GuideBeamDirections[0] = Vector3.up; }
                            break;
                        case PointerManager.CustomSymmetryType.LineGroup:
                        case PointerManager.CustomSymmetryType.Helical:
                            m_SingleAxisMesh.enabled = true;
                            // These groups rotate or advance along local Y.
                            m_GuideBeamDirections[0] = Vector3.up;
                            break;
                        case PointerManager.CustomSymmetryType.Wallpaper:
                            m_SmallCubeMesh.enabled = true;
                            SetLatticeBeamDirections(pm, false);
                            break;
                        case PointerManager.CustomSymmetryType.Penrose:
                            m_SmallCubeMesh.enabled = true;
                            m_GuideBeamDirections[0] = Vector3.right;
                            m_GuideBeamDirections[1] = Vector3.up;
                            break;
                        case PointerManager.CustomSymmetryType.SpaceGroup:
                            m_SmallCubeMesh.enabled = true;
                            SetLatticeBeamDirections(pm, true);
                            break;
                        case PointerManager.CustomSymmetryType.Polyhedra:
                            m_SmallCubeMesh.enabled = true;
                            break;
                    }
                    break;
                case PointerManager.SymmetryMode.ScriptedSymmetryMode:
                    m_SmallCubeMesh.enabled = true;
                    Array.Copy(m_ScriptedGuideBeamDirections, m_GuideBeamDirections, m_GuideBeamDirections.Length);
                    break;
            }

            // Normalize independently: lattice spacing affects direction, not beam length.
            for (int i = 0; i < m_GuideBeamDirections.Length; ++i)
            {
                m_GuideBeamDirections[i] = m_GuideBeamDirections[i].normalized;
                bool visible = m_GuideBeamDirections[i] != Vector3.zero;
                m_GuideBeams[i * 2].Renderer.enabled = visible;
                m_GuideBeams[i * 2 + 1].Renderer.enabled = visible;
            }
            m_GuideBeamsDirty = true;
        }

        public void SetScriptedGuideBeams(IReadOnlyList<Vector3> directions)
        {
            if (directions == null || directions.Count > m_ScriptedGuideBeamDirections.Length)
            {
                throw new ArgumentException($"Expected zero to {m_ScriptedGuideBeamDirections.Length} guide beam directions.");
            }
            // Validate the entire configuration before replacing the previous one.
            foreach (var direction in directions)
            {
                if (float.IsNaN(direction.x) || float.IsInfinity(direction.x)
                    || float.IsNaN(direction.y) || float.IsInfinity(direction.y)
                    || float.IsNaN(direction.z) || float.IsInfinity(direction.z))
                {
                    throw new ArgumentException("Guide beam directions must have finite components.");
                }
            }
            Array.Clear(m_ScriptedGuideBeamDirections, 0, m_ScriptedGuideBeamDirections.Length);
            for (int i = 0; i < directions.Count; ++i)
            {
                m_ScriptedGuideBeamDirections[i] = directions[i];
            }
            // Start() runs before the scripted mode is selected. Save its configuration
            // now; SetSymmetryMode will apply it once the mode changes.
            if (PointerManager.m_Instance.CurrentSymmetryMode == PointerManager.SymmetryMode.ScriptedSymmetryMode)
            {
                SetAppearance();
            }
        }

        public void ClearScriptedGuideBeams() => SetScriptedGuideBeams(Array.Empty<Vector3>());

        private void SetLatticeBeamDirections(PointerManager pm, bool includeNormal)
        {
            var a = pm.SymmetryLatticeBasis.MultiplyVector(Vector3.right);
            var b = pm.SymmetryLatticeBasis.MultiplyVector(Vector3.up);
            m_GuideBeamDirections[0] = a;
            m_GuideBeamDirections[1] = b;
            if (includeNormal)
            {
                m_GuideBeamDirections[2] = pm.SymmetryLatticeBasis.MultiplyVector(Vector3.forward);
            }
            if (pm.SymmetryLatticeIsHexagonal)
            {
                // Complete the three equivalent basal directions, whether the basis
                // vectors supplied by the lattice meet at 60 or 120 degrees.
                m_GuideBeamDirections[3] = Vector3.Dot(a, b) > 0 ? a - b : a + b;
            }
        }

        protected override TrTransform GetDesiredTransform(TrTransform xf_GS)
        {
            if (SnapEnabled)
            {
                return GetSnappedTransform(xf_GS);
            }
            return xf_GS;
        }

        override protected void OnUpdate()
        {
            // While the mirror is held, its strokes follow it.
            SymmetryMirrorMove.Update();
            HaltIfLinked();
            if (m_ShowingLinked != (SymmetryMirrors.Showing != null)) { UpdateTitle(); }

            bool moved = m_UserInteracting;

            // Drive the top of the mirror towards room-space up, to keep the text readable
            // It's a bit obnoxious to do this when the user's grabbing it. Maybe we should
            // also not do this when the canvas is being manipulated?
            if (!m_UserInteracting && !m_IsSpinningFreely && !m_SnapDriftCancel
                && PointerManager.m_Instance.CurrentSymmetryMode != PointerManager.SymmetryMode.MultiMirror)
            {
                // Doing the rotation in object space makes it easier to prove that the
                // plane normal will never be affected.
                // NOTE: This assumes mirror-up is object-space-up
                // and mirror-normal is object-space-right (see ReflectionPlane.get)
                Vector3 up_OS = Vector3.up;
                Vector3 normal_OS = Vector3.right;

                Vector3 desiredUp_OS = transform.InverseTransformDirection(Vector3.up);
                float stability;
                float angle = MathUtils.GetAngleBetween(up_OS, desiredUp_OS, normal_OS, out stability);
                if (stability > .1f && Mathf.Abs(angle) > .05f)
                {
                    float delta = angle * m_SnapOrientationSpeed;
                    Quaternion qDelta_OS = Quaternion.AngleAxis(delta, normal_OS);
                    if (m_NonScaleChild != null)
                    {
                        var t = m_NonScaleChild;
                        t.localRotation = t.localRotation * qDelta_OS;
                    }
                    else
                    {
                        var t = transform;
                        t.localRotation = t.localRotation * qDelta_OS;
                    }
                }
            }

            // Rotation about ReflectionPlane.normal is purely visual and does
            // not affect the widget functionality. So when spinning, rotate about
            // normal until the widget is in a "natural" orientation. Natural is
            // defined as: one of the arms of the widget is aligned as closely as
            // possible to the axis of rotation.
            //
            // The spinning plane divides space into 2 (really 3) regions:
            // - Points that are always in front of or in back of the plane
            //   (two cones joined at the tip)
            // - Points that alternate between front and back of the plane
            //
            // Aligning one of the arms this way makes that arm/axis trace out
            // the boundary between these regions. Interestingly enough, the other
            // axis traces out a plane whose normal is the axis of rotation.
            if (IsSpinningFreely)
            {
                float MAX_ROTATE_SPEED = 100f; // deg/sec
                float DECAY_TIME_SEC = .75f;   // Time to decay 63% towards goal
                Vector3 normal = ReflectionPlane.normal;
                Vector3 projected = AngularVelocity_GS;
                projected = projected - Vector3.Dot(normal, projected) * normal;
                float length = projected.magnitude;
                if (length > 1e-4f)
                {
                    projected /= length;

                    // arm to rotate towards projected; pick the one that's closest
                    // Choices are .up and .forward (and their negatives)
                    Vector3 arm =
                        (Mathf.Abs(Vector3.Dot(transform.up, projected)) >
                        Mathf.Abs(Vector3.Dot(transform.forward, projected)))
                            ? transform.up : transform.forward;
                    arm *= Mathf.Sign(Vector3.Dot(arm, projected));

                    // Rotate arm towards projected. Since both arm and projected
                    // are on the plane, the axis should be +normal or -normal.
                    Vector3 cross = Vector3.Cross(arm, projected);
                    Vector3 axis = normal * Mathf.Sign(Vector3.Dot(cross, normal));
                    float delta = Mathf.Asin(cross.magnitude) * Mathf.Rad2Deg;
                    float angle = (1f - Mathf.Exp(-Time.deltaTime / DECAY_TIME_SEC)) * delta;
                    angle = Mathf.Min(angle, MAX_ROTATE_SPEED * Time.deltaTime);
                    Quaternion q = Quaternion.AngleAxis(angle, axis);
                    transform.rotation = q * transform.rotation;
                    moved = true;
                }
            }

            if (moved && m_NonScaleChild != null)
            {
                m_NonScaleChild.OnPosRotChanged();
            }

            //if our transform changed, update the beams
            float fShowRatio = GetShowRatio();
            bool bInTransition = m_GuideBeamShowRatio != fShowRatio;
            if (bInTransition || transform.hasChanged || m_GuideBeamsDirty)
            {
                for (int i = 0; i < m_GuideBeams.Length; ++i)
                {
                    var beam = m_GuideBeams[i];
                    var direction = m_GuideBeamDirections[i / 2];
                    if (direction == Vector3.zero) { continue; }
                    var orientation = Quaternion.FromToRotation(Vector3.up, direction);
                    Vector3 vTransformedOffset = transform.rotation * (orientation * beam.Offset);
                    Vector3 vGuideBeamPos = transform.position + vTransformedOffset;
                    Vector3 vGuideBeamDir = transform.rotation * (i % 2 == 0 ? direction : -direction);

                    float fBeamLength = m_GuideBeamLength * App.METERS_TO_UNITS;
                    fBeamLength *= fShowRatio;

                    //position guide beam half way to hit point
                    Vector3 vHitPoint = vGuideBeamPos + (vGuideBeamDir * fBeamLength);
                    Vector3 vHalfWay = (vGuideBeamPos + vHitPoint) * 0.5f;
                    beam.Transform.SetPositionAndRotation(vHalfWay,
                        transform.rotation * orientation * beam.BaseRotation);

                    //set scale to half the distance
                    Vector3 vScale = beam.BaseScale;
                    vScale.y = fBeamLength * 0.5f;

                    beam.Transform.localScale = vScale;
                }

                transform.hasChanged = false;
                m_GuideBeamShowRatio = fShowRatio;
                m_GuideBeamsDirty = false;
            }

            if (PointerManager.m_Instance.CurrentSymmetryMode == PointerManager.SymmetryMode.MultiMirror)
            {
                DrawCustomSymmetryGuides();
            }
        }

        override public void Activate(bool bActive)
        {
            base.Activate(bActive);
            if (bActive && SnapEnabled)
            {
                for (int i = 0; i < m_GuideBeams.Length; ++i)
                {
                    m_GuideBeams[i].Renderer.material.color = m_SnapColor;
                }
            }
            m_HintText.SetActive(bActive);
            m_TitleActive = bActive;
            UpdateTitle();
        }

        /// "Mirror", or "Mirror / Linked" in a distinct colour while a linked mirror is active,
        /// so the mode is visible on the widget itself.
        private void UpdateTitle()
        {
            if (m_TitleText == null) { return; }
            m_PlainTitle ??= m_TitleText.text;
            m_ShowingLinked = SymmetryMirrors.Showing != null;
            m_TitleText.text = m_ShowingLinked ? "Mirror\nLinked" : m_PlainTitle;
            if (!m_ShowingLinked)
            {
                m_TitleText.color = m_TitleActive ? Color.white : Color.grey;
            }
            else
            {
                Color dim = kLinkedTitleColor * 0.6f;
                dim.a = 1f;
                m_TitleText.color = m_TitleActive ? kLinkedTitleColor : dim;
            }
        }

        override protected void OnUserBeginInteracting()
        {
            SymmetryMirrorMove.Begin();
            base.OnUserBeginInteracting();
            m_Home.gameObject.SetActive(true);
            m_Home.Reset();
        }

        override protected void OnUserEndInteracting()
        {
            base.OnUserEndInteracting();
            m_Home.gameObject.SetActive(false);
            if (m_Home.ShouldSnapHome())
            {
                ResetToHome();
            }
            // After any snap, so the strokes follow the mirror to where it ends up.
            SymmetryMirrorMove.End();
        }

        public Mirror ToMirror()
        {
            return new Mirror
            {
                Transform = TrTransform.FromLocalTransform(transform),
            };
        }

        public void FromMirror(Mirror data)
        {
            transform.localPosition = data.Transform.translation;
            transform.localRotation = data.Transform.rotation;
            if (m_NonScaleChild != null)
            {
                m_NonScaleChild.OnPosRotChanged();
            }
        }

        internal void SetTransformInSceneSpace(TrTransform pose)
        {
            // Snapshots contain scene-relative scale, which depends on the scene zoom
            // when captured. The mirror follows scene position/rotation but keeps its
            // physical size, so restoring that scale would resize it after a zoom.
            if (m_NonScaleChild != null)
            {
                m_NonScaleChild.PositionRotationInParentSpace = pose;
            }
            else
            {
                var globalPose = App.Scene.Pose * pose;
                transform.SetPositionAndRotation(globalPose.translation, globalPose.rotation);
            }
        }

        public void ResetToHome()
        {
            m_IsSpinningFreely = false;
            App.Scene.AsScene[transform] = m_Home.m_Transform_SS;
            transform.localScale = Vector3.one;
            if (m_NonScaleChild != null)
            {
                m_NonScaleChild.OnPosRotChanged();
            }
        }

        public void BringToUser()
        {
            // Get brush controller and place a little in front and a little higher.
            Vector3 controllerPos =
                InputManager.m_Instance.GetController(InputManager.ControllerName.Brush).position;
            Vector3 headPos = ViewpointScript.Head.position;
            Vector3 headToController = controllerPos - headPos;
            Vector3 offset = headToController.normalized * m_JumpToUserControllerOffsetDistance +
                Vector3.up * m_JumpToUserControllerYOffset;
            TrTransform xf_GS = TrTransform.TR(controllerPos + offset, transform.rotation);

            // The transform we built was global space, but we need it in widget local for the command.
            TrTransform newXf = TrTransform.FromTransform(m_NonScaleChild.parent).inverse * xf_GS;
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new MoveWidgetCommand(this, newXf, CustomDimension, final: true),
                discardIfNotMerged: false);
        }

        public override void Show(bool bShow, bool bPlayAudio = true)
        {
            base.Show(bShow, false);

            if (bShow)
            {
                // Play mirror sound
                AudioManager.m_Instance.PlayMirrorSound(transform.position);
            }
        }

        public void DrawCustomSymmetryGuides()
        {
            List<LineRenderer> lrs = new List<LineRenderer>();
            var matrices = PointerManager.m_Instance.CustomMirrorMatrices;

            // This can get called before we've had a chance to set up matrices
            if (matrices.Count < 1)
            {
                PointerManager.m_Instance.CalculateMirrors();
                matrices = PointerManager.m_Instance.CustomMirrorMatrices;
            }

            float mirrorScale = PointerManager.m_Instance.GetCustomMirrorScale();

            lrs = m_SymmetryDomainParent.GetComponentsInChildren<LineRenderer>(true).ToList();
            var defaultWidth = m_SymmetryDomainPrefab.GetComponent<LineRenderer>().widthMultiplier;
            foreach (var lr in lrs)
            {
                lr.gameObject.SetActive(false);
                lr.widthMultiplier = defaultWidth;
                lr.sortingOrder = 0;
            }

            if (PointerManager.m_Instance.m_CustomSymmetryType == PointerManager.CustomSymmetryType.Point
                || PointerManager.m_Instance.m_CustomSymmetryType == PointerManager.CustomSymmetryType.Polyhedra)
            {
                DrawPointGroupGuides(lrs, matrices, mirrorScale);
                return;
            }

            if (PointerManager.m_Instance.m_CustomSymmetryType == PointerManager.CustomSymmetryType.Penrose)
            {
                DrawPenroseGuides(lrs, matrices, mirrorScale);
                return;
            }

            if (PointerManager.m_Instance.m_CustomSymmetryType == PointerManager.CustomSymmetryType.LineGroup)
            {
                DrawLineGroupGuides(lrs, matrices, mirrorScale);
                return;
            }

            if (PointerManager.m_Instance.m_CustomSymmetryType == PointerManager.CustomSymmetryType.SpaceGroup)
            {
                DrawSpaceGroupGuides(lrs, matrices, mirrorScale);
                return;
            }

            for (var i = 0; i < matrices.Count; i++)
            {
                var m0 = matrices[i];
                var m = transform.localToWorldMatrix * m0;
                if (PointerManager.m_Instance.m_CustomSymmetryType == PointerManager.CustomSymmetryType.Helical)
                {
                    var pm = PointerManager.m_Instance;
                    var center = pm.MultiMirrorCenter();
                    // Apply the pointer's world transform to the reference guide. Widget size
                    // changes the marker, while canvas scale determines helical advance.
                    var symmetry = center * pm.TrFromMatrix(m0) * center.inverse;
                    m = symmetry.ToMatrix4x4() * transform.localToWorldMatrix;
                }
                // Size the marker without shifting it away from the symmetry origin.
                m *= Matrix4x4.Scale(new Vector3(0.5f, 0.4f, 0));
                matrices[i] = m;

                if (PointerManager.m_Instance.m_CustomSymmetryType == PointerManager.CustomSymmetryType.Wallpaper)
                {
                    LineRenderer lr;
                    if (i < lrs.Count)
                    {
                        lr = lrs[i];
                    }
                    else
                    {
                        var go = Instantiate(m_SymmetryDomainPrefab, m_SymmetryDomainParent);
                        lr = go.GetComponent<LineRenderer>();
                    }
                    lr.gameObject.SetActive(true);
                    // var path = PointerManager.m_Instance.CustomMirrorDomain;
                    float insetAmount = i == 0 ? .1f : .11f;  // Slightly different inset for the first one so it's visible even if overlapping
                    var path = InsetPolygon(PointerManager.m_Instance.CustomMirrorDomain, insetAmount);
                    var path3d = path.Select(v =>
                    {
                        var p = m0.MultiplyPoint3x4(v);
                        p *= mirrorScale;
                        return p;
                    }).ToArray();
                    lr.loop = true;
                    lr.positionCount = path3d.Length;
                    lr.SetPositions(path3d);
                    if (i == 0)
                    {
                        lr.startColor = Color.white;
                        lr.endColor = Color.white;
                    }
                    else
                    {
                        lr.startColor = Color.blue;
                        lr.endColor = Color.blue;
                    }
                }
            }

            if (PointerManager.m_Instance.m_CustomSymmetryType != PointerManager.CustomSymmetryType.Wallpaper)
            {
                m_CustomSymmetryMaterial.color = Color.gray;
                m_CustomSymmetryMaterial.enableInstancing = true;
                m_CustomSymmetryMaterial.SetFloat(OutlineWidth, -0.01f);
                Graphics.DrawMeshInstanced(
                    m_CustomSymmetryMesh, 0, m_CustomSymmetryMaterial,
                    matrices, null, ShadowCastingMode.Off, false
                );
            }
        }

        private void DrawPointGroupGuides(List<LineRenderer> renderers, List<Matrix4x4> matrices, float mirrorScale)
        {
            var paths = PointerManager.m_Instance.PointGroupDomainPaths;
            var arcs = PointerManager.m_Instance.PointGroupArcPaths;
            var pathCount = paths.Count + arcs.Count;
            for (var pathIndex = 0; pathIndex < pathCount; pathIndex++)
            {
                var isArc = pathIndex >= paths.Count;
                var path = isArc ? arcs[pathIndex - paths.Count] : paths[pathIndex];
                var points = new Vector3[path.Length];
                for (var copy = 0; copy < matrices.Count; copy++)
                {
                    for (var vertex = 0; vertex < path.Length; vertex++)
                        points[vertex] = matrices[copy].MultiplyPoint3x4(path[vertex]) * mirrorScale;
                    var slot = copy * pathCount + pathIndex;
                    var renderer = slot < renderers.Count ? renderers[slot]
                        : Instantiate(m_SymmetryDomainPrefab, m_SymmetryDomainParent).GetComponent<LineRenderer>();
                    renderer.gameObject.SetActive(true);
                    renderer.loop = false;
                    renderer.positionCount = points.Length;
                    renderer.SetPositions(points);
                    var color = copy == 0 ? Color.white : Color.blue;
                    color.a = isArc ? 0.1f : 1f;
                    renderer.startColor = renderer.endColor = color;
                    renderer.sortingOrder = copy == 0 ? 1 : 0;
                }
            }
        }

        private void DrawPenroseGuides(List<LineRenderer> renderers, List<Matrix4x4> matrices, float mirrorScale)
        {
            var pm = PointerManager.m_Instance;
            var tiling = pm.PenrosePattern;
            var rhombPoints = new Vector3[4];
            var boundaryPoints = new Vector3[3];
            var slot = 0;
            for (var i = 0; i < tiling.tiles.Count; i++)
            {
                var tile = tiling.tiles[i];
                var selected = pm.UsesPenroseTile(tile.kind);
                // Selected outlines follow Transform Each just like their copies.
                var frame = selected
                    ? matrices[slot] * pm.PenroseSourcePlacement * tiling.matrices[slot].inverse
                    : Matrix4x4.identity;
                if (selected) slot++;
                var points = tile.completeRhomb ? rhombPoints : boundaryPoints;
                for (var j = 0; j < points.Length; j++)
                    points[j] = frame.MultiplyPoint3x4(tiling.vertices[tile.indices[j]]) * mirrorScale;
                var renderer = i < renderers.Count ? renderers[i]
                    : Instantiate(m_SymmetryDomainPrefab, m_SymmetryDomainParent).GetComponent<LineRenderer>();
                renderer.gameObject.SetActive(true);
                renderer.loop = true;
                renderer.positionCount = points.Length;
                renderer.SetPositions(points);
                var color = i == pm.PenroseReferenceTile ? Color.white : !selected ? Color.gray
                    : tile.kind == PenroseTiling.Kind.Thin ? new Color(1f, 0.7f, 0.15f) : Color.blue;
                renderer.startColor = renderer.endColor = color;
            }
        }

        private void DrawLineGroupGuides(List<LineRenderer> renderers, List<Matrix4x4> matrices, float mirrorScale)
        {
            var pm = PointerManager.m_Instance;
            var advance = pm.m_LineAdvance;
            var domainPath = pm.LineGroupDomainPath;
            var points = new Vector3[domainPath.Length];
            LineRenderer GetRenderer(int index)
            {
                var renderer = index < renderers.Count ? renderers[index]
                    : Instantiate(m_SymmetryDomainPrefab, m_SymmetryDomainParent).GetComponent<LineRenderer>();
                renderer.gameObject.SetActive(true);
                renderer.loop = false;
                return renderer;
            }
            for (var i = 0; i < matrices.Count; i++)
            {
                for (var j = 0; j < points.Length; j++)
                    points[j] = matrices[i].MultiplyPoint3x4(domainPath[j]) * mirrorScale;
                var renderer = GetRenderer(i);
                renderer.positionCount = points.Length;
                renderer.SetPositions(points);
                renderer.startColor = renderer.endColor = i == 0 ? Color.white
                    : matrices[i].determinant < 0 ? new Color(1f, 0.7f, 0.15f) : Color.blue;
            }

            if (advance == 0f) return;

            // Connect the step origins, with a short transverse tick at each step.
            // Using the generated matrices also keeps this guide aligned with Transform Each.
            var copiesPerStep = matrices.Count / pm.m_LineRepeats;
            var axisPoints = new Vector3[pm.m_LineRepeats * 4];
            for (var step = 0; step < pm.m_LineRepeats; step++)
            {
                var matrix = matrices[step * copiesPerStep];
                var origin = matrix.MultiplyPoint3x4(Vector3.zero) * mirrorScale;
                var tick = matrix.MultiplyVector(Vector3.right * (PointerManager.LineGroupDomainRadius * 0.08f)) * mirrorScale;
                axisPoints[step * 4] = origin;
                axisPoints[step * 4 + 1] = origin - tick;
                axisPoints[step * 4 + 2] = origin + tick;
                axisPoints[step * 4 + 3] = origin;
            }
            var axis = GetRenderer(matrices.Count);
            axis.positionCount = axisPoints.Length;
            axis.SetPositions(axisPoints);
            axis.startColor = axis.endColor = Color.gray;
        }

        private void DrawSpaceGroupGuides(List<LineRenderer> renderers,
            List<Matrix4x4> matrices, float mirrorScale)
        {
            var paths = PointerManager.m_Instance.SpaceGroupDomainPaths;
            for (var pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                var path = paths[pathIndex];
                var points = new Vector3[path.Length];
                for (var copy = 0; copy < matrices.Count; copy++)
                {
                    for (var vertex = 0; vertex < path.Length; vertex++)
                    {
                        points[vertex] = matrices[copy].MultiplyPoint3x4(path[vertex]) * mirrorScale;
                    }
                    var index = copy * paths.Count + pathIndex;
                    var renderer = index < renderers.Count
                        ? renderers[index]
                        : Instantiate(m_SymmetryDomainPrefab, m_SymmetryDomainParent).GetComponent<LineRenderer>();
                    renderer.gameObject.SetActive(true);
                    renderer.loop = false;
                    renderer.positionCount = points.Length;
                    renderer.SetPositions(points);
                    renderer.startColor = renderer.endColor = copy == 0 ? Color.white : Color.blue;
                    renderer.sortingOrder = copy == 0 ? 1 : 0;
                }
            }
        }

        public static List<Vector2> InsetPolygon(List<Vector2> originalPoly, float insetAmount)
        {
            insetAmount = -insetAmount;
            int Mod(int x, int m) { return (x % m + m) % m; }

            Vector2 offsetDir = Vector2.zero;

            // Create the Vector3 vertices
            List<Vector2> offsetPoly = new List<Vector2>();
            for (int i = 0; i < originalPoly.Count; i++)
            {
                if (insetAmount != 0)
                {
                    Vector2 tangent1 = (originalPoly[(i + 1) % originalPoly.Count] - originalPoly[i]).normalized;
                    Vector2 tangent2 = (originalPoly[i] - originalPoly[Mod(i - 1, originalPoly.Count)]).normalized;

                    Vector2 normal1 = new Vector2(-tangent1.y, tangent1.x).normalized;
                    Vector2 normal2 = new Vector2(-tangent2.y, tangent2.x).normalized;

                    offsetDir = (normal1 + normal2) / 2;
                    offsetDir *= insetAmount / offsetDir.magnitude;
                }
                offsetPoly.Add(new Vector2(originalPoly[i].x - offsetDir.x, originalPoly[i].y - offsetDir.y));
            }

            return offsetPoly;
        }
    }
} // namespace TiltBrush
