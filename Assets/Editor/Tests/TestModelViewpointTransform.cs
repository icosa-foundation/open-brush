// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    internal class TestModelViewpointTransform
    {
        [TestCase(0.25f, 2f)]
        [TestCase(10f, 2f)]
        [TestCase(1f, 2f)]
        [TestCase(3f, -2f)]
        public void RescalingPreservesViewpointPositionAndModelRotation(float factor, float initialScale)
        {
            var model = TrTransform.TRS(new Vector3(3, -2, 7), Quaternion.Euler(15, 63, -12), initialScale);
            var viewpointLocal = new Vector3(-4, 1.5f, 2);
            Vector3 viewpoint = model * viewpointLocal;

            var resized = ModelPlaybackController.ScaleModelAboutViewpoint(model, viewpoint, factor);

            Assert.That(Vector3.Distance(resized * viewpointLocal, viewpoint), Is.LessThan(0.0001f));
            Assert.That(resized.scale, Is.EqualTo(initialScale * factor).Within(0.0001f));
            Assert.That(Quaternion.Angle(resized.rotation, model.rotation), Is.LessThan(0.0001f));
        }
    }
}
