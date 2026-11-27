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

namespace TiltBrush
{
    internal class TestVoxImportOptions
    {
        [Test]
        public void VoxImportOptions_DefaultsMatchLegacyBehavior()
        {
            var options = new VoxImportOptions();

            Assert.AreEqual(VoxImporter.MeshMode.Optimized, options.MeshMode);
            Assert.IsNull(options.MaterialOverride);
            Assert.IsTrue(options.GenerateCollider);
            Assert.IsNull(options.RootObjectName);
        }

        [Test]
        public void VoxImportOptions_ExplicitOverridesPersist()
        {
            var options = new VoxImportOptions
            {
                MeshMode = VoxImporter.MeshMode.SeparateCubes,
                GenerateCollider = false,
                RootObjectName = "custom-root"
            };

            Assert.AreEqual(VoxImporter.MeshMode.SeparateCubes, options.MeshMode);
            Assert.IsFalse(options.GenerateCollider);
            Assert.AreEqual("custom-root", options.RootObjectName);
        }
    }
}
