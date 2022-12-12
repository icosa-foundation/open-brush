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
    public class GuideBeam
    {
        public Transform m_Beam;
        public SymmetryWidget.BeamDirection m_Direction;
        [NonSerialized] public Renderer m_BeamRenderer;
        [NonSerialized] public Vector3 m_Offset;
        [NonSerialized] public Vector3 m_BaseScale;
    }

    public class SymmetryWidget : GrabWidget
    {
        [SerializeField] private Renderer m_LeftRightMesh;
        [SerializeField] private Renderer m_FrontBackMesh;
        [SerializeField] private TextMeshPro m_TitleText;
        [SerializeField] private GameObject m_HintText;
        [SerializeField] private GrabWidgetHome m_Home;

        [SerializeField] private Mesh m_CustomSymmetryMesh;
        [SerializeField] private Material m_CustomSymmetryMaterial;

        public enum BeamDirection
        {
            Up,
            Down,
            Left,
            Right,
            Front,
            Back
        }
        [SerializeField] private GuideBeam[] m_GuideBeams;
        [SerializeField] private float m_GuideBeamLength;
        private float m_GuideBeamShowRatio;

        [SerializeField] private Color m_SnapColor;
        [SerializeField] private float m_SnapOrientationSpeed = 0.2f;

        [SerializeField] private float m_SnapAngleXZPlane = 45.0f;
        [SerializeField] private float m_SnapXZPlaneStickyAmount;
        private float m_SnapQuantizeAmount = 15.0f;
        private float m_SnapStickyAngle = 1.0f;

        [SerializeField] private float m_JumpToUserControllerOffsetDistance;
        [SerializeField] private float m_JumpToUserControllerYOffset;
        private static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        [SerializeField] private Transform m_SymmetryDomainPrefab;
        [SerializeField] private Transform m_SymmetryDomainParent;


        public Plane ReflectionPlane
        {
            get
            {
                return new Plane(transform.right, transform.position);
            }
        }

        public override Vector3 CustomDimension
        {
            get { return m_AngularVelocity_LS; }
            set
            {
                m_AngularVelocity_LS = value;
                m_IsSpinningFreely = value.magnitude > m_AngVelDampThreshold;
            }
        }

        override protected void Awake()
        {
            base.Awake();

            m_AngVelDampThreshold = 50f;

            //initialize beams
            for (int i = 0; i < m_GuideBeams.Length; ++i)
            {
                m_GuideBeams[i].m_Offset = m_GuideBeams[i].m_Beam.position - transform.position;
                m_GuideBeams[i].m_BaseScale = m_GuideBeams[i].m_Beam.localScale;
                m_GuideBeams[i].m_BeamRenderer = m_GuideBeams[i].m_Beam.GetComponent<Renderer>();
                m_GuideBeams[i].m_BeamRenderer.enabled = false;
            }

            m_GuideBeamShowRatio = 0.0f;

            m_Home.Init();
            m_Home.SetOwner(transform);
            m_Home.SetFixedPosition(App.Scene.AsScene[transform].translation);

            m_CustomShowHide = true;
        }

        public void SetMode(PointerManager.SymmetryMode rMode)
        {
            switch (rMode)
            {
                case PointerManager.SymmetryMode.SinglePlane:
                    m_LeftRightMesh.enabled = false;
                    for (int i = 0; i < m_GuideBeams.Length; ++i)
                    {
                        m_GuideBeams[i].m_BeamRenderer.enabled = ((m_GuideBeams[i].m_Direction != BeamDirection.Left) &&
                            (m_GuideBeams[i].m_Direction != BeamDirection.Right));
                    }
                    break;
                case PointerManager.SymmetryMode.TwoHanded:
                case PointerManager.SymmetryMode.MultiMirror:
                    m_LeftRightMesh.enabled = false;
                    m_FrontBackMesh.enabled = true;
                    for (int i = 0; i < m_GuideBeams.Length; ++i)
                    {
                        m_GuideBeams[i].m_BeamRenderer.enabled = false;
                    }
                    if (PointerManager.m_Instance.m_CustomSymmetryType == PointerManager.CustomSymmetryType.Point)
                    {
                    }
                    break;
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
            if (bInTransition || transform.hasChanged)
            {
                for (int i = 0; i < m_GuideBeams.Length; ++i)
                {
                    Vector3 vTransformedOffset = transform.rotation * m_GuideBeams[i].m_Offset;
                    Vector3 vGuideBeamPos = transform.position + vTransformedOffset;
                    Vector3 vGuideBeamDir = GetBeamDirection(m_GuideBeams[i].m_Direction);

                    float fBeamLength = m_GuideBeamLength * App.METERS_TO_UNITS;
                    fBeamLength *= fShowRatio;

                    //position guide beam half way to hit point
                    Vector3 vHitPoint = vGuideBeamPos + (vGuideBeamDir * fBeamLength);
                    Vector3 vHalfWay = (vGuideBeamPos + vHitPoint) * 0.5f;
                    m_GuideBeams[i].m_Beam.position = vHalfWay;

                    //set scale to half the distance
                    Vector3 vScale = m_GuideBeams[i].m_BaseScale;
                    vScale.y = fBeamLength * 0.5f;

                    m_GuideBeams[i].m_Beam.localScale = vScale;
                }

                transform.hasChanged = false;
                m_GuideBeamShowRatio = fShowRatio;
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
                    m_GuideBeams[i].m_BeamRenderer.material.color = m_SnapColor;
                }
            }
            m_HintText.SetActive(bActive);
            m_TitleText.color = bActive ? Color.white : Color.grey;
        }

        Vector3 GetBeamDirection(BeamDirection rDir)
        {
            switch (rDir)
            {
                case BeamDirection.Up: return transform.up;
                case BeamDirection.Down: return -transform.up;
                case BeamDirection.Left: return -transform.right;
                case BeamDirection.Right: return transform.right;
                case BeamDirection.Front: return transform.forward;
                case BeamDirection.Back: return -transform.forward;
            }
            return transform.up;
        }

        override protected void OnUserBeginInteracting()
        {
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

            lrs = m_SymmetryDomainParent.GetComponentsInChildren<LineRenderer>().ToList();
            foreach (var lr in lrs)
            {
                lr.gameObject.SetActive(false);
            }

            for (var i = 0; i < matrices.Count; i++)
            {
                var m0 = matrices[i];
                var m = transform.localToWorldMatrix * m0;
                // Scale the guides away from the origin
                m *= Matrix4x4.TRS(new Vector3(2, .5f, .05f), Quaternion.identity, new Vector3(0.5f, 0.4f, 0));
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
