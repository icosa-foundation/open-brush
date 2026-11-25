// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    internal class TestSdfBoxContact
    {
        [TestCase(0f)]
        [TestCase(37f)]
        public void InsideEdgeProjectsToNearestFaceWithoutSlidingAlongEdge(float angle)
        {
            var box = new GameObject("Contact test box");
            try
            {
                var frame = box.transform;
                frame.SetPositionAndRotation(new Vector3(1, -2, 3), Quaternion.Euler(angle, angle, 0));
                frame.localScale = Vector3.one * 1.5f;
                var halfWidth = new Vector3(1, .7f, .65f);
                foreach (float delta in new[] { -.001f, 0f, .001f })
                {
                    var local = new Vector3(halfWidth.x - .02f + delta, .113f, halfWidth.z - .02f);
                    SdfStencil.ProjectToBox(frame, halfWidth, frame.TransformPoint(local),
                        out Vector3 point, out Vector3 normal);
                    Vector3 snap = frame.InverseTransformPoint(point);
                    Assert.That(snap.y, Is.EqualTo(local.y).Within(1e-6f));
                    Assert.That(Vector3.Distance(snap, local),
                        Is.EqualTo(Mathf.Min(halfWidth.x - local.x, halfWidth.z - local.z)).Within(1e-6f));
                    Assert.That(Mathf.Min(Mathf.Abs(snap.x - halfWidth.x), Mathf.Abs(snap.z - halfWidth.z)),
                        Is.LessThan(1e-6f));
                    Assert.That(Vector3.Dot(normal, frame.TransformDirection((snap - local).normalized)),
                        Is.GreaterThan(.9999f));
                }
            }
            finally
            {
                Object.DestroyImmediate(box);
            }
        }
    }
}
