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
using System.Collections.Generic;
using ImmPlayer.Exporter;
using NUnit.Framework;
using TiltBrush.FrameAnimation;
using UnityEngine;

namespace TiltBrush.Tests
{
    internal class TestImmAnimationTrackSnapshot
    {
        [TestCase(Quill.BRUSH_QUILL_CYLINDER, BrushSectionType.Circle)]
        [TestCase(Quill.BRUSH_QUILL_ELLIPSE, BrushSectionType.Ellipse)]
        [TestCase(Quill.BRUSH_QUILL_CUBE, BrushSectionType.Square)]
        [TestCase(Quill.BRUSH_QUILL_RIBBON, BrushSectionType.Segment)]
        public void ProjectsCanvasGeometryAndEffectivePerPointColor(string guid, BrushSectionType section)
        {
            var stroke = MakeStroke(guid);
            stroke.m_ControlPointsToDrop = new[] { false, true, false };
            stroke.m_ColorOverrideMode = ColorOverrideMode.Replace;
            stroke.m_OverrideColors = new List<Color32?> { null, null, new Color32(128, 64, 32, 96) };

            Assert.IsTrue(ImmAnimationTrackSnapshot.TryProjectStroke(stroke, out var paint, out float biggest));
            Assert.AreEqual(section, paint.Brush);
            Assert.AreEqual(VisibilityType.Always, paint.Visibility);
            Assert.AreEqual(2, paint.Points.Length);
            Assert.AreEqual(0.5f, paint.Points[0].Width);
            Assert.AreEqual(1f, biggest);
            var end = paint.Points[1];
            Assert.AreEqual(new Vector3(1, 2, -3), end.Position);
            Assert.AreEqual(Vector3.back, end.Direction);
            Assert.AreEqual(Vector3.up, end.Normal);
            Assert.AreEqual(1f, end.Width);
            Assert.AreEqual(2f, end.Length);
            Assert.That(end.Time, Is.EqualTo(0.02f).Within(1e-6f));
            Assert.That(end.Color.r, Is.EqualTo(Mathf.GammaToLinearSpace(128f / 255f)).Within(1e-6f));
            Assert.That(end.Alpha, Is.EqualTo(96f / 255f).Within(1e-6f));
        }

        [Test]
        public void EmptyBatchedQuillStrokesStayInvisibleDuringNativePlayback()
        {
            var stroke = MakeStroke(Quill.BRUSH_QUILL_RIBBON);
            stroke.m_Type = Stroke.Type.BatchedBrushStroke;
            stroke.m_BatchSubset = new BatchSubset { m_Active = true };
            Assert.IsFalse(ImmAnimationTrackSnapshot.IncludeSourceStroke(stroke));
            stroke.m_BatchSubset.m_VertLength = 4;
            Assert.IsFalse(ImmAnimationTrackSnapshot.IncludeSourceStroke(stroke));
            stroke.m_BatchSubset.m_nTriIndex = 6;
            Assert.IsTrue(ImmAnimationTrackSnapshot.IncludeSourceStroke(stroke));
            stroke.m_BatchSubset.m_Active = false;
            Assert.IsFalse(ImmAnimationTrackSnapshot.IncludeSourceStroke(stroke));

            stroke.m_BrushGuid = Guid.Empty;
            stroke.m_BatchSubset = new BatchSubset { m_Active = true };
            Assert.IsTrue(ImmAnimationTrackSnapshot.IncludeSourceStroke(stroke),
                "An unsupported active stroke must still make the track fall back.");
        }

        [Test]
        public void RecolorRebuildsWhenAStrokeWasMissingFromThePreviousProjection()
        {
            var go = new GameObject("Projection test canvas");
            go.SetActive(false);
            try
            {
                var canvas = go.AddComponent<CanvasScript>();
                var id = new AnimationDrawingId(1);
                var drawing = new FrameDrawing(id, canvas);
                var track = new AnimationTimelineModel.Track(1, true, false, 1,
                    new List<AnimationTimelineModel.Span>
                    {
                        new AnimationTimelineModel.Span(0, 1, new AnimationTimelineModel.FrameValue(id))
                    });
                var first = MakeStroke(Quill.BRUSH_QUILL_CYLINDER);
                var added = MakeStroke(Quill.BRUSH_QUILL_CYLINDER);
                first.m_IntendedCanvas = added.m_IntendedCanvas = canvas;
                var visible = new List<Stroke> { first };
                Assert.IsTrue(ImmAnimationTrackSnapshot.TryCreate(track, _ => drawing, _ => visible,
                    _ => false, out var previous));
                long before = drawing.ContentRevision;
                drawing.MarkContentChanged();
                visible.Add(added);
                var hint = default(ImmAnimationTrackSnapshot.Recolor).Advance(added, id, before, drawing.ContentRevision);
                Assert.IsTrue(ImmAnimationTrackSnapshot.TryCreate(track, _ => drawing, _ => visible,
                    _ => false, out var current, previous, recolor: hint));
                Assert.AreEqual(2, current.Drawings[0].Strokes.Length);
                Assert.AreEqual(1, previous.Drawings[0].Strokes.Length);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void RejectsUnsupportedBrushAndTiltedSections()
        {
            var stroke = MakeStroke(Quill.BRUSH_QUILL_RIBBON);
            stroke.m_BrushGuid = Guid.Empty;
            Assert.IsFalse(ImmAnimationTrackSnapshot.TryProjectStroke(stroke, out _, out _));
            stroke.m_BrushGuid = new Guid(Quill.BRUSH_QUILL_RIBBON);
            stroke.m_ControlPoints[1].m_Orient = new Quaternion(0, 0.70710677f, 0, 0.70710677f);
            Assert.IsFalse(ImmAnimationTrackSnapshot.TryProjectStroke(stroke, out _, out _));
        }

        private static Stroke MakeStroke(string guid)
        {
            var points = new PointerManager.ControlPoint[3];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new PointerManager.ControlPoint
                {
                    m_Pos = new Vector3(1, 2, i + 1),
                    m_Orient = Quaternion.identity,
                    m_Pressure = 0.5f + i * 0.25f,
                    m_TimestampMs = (uint)(100 + i * 10)
                };
            }
            return new Stroke
            {
                m_BrushGuid = new Guid(guid), m_BrushSize = 0.5f, m_BrushScale = 4,
                m_Color = Color.white, m_ControlPoints = points
            };
        }
    }
}
