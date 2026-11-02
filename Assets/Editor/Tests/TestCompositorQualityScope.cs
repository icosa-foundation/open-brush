// Copyright 2026 The Open Brush Authors. Licensed under the Apache License, Version 2.0.
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    public class TestCompositorQualityScope
    {
        private GameObject m_Object;
        private OverlayManager m_Overlay;

        [SetUp]
        public void SetUp()
        {
            m_Object = new GameObject(nameof(TestCompositorQualityScope));
            m_Object.SetActive(false);
            m_Overlay = m_Object.AddComponent<OverlayManager>();
        }

        [TearDown]
        public void TearDown() { UnityEngine.Object.DestroyImmediate(m_Object); }

        private IDisposable BeginScope()
        {
            var method = typeof(OverlayManager).GetMethod("BeginCompositorOperation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return (IDisposable)method.Invoke(m_Overlay, null);
        }

        [Test]
        public void NestedScopesAndRenderingPauseKeepMeasurementBlocked()
        {
            Assert.IsFalse(m_Overlay.BlocksQualityMeasurement);
            var outer = BeginScope();
            var inner = BeginScope();
            Assert.IsTrue(m_Overlay.BlocksQualityMeasurement);
            outer.Dispose();
            outer.Dispose(); // Disposal must be idempotent, including coroutine cancellation.
            Assert.IsTrue(m_Overlay.BlocksQualityMeasurement);
            m_Overlay.PauseRendering(true);
            inner.Dispose();
            Assert.IsTrue(m_Overlay.BlocksQualityMeasurement);
            m_Overlay.PauseRendering(false);
            Assert.IsFalse(m_Overlay.BlocksQualityMeasurement);
        }

        [Test]
        public void ExceptionUnwindsMeasurementBlock()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                using var scope = BeginScope();
                Assert.IsTrue(m_Overlay.BlocksQualityMeasurement);
                throw new InvalidOperationException("Interrupted compositor work");
            });
            Assert.IsFalse(m_Overlay.BlocksQualityMeasurement);
        }
    }
}

