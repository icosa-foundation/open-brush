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

using System.Collections.Generic;
using NUnit.Framework;
using TiltBrush.FrameAnimation;
using UnityEngine;

namespace TiltBrush.Tests
{
    public class TestFrameDrawingRenderProxy
    {
        private CanvasScript m_Canvas;
        private FrameDrawing m_Drawing;

        [SetUp]
        public void SetUp()
        {
            m_Canvas = new GameObject("Proxy infrastructure canvas").AddComponent<CanvasScript>();
            m_Drawing = new FrameDrawingRepository().GetOrCreate(
                m_Canvas, () => new AnimationDrawingId(1));
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Canvas != null) Object.DestroyImmediate(m_Canvas.gameObject);
        }

        [Test]
        public void ClassifierAcceptsPureBatchedDrawingWhenBatchPolicySupportsIt()
        {
            Batch batch = new GameObject("Supported proxy batch").AddComponent<Batch>();
            batch.transform.SetParent(m_Canvas.transform, false);
            var subset = new BatchSubset { m_ParentBatch = batch };
            var stroke = new Stroke { m_Type = Stroke.Type.BatchedBrushStroke, m_BatchSubset = subset };

            FrameDrawingProxyCompatibility result = FrameDrawingProxyClassifier.Classify(
                m_Drawing, new[] { stroke }, new GrabWidget[0], hasAnimatedPath: false,
                supportsBatch: candidate => candidate == batch);

            Assert.IsTrue(result.IsEligible);
            Assert.AreEqual(1, result.StrokeCount);
            Assert.AreEqual(1, result.BatchCount);
        }

        [Test]
        public void ClassifierReportsIndependentFallbackReasons()
        {
            var unbatchedStroke = new Stroke
            {
                m_Type = Stroke.Type.NotCreated,
                m_IntendedCanvas = m_Canvas,
            };
            var widgetObject = new GameObject("Unsupported proxy widget");
            widgetObject.transform.SetParent(m_Canvas.transform, false);
            GrabWidget widget = widgetObject.AddComponent<GrabWidget>();

            FrameDrawingProxyCompatibility result = FrameDrawingProxyClassifier.Classify(
                m_Drawing, new[] { unbatchedStroke }, new[] { widget }, hasAnimatedPath: true);

            Assert.IsFalse(result.IsEligible);
            Assert.AreNotEqual(
                0, result.Reasons & FrameDrawingProxyIncompatibility.NonBatchedStroke);
            Assert.AreNotEqual(
                0, result.Reasons & FrameDrawingProxyIncompatibility.WidgetContent);
            Assert.AreNotEqual(
                0, result.Reasons & FrameDrawingProxyIncompatibility.AnimatedPath);
        }

        [Test]
        public void ResourceOwnerDestroysOnlyResourcesStillOwned()
        {
            var resources = new FrameDrawingProxyResources();
            GameObject destroyed = resources.Own(new GameObject("Owned proxy resource"));
            GameObject released = resources.Own(new GameObject("Released proxy resource"));
            Assert.IsTrue(resources.Release(released));

            resources.Dispose();

            Assert.IsTrue(destroyed == null);
            Assert.IsFalse(released == null);
            Object.DestroyImmediate(released);
        }

        [Test]
        public void ComparisonDetectsGeometryAndRendererMismatch()
        {
            GameObject source = CreateTriangleRoot("Source proxy metrics");
            GameObject equivalent = CreateTriangleRoot("Equivalent proxy metrics");
            GameObject missing = new GameObject("Missing proxy metrics");
            try
            {
                FrameDrawingRenderMetrics sourceMetrics =
                    FrameDrawingRenderMetrics.Capture(source, batches: 1);
                FrameDrawingRenderMetrics equivalentMetrics =
                    FrameDrawingRenderMetrics.Capture(equivalent, batches: 1);
                FrameDrawingRenderMetrics missingMetrics =
                    FrameDrawingRenderMetrics.Capture(missing, batches: 0);

                Assert.IsTrue(new FrameDrawingRenderComparison(
                    sourceMetrics, equivalentMetrics).Matches);
                Assert.IsFalse(new FrameDrawingRenderComparison(
                    sourceMetrics, missingMetrics).Matches);
                Assert.AreEqual(3, sourceMetrics.Vertices);
                Assert.AreEqual(3, sourceMetrics.Indices);
            }
            finally
            {
                DestroyMetricsRoot(source);
                DestroyMetricsRoot(equivalent);
                DestroyMetricsRoot(missing);
            }
        }

        private static GameObject CreateTriangleRoot(string name)
        {
            var root = new GameObject(name);
            MeshFilter filter = root.AddComponent<MeshFilter>();
            root.AddComponent<MeshRenderer>();
            var mesh = new Mesh { name = $"{name} mesh" };
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            filter.sharedMesh = mesh;
            return root;
        }

        private static void DestroyMetricsRoot(GameObject root)
        {
            if (root == null) return;
            MeshFilter filter = root.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                Object.DestroyImmediate(filter.sharedMesh);
            }
            Object.DestroyImmediate(root);
        }
    }
}
