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

using NUnit.Framework;
using TiltBrush.FrameAnimation;
using UnityEngine;

namespace TiltBrush.Tests
{
    public class TestFrameDrawingRepository
    {
        private CanvasScript m_Canvas;

        [SetUp]
        public void SetUp()
        {
            m_Canvas = CanvasScript.UnitTestSetUp(new GameObject("FrameDrawing repository canvas"));
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Canvas != null) CanvasScript.UnitTestTearDown(m_Canvas.gameObject);
        }

        [Test]
        public void GetOrCreatePreservesStableIdentityAndCanvasAdapter()
        {
            var repository = new FrameDrawingRepository();
            int idAllocations = 0;

            FrameDrawing first = repository.GetOrCreate(
                m_Canvas, () => new AnimationDrawingId(++idAllocations));
            FrameDrawing second = repository.GetOrCreate(
                m_Canvas, () => new AnimationDrawingId(++idAllocations));

            Assert.AreSame(first, second);
            Assert.AreEqual(1, idAllocations);
            Assert.AreEqual(new AnimationDrawingId(1), first.Id);
            Assert.AreSame(m_Canvas, first.Canvas);
            Assert.IsTrue(repository.TryGet(first.Id, out FrameDrawing byId));
            Assert.AreSame(first, byId);
            Assert.IsTrue(repository.TryGet(m_Canvas, out FrameDrawing byCanvas));
            Assert.AreSame(first, byCanvas);
        }

        [Test]
        public void ContentRevisionBelongsToLogicalDrawing()
        {
            var repository = new FrameDrawingRepository();
            FrameDrawing drawing = repository.GetOrCreate(
                m_Canvas, () => new AnimationDrawingId(7));

            drawing.MarkContentChanged();
            drawing.MarkContentChanged();

            Assert.AreEqual(2, drawing.ContentRevision);
            Assert.AreSame(m_Canvas, drawing.Canvas);
        }

        [Test]
        public void RemovingDrawingClearsBothLookupDirections()
        {
            var repository = new FrameDrawingRepository();
            FrameDrawing drawing = repository.GetOrCreate(
                m_Canvas, () => new AnimationDrawingId(11));

            Assert.IsTrue(repository.Remove(drawing.Id));

            Assert.AreEqual(0, repository.Count);
            Assert.IsFalse(repository.TryGet(drawing.Id, out _));
            Assert.IsFalse(repository.TryGet(m_Canvas, out _));
        }

        [Test]
        public void DevelopmentRepairRestoresOnlyMissingCanvasLookup()
        {
            var repository = new FrameDrawingRepository();
            FrameDrawing drawing = repository.GetOrCreate(
                m_Canvas, () => new AnimationDrawingId(13));
            Assert.IsTrue(repository.RemoveCanvasIndexForTests(m_Canvas));

            Assert.IsFalse(repository.TryGet(m_Canvas, out _));
            Assert.IsTrue(repository.TryGet(drawing.Id, out FrameDrawing retainedDrawing));
            Assert.AreSame(drawing, retainedDrawing);
            Assert.IsTrue(repository.TryRepairCanvasIndex(m_Canvas, out FrameDrawing repaired));
            Assert.AreSame(drawing, repaired);
            Assert.IsTrue(repository.TryGet(m_Canvas, out FrameDrawing byCanvas));
            Assert.AreSame(drawing, byCanvas);
        }
    }
}
