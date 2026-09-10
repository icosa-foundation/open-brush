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
using System.IO;
using NUnit.Framework;

namespace TiltBrush
{
    internal class TestModelLocation
    {
        [Test]
        public void SafMaterialization_UsesCachePathWithoutChangingPersistentPath()
        {
            const string relativePath = "Nested/model.glb";
            string cachePath = Path.Combine(Path.GetTempPath(), "saf-document", relativePath);
            Model.Location location = Model.Location.File(relativePath, cachePath);

            Assert.AreEqual(cachePath.Replace("\\", "/"), location.AbsolutePath);
            Assert.AreEqual(relativePath, location.RelativePath);
            Assert.AreEqual(".glb", location.Extension);
            Assert.AreEqual(Model.Location.Type.LocalFile, location.GetLocationType());
        }

        [Test]
        public void SafMaterialization_DoesNotChangeLocationIdentity()
        {
            const string relativePath = "Nested/model.glb";
            Model.Location original = Model.Location.File(relativePath);
            Model.Location materialized = Model.Location.File(
                relativePath, Path.Combine(Path.GetTempPath(), "saf-document", relativePath));
            var references = new Dictionary<Model.Location, string> { [original] = "saved widget" };

            Assert.AreEqual(original, materialized);
            Assert.AreEqual(original.GetHashCode(), materialized.GetHashCode());
            Assert.AreEqual(original.ToString(), materialized.ToString());
            Assert.AreEqual("saved widget", references[materialized]);
        }
    }
}
