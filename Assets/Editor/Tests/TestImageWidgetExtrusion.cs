// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under
// the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
// ANY KIND, either express or implied. See the License for permissions and limitations.

using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    internal class TestImageWidgetExtrusion
    {
        [Test]
        public void ZeroSvgExtrusionRestoresFlatRendererAndClearsExtrudedMesh()
        {
            var root = new GameObject("SVG extrusion test");
            root.SetActive(false);
            var oldMesh = new Mesh();
            try
            {
                var widget = root.AddComponent<ImageWidget>();
                root.AddComponent<SpriteExtruder>();
                var extruded = root.AddComponent<MeshFilter>();
                extruded.sharedMesh = oldMesh;
                var flat = new GameObject("Flat SVG");
                flat.transform.SetParent(root.transform, false);
                var renderer = flat.AddComponent<MeshRenderer>();
                renderer.enabled = false;
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(GrabWidget).GetField("m_Mesh", fields).SetValue(widget, flat.transform);
                // Depth zero does not read the file; only its SVG extension is needed.
                typeof(ImageWidget).GetField("m_ReferenceImage", fields).SetValue(widget, new ReferenceImage("test.svg"));

                widget.SetExtrusion(0, Color.white);

                Assert.IsTrue(renderer.enabled);
                Assert.IsNull(extruded.sharedMesh);
                Assert.AreEqual(0, widget.ExtrusionDepth);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(oldMesh);
            }
        }
    }
}
