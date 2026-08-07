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

using System;
using System.Reflection;
using Gsplat;
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    internal class TestGsplatModelOwnership
    {
        private static readonly FieldInfo sm_OwnedGsplatAssetField =
            typeof(Model).GetField("m_OwnedGsplatAsset",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo sm_GetGsplatSourceCoordinates =
            typeof(Model).GetMethod("GetGsplatSourceCoordinates",
                BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo sm_ReleaseFromCatalog =
            typeof(Model).GetMethod("ReleaseFromCatalog",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo sm_ReleaseUsage =
            typeof(Model).GetMethod("ReleaseUsage",
                BindingFlags.Instance | BindingFlags.NonPublic);

        [TestCase(".ply", SourceCoordinates.RUB)]
        [TestCase(".spz", SourceCoordinates.RUB)]
        [TestCase(".sog", SourceCoordinates.RDB)]
        public void GsplatFormatsRetainMigrationCoordinatePolicy(
            string extension, SourceCoordinates expected)
        {
            Assert.IsNotNull(sm_GetGsplatSourceCoordinates);
            Assert.AreEqual(expected,
                sm_GetGsplatSourceCoordinates.Invoke(null, new object[] { extension }));
        }

        [Test]
        public void RepeatedUnloadDestroysEachOwnedRuntimeAsset()
        {
            Assert.IsNotNull(sm_OwnedGsplatAssetField);
            var model = new Model($"gsplat-ownership-{Guid.NewGuid():N}.ply");

            for (int iteration = 0; iteration < 2; ++iteration)
            {
                var asset = ScriptableObject.CreateInstance<GsplatAssetSpark>();
                try
                {
                    model.m_Valid = true;
                    sm_OwnedGsplatAssetField.SetValue(model, asset);

                    model.UnloadModel();

                    Assert.IsNull(model.m_ModelParent);
                    Assert.IsFalse(model.m_Valid);
                    Assert.IsTrue(asset == null,
                        $"Runtime splat asset from unload iteration {iteration} was not destroyed.");
                    Assert.IsNull(sm_OwnedGsplatAssetField.GetValue(model));
                }
                finally
                {
                    if (asset != null)
                    {
                        UnityEngine.Object.DestroyImmediate(asset);
                    }
                }
            }
        }

        [Test]
        public void CatalogRemovalDefersAssetDestructionUntilLastBorrowerReleases()
        {
            Assert.IsNotNull(sm_ReleaseFromCatalog);
            Assert.IsNotNull(sm_ReleaseUsage);
            var model = new Model($"gsplat-catalog-release-{Guid.NewGuid():N}.ply");
            var asset = ScriptableObject.CreateInstance<GsplatAssetSpark>();
            try
            {
                model.m_UsageCount = 1;
                sm_OwnedGsplatAssetField.SetValue(model, asset);

                sm_ReleaseFromCatalog.Invoke(model, null);

                Assert.IsTrue(asset != null,
                    "Catalog removal destroyed an asset while a widget still borrowed it.");
                Assert.AreSame(asset, sm_OwnedGsplatAssetField.GetValue(model));

                sm_ReleaseUsage.Invoke(model, null);

                Assert.AreEqual(0, model.m_UsageCount);
                Assert.IsTrue(asset == null,
                    "The final borrower release did not destroy the catalog-orphaned asset.");
                Assert.IsNull(sm_OwnedGsplatAssetField.GetValue(model));
            }
            finally
            {
                if (asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
            }
        }
    }
}
