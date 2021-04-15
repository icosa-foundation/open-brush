﻿// Copyright 2020 The Tilt Brush Authors
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
using UnityEngine;

namespace TiltBrush
{

    class SprayBrush : GeometryBrush
    {
        const int kVertsInSolid = 4;
        const int kTrisInSolid = 2;
        const int kMaxQuadsPerKnot = 500;

        // Values used to compute "salt"; see StatelessRng docs for more detail.
        private const int kSaltMaxQuadsPerKnot = 12;
        private const int kSaltMaxSaltsPerQuad = 10;
        private const int kSaltPressure = 0;
        private const int kSaltRotation = kSaltPressure + 1;
        private const int kSaltPosition = kSaltRotation + 1;
        private const int kSaltAlpha = kSaltPosition + 3;
        private const int kSaltAtlas = kSaltAlpha + 1;
        // next is kSaltAtlas + 1 (total used is 7)

        protected const int BR = 0; // back right  (top)
        protected const int BL = 1; // back left   (top)
        protected const int FR = 2; // front right (top)
        protected const int FL = 3; // front left  (top)

        private readonly Vector2 m_TextureAtlas00 = new Vector2(0.0f, 0.0f);
        private readonly Vector2 m_TextureAtlas05 = new Vector2(0.0f, 0.5f);
        private readonly Vector2 m_TextureAtlas50 = new Vector2(0.5f, 0.0f);
        private readonly Vector2 m_TextureAtlas55 = new Vector2(0.5f, 0.5f);

        private List<float> m_DecayTimers;
        // Number of knots popped off the front by DecayBrush()
        // Used to keep from reusing RNG values
        private int m_DecayedKnots;

        public SprayBrush()
            : base(bCanBatch: true,
                upperBoundVertsPerKnot: kVertsInSolid,
                bDoubleSided: true,
                bSmoothPositions: false)
        {
            m_DecayTimers = new List<float>();
            m_DecayedKnots = 0;
        }

        protected int CalculateSalt(int knotIndex, int quadIndex)
        {
            // Act as if the preview stroke is one very long stroke, and we're only generating
            // the geometry for the very tail end of it.
            int pretendKnotIndex = knotIndex + m_DecayedKnots;
            // If there are lots of quads in this knot, don't take random numbers from
            // adjacent knots; cycle around and reuse this knot's random numbers.
            return kSaltMaxSaltsPerQuad * (
                pretendKnotIndex * kSaltMaxQuadsPerKnot +
                quadIndex % kSaltMaxQuadsPerKnot);
        }

        override public float GetSpawnInterval(float pressure01)
        {
            return PressuredSize(pressure01) / m_Desc.m_SprayRateMultiplier;
        }

        override public bool AlwaysRebuildPreviewBrush()
        {
            return false;
        }

        protected override void InitBrush(BrushDescriptor desc, TrTransform localPointerXf)
        {
            base.InitBrush(desc, localPointerXf);
            SetDoubleSided(desc);
            m_DecayTimers.Clear();
            m_geometry.Layout = GetVertexLayout(desc);
        }

        override public GeometryPool.VertexLayout GetVertexLayout(BrushDescriptor desc)
        {
            return new GeometryPool.VertexLayout
            {
                uv0Size = 2,
                uv0Semantic = GeometryPool.Semantic.XyIsUv,
                uv1Size = 0,
                bUseNormals = true,
                bUseColors = true,
                bUseTangents = true,
            };
        }

        public override void DecayBrush()
        {
            int knotsToShift = 0;
            // Decay the preview by counting the number of m_DecayTimers that have expired
            // and deleting that many knots from the beginning.
            for (int i = 0; i < m_DecayTimers.Count; i++)
            {
                m_DecayTimers[i] += Time.deltaTime;
                if (m_DecayTimers[i] > kPreviewDuration)
                {
                    knotsToShift++;
                }
            }

            Debug.Assert(m_knots.Count - 2 == m_DecayTimers.Count);
            m_DecayTimers.RemoveRange(0, knotsToShift);

            RemoveInitialKnots(knotsToShift);

            m_DecayedKnots += knotsToShift;
        }

        public override void ResetBrushForPreview(TrTransform localPointerXf)
        {
            base.ResetBrushForPreview(localPointerXf);
            m_DecayTimers.Clear();
        }

