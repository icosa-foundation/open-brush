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
        public void ClassifierAcceptsSupportedAnimatedPathAndRetainsUnsupportedFallback()
        {
            Batch batch = new GameObject("Path proxy batch").AddComponent<Batch>();
            batch.transform.SetParent(m_Canvas.transform, false);
            var subset = new BatchSubset { m_ParentBatch = batch };
            var stroke = new Stroke
            {
                m_Type = Stroke.Type.BatchedBrushStroke,
                m_BatchSubset = subset,
            };

            FrameDrawingProxyCompatibility supported = FrameDrawingProxyClassifier.Classify(
                m_Drawing, new[] { stroke }, new GrabWidget[0], hasAnimatedPath: true,
                supportsAnimatedPath: true, supportsBatch: candidate => candidate == batch);
            FrameDrawingProxyCompatibility unsupported = FrameDrawingProxyClassifier.Classify(
                m_Drawing, new[] { stroke }, new GrabWidget[0], hasAnimatedPath: true,
                supportsAnimatedPath: false, supportsBatch: candidate => candidate == batch);

            Assert.IsTrue(supported.IsEligible);
            Assert.AreNotEqual(
                0, unsupported.Reasons & FrameDrawingProxyIncompatibility.AnimatedPath);
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

        [Test]
        public void CanvasBatchProxySharesRenderResourcesAndMatchesSourceMetrics()
        {
            GameObject batchObject = CreateTriangleRoot("Canvas proxy source batch");
            batchObject.transform.SetParent(m_Canvas.transform, false);
            batchObject.AddComponent<Batch>();
            Mesh sourceMesh = batchObject.GetComponent<MeshFilter>().sharedMesh;
            MeshRenderer sourceRenderer = batchObject.GetComponent<MeshRenderer>();
            batchObject.layer = 23;
            sourceRenderer.sortingLayerID = 0;
            sourceRenderer.sortingOrder = 23;
            sourceRenderer.receiveShadows = false;
            sourceRenderer.renderingLayerMask = 0x2a;
            var sourceProperties = new MaterialPropertyBlock();
            sourceProperties.SetFloat("_OBProxyTestValue", 17f);
            sourceRenderer.SetPropertyBlock(sourceProperties);
            var proxy = new CanvasBatchRenderProxy(trackId: 4);
            try
            {
                proxy.Synchronize(m_Drawing);
                proxy.SetVisible(true);

                Assert.AreEqual(m_Drawing.Id, proxy.DrawingId);
                Assert.IsTrue(proxy.IsVisible);
                Assert.AreEqual(
                    FrameDrawingRenderMetrics.CaptureBatches(m_Canvas), proxy.Metrics);
                Mesh proxyMesh = proxy.Root.GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.AreSame(sourceMesh, proxyMesh,
                    "The dormant proxy path must not duplicate source mesh memory");
                MeshRenderer proxyRenderer = proxy.Root.GetComponentInChildren<MeshRenderer>();
                Assert.AreEqual(batchObject.layer, proxyRenderer.gameObject.layer);
                Assert.AreEqual(sourceRenderer.sortingLayerID, proxyRenderer.sortingLayerID);
                Assert.AreEqual(sourceRenderer.sortingOrder, proxyRenderer.sortingOrder);
                Assert.AreEqual(sourceRenderer.receiveShadows, proxyRenderer.receiveShadows);
                Assert.AreEqual(sourceRenderer.renderingLayerMask, proxyRenderer.renderingLayerMask);
                var proxyProperties = new MaterialPropertyBlock();
                proxyRenderer.GetPropertyBlock(proxyProperties);
                Assert.AreEqual(17f, proxyProperties.GetFloat("_OBProxyTestValue"));
            }
            finally
            {
                proxy.Dispose();
                Object.DestroyImmediate(sourceMesh);
            }
        }

        [Test]
        public void CanvasBatchProxyPreservesDrawingAndSceneTransformComposition()
        {
            var sceneParent = new GameObject("Proxy scene parent");
            m_Canvas.transform.SetParent(sceneParent.transform, false);
            sceneParent.transform.SetPositionAndRotation(
                new Vector3(4f, -2f, 7f), Quaternion.Euler(10f, 25f, -15f));
            sceneParent.transform.localScale = Vector3.one * 1.2f;
            m_Canvas.transform.localPosition = new Vector3(1f, 2f, 3f);
            m_Canvas.transform.localRotation = Quaternion.Euler(5f, 35f, 12f);
            m_Canvas.transform.localScale = Vector3.one * 0.75f;
            GameObject batchObject = CreateTriangleRoot("Transformed source batch");
            batchObject.transform.SetParent(m_Canvas.transform, false);
            batchObject.transform.localPosition = new Vector3(-2f, 0.5f, 1f);
            batchObject.transform.localRotation = Quaternion.Euler(20f, 5f, 40f);
            Batch batch = batchObject.AddComponent<Batch>();
            var proxy = new CanvasBatchRenderProxy(trackId: 9);
            Mesh sourceMesh = batchObject.GetComponent<MeshFilter>().sharedMesh;
            try
            {
                proxy.Synchronize(m_Drawing);
                MeshFilter proxyFilter = proxy.Root.GetComponentInChildren<MeshFilter>(true);
                AssertTransformsMatch(batch.transform, proxyFilter.transform);

                m_Canvas.transform.localPosition = new Vector3(-3f, 4f, 2f);
                m_Canvas.transform.localRotation = Quaternion.Euler(-15f, 60f, 8f);
                m_Canvas.transform.localScale = Vector3.one * 1.3f;
                proxy.SynchronizeTransform();

                Assert.AreSame(sceneParent.transform, proxy.Root.transform.parent);
                AssertTransformsMatch(m_Canvas.transform, proxy.Root.transform);
                AssertTransformsMatch(batch.transform, proxyFilter.transform);
                Assert.AreEqual(m_Drawing.ContentRevision, proxy.SourceRevision,
                    "Transform-only synchronization must not rebuild drawing content");
            }
            finally
            {
                proxy.Dispose();
                m_Canvas.transform.SetParent(null, true);
                Object.DestroyImmediate(sceneParent);
                Object.DestroyImmediate(sourceMesh);
            }
        }

        private static void AssertTransformsMatch(Transform expected, Transform actual)
        {
            Assert.Less(Vector3.Distance(expected.position, actual.position), 0.0001f);
            Assert.Less(Quaternion.Angle(expected.rotation, actual.rotation), 0.01f);
            Assert.Less(Vector3.Distance(expected.lossyScale, actual.lossyScale), 0.0001f);
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
