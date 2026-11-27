// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TiltBrush
{
    public class TestNativeImagePlacement
    {
        [TestCase(0.001f)]
        [TestCase(5000f)]
        public void ImportedImageKeepsAuthoredHeightInSavedPose(float height)
        {
            var parent = new GameObject("Image placement test");
            parent.SetActive(false);
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<ImageWidget>("Assets/Prefabs/ImageWidget.prefab");
                var image = Object.Instantiate(prefab, parent.transform);
                image.SetAspectRatioOverride(2f);
                var pose = TrTransform.TRS(Vector3.right, Quaternion.Euler(0, 30, 0), height);
                image.LocalTransform = pose;
                Assert.AreNotEqual(height, image.GetSignedWidgetSize());
                image.RestoreImagePose(pose);
                Assert.AreEqual(height, image.SaveTransform.scale);
                Assert.AreEqual(pose.translation, image.SaveTransform.translation);
                Assert.Less(Quaternion.Angle(pose.rotation, image.SaveTransform.rotation), 0.01f);
            }
            finally { Object.DestroyImmediate(parent); }
        }
    }
}