        override protected bool UpdatePositionImpl(
            Vector3 pos, Quaternion ori, float pressure)
        {
            bool keep = base.UpdatePositionImpl(pos, ori, pressure);
            if (keep && m_PreviewMode)
            {
                m_DecayTimers.Add(0);
            }
            return keep;
        }

        override protected void ControlPointsChanged(int iKnot0)
        {
            // Frames knots, determines how much geometry each knot should get
            OnChanged_FrameKnots(iKnot0);
            ResizeGeometry();
            OnChanged_MakeGeometry(iKnot0);

            OnChanged_UVs(iKnot0);
            OnChanged_Tangents(iKnot0);
        }

        public override bool NeedsStraightEdgeProxy()
        {
            return true;
        }

        void OnChanged_FrameKnots(int iKnot0)
        {
            Knot prev = m_knots[iKnot0 - 1];
            for (int iKnot = iKnot0; iKnot < m_knots.Count; ++iKnot)
            {
                Knot cur = m_knots[iKnot];
                cur.iTri = prev.iTri + prev.nTri;
                cur.iVert = (ushort)(prev.iVert + prev.nVert);

                Vector3 vMove = cur.point.m_Pos - prev.point.m_Pos;
                cur.length = vMove.magnitude;
                float fMinDistanceToSpawn = GetSpawnInterval(cur.smoothedPressure);

                if (cur.length < fMinDistanceToSpawn)
                {
                    cur.nTri = 0;
                    cur.nVert = 0;
                    cur.nRight = cur.nSurface = Vector3.zero;
                }
                else
                {
                    Vector3 vFacing = vMove.normalized;
                    ComputeSurfaceFrameNew(Vector3.zero, vFacing, cur.point.m_Orient,
                        out cur.nRight, out cur.nSurface);

                    int iNumQuads = Math.Min((int)(cur.length / fMinDistanceToSpawn), GetNumQuadsAllowed());
                    cur.nTri = (ushort)(iNumQuads * kTrisInSolid * NS);
                    cur.nVert = (ushort)(iNumQuads * kVertsInSolid * NS);
                }

                m_knots[iKnot] = cur;
                prev = cur;
            }
        }

        void OnChanged_MakeGeometry(int iKnot0)
        {
            Knot prev = m_knots[iKnot0 - 1];
            for (int iKnot = iKnot0; iKnot < m_knots.Count; ++iKnot)
            {
                Knot cur = m_knots[iKnot];

                if (cur.HasGeometry)
                {
                    int iNumQuads = cur.nTri / (kTrisInSolid * NS);

                    Vector3 vMoveDirection = (cur.point.m_Pos - prev.point.m_Pos).normalized;
                    float fMinDistanceToSpawn = GetSpawnInterval(cur.smoothedPressure);

                    Vector3 vLastSpawnPos = prev.point.m_Pos;

                    int iVertIndex = cur.iVert;
                    int iTriIndex = cur.iTri;
                    float alpha = PressuredOpacity(cur.smoothedPressure);

                    for (int i = 0; i < iNumQuads; i++)
                    {
                        int salt = CalculateSalt(iKnot, i);
                        Vector3 vCenter = vLastSpawnPos;
                        Vector3 vRight = cur.nRight;
                        Vector3 vFacing = vMoveDirection;

                        float rotationVariance = m_Desc.m_RotationVariance;
                        if (rotationVariance > 0.0001f)
                        {
                            Quaternion qRotate = Quaternion.AngleAxis(
                                m_rng.InRange(salt + kSaltRotation,
                                    -rotationVariance, rotationVariance), cur.nSurface);

                            vRight = qRotate * vRight;
                            vFacing = qRotate * vFacing;
                        }

                        float fSize = PressuredRandomSize(cur.smoothedPressure, salt + kSaltPressure);
                        Vector3 vForwardOffset = vFacing * fSize * m_Desc.m_SizeRatio.x * 0.5f;
                        Vector3 vRightOffset = vRight * fSize * m_Desc.m_SizeRatio.y * 0.5f;
                        vCenter += fSize * m_Desc.m_PositionVariance * m_rng.InUnitSphere(salt + kSaltPosition);

                        SetTri(iTriIndex, iVertIndex, 0, BR, BL, FL);
                        SetTri(iTriIndex, iVertIndex, 1, BR, FL, FR);

                        if (m_Desc.m_RandomizeAlpha)
                        {
                            alpha = m_rng.InRange(salt + kSaltAlpha, 0.0f, 1.0f);
                        }

                        SetVert(iVertIndex, BR, vCenter - vForwardOffset + vRightOffset, cur.nSurface, m_Color,
                            alpha);
                        SetVert(iVertIndex, BL, vCenter - vForwardOffset - vRightOffset, cur.nSurface, m_Color,
                            alpha);
                        SetVert(iVertIndex, FR, vCenter + vForwardOffset + vRightOffset, cur.nSurface, m_Color,
                            alpha);
                        SetVert(iVertIndex, FL, vCenter + vForwardOffset - vRightOffset, cur.nSurface, m_Color,
                            alpha);

                        iTriIndex += kTrisInSolid * NS;
                        iVertIndex += kVertsInSolid * NS;

                        vLastSpawnPos += vMoveDirection * fMinDistanceToSpawn;
                    }
                }
                prev = cur;
            }
        }

        void OnChanged_UVs(int iKnot0)
        {
            for (int iKnot = iKnot0; iKnot < m_knots.Count; ++iKnot)
            {
                Knot cur = m_knots[iKnot];

                if (cur.HasGeometry)
                {
                    int iQuad = 0;
                    for (int iVertIndex = cur.iVert;
                         iVertIndex < cur.iVert + cur.nVert;
                         iVertIndex += kVertsInSolid * NS)
                    {
                        int salt = CalculateSalt(iKnot, iQuad);
                        // XXX: ignores the actual value of m_TextureAtlasV; we should add
                        // m_TextureAtlasU to go with m_TextureAtlasV, and use those
                        if (m_Desc.m_TextureAtlasV > 1)
                        {
                            int rand = m_rng.InIntRange(salt + kSaltAtlas, 0, 4);
                            Vector2 offset = m_TextureAtlas00;
                            if (rand == 1) { offset = m_TextureAtlas50; }
                            else if (rand == 2) { offset = m_TextureAtlas05; }
                            else if (rand == 3) { offset = m_TextureAtlas55; }

                            SetUv0(iVertIndex, BL, m_TextureAtlas00 + offset);
                            SetUv0(iVertIndex, FL, m_TextureAtlas50 + offset);
                            SetUv0(iVertIndex, BR, m_TextureAtlas05 + offset);
                            SetUv0(iVertIndex, FR, m_TextureAtlas55 + offset);
                        }
                        else
                        {
                            SetUv0(iVertIndex, BL, new Vector2(0, 0));
                            SetUv0(iVertIndex, FL, new Vector2(1, 0));
                            SetUv0(iVertIndex, BR, new Vector2(0, 1));
                            SetUv0(iVertIndex, FR, new Vector2(1, 1));
                        }
                        iQuad += 1;
                    }
                }
            }
        }

        void OnChanged_Tangents(int iKnot0)
        {
            for (int iKnot = iKnot0; iKnot < m_knots.Count; ++iKnot)
            {
                Knot cur = m_knots[iKnot];

                if (cur.HasGeometry)
                {
                    for (int iVertIndex = cur.iVert; iVertIndex < cur.iVert + cur.nVert; iVertIndex += kVertsInSolid * NS)
                    {
                        Vector3 vBL = m_geometry.m_Vertices[iVertIndex + BL * NS];
                        Vector3 vFL = m_geometry.m_Vertices[iVertIndex + FL * NS];
                        Vector3 vFacing = vFL - vBL;

                        SetTangent(iVertIndex, BL, vFacing);
                        SetTangent(iVertIndex, BR, vFacing);
                        SetTangent(iVertIndex, FL, vFacing);
                        SetTangent(iVertIndex, FR, vFacing);
                    }
                }
            }
        }

        int GetNumQuadsAllowed()
        {
            // Check if we have room for one more stride's worth of verts.
            // The maximum index in Unity is 0xfffe.
            int MAX_NUM_VERTS = 0xffff;
            int iNumQuadsAllowed = (int)((MAX_NUM_VERTS - GetNumUsedVerts()) / (kVertsInSolid * NS));
            iNumQuadsAllowed = Math.Min(iNumQuadsAllowed, kMaxQuadsPerKnot);
            return iNumQuadsAllowed;
        }
    }
} // namespace TiltBrush
