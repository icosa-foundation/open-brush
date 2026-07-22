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
using System.Reflection;
using Icosa.OpenBlocks.FileFormat;
using NUnit.Framework;
using TiltBrush.MeshEditing;
using UnityEngine;
using BlocksFace = Icosa.OpenBlocks.FileFormat.Face;
using BlocksVertex = Icosa.OpenBlocks.FileFormat.Vertex;

namespace TiltBrush
{
    internal class TestBlocksReader
    {
        private static readonly MethodInfo sm_GetPreferredBlocksModelFile =
            typeof(ModelCatalog).GetMethod(
                "GetPreferredBlocksModelFile", BindingFlags.Static | BindingFlags.NonPublic);

        [Test]
        public void ConvertAppliesMeshTransformCentersBoundsAndPreservesFaceColor()
        {
            var vertices = new Dictionary<int, BlocksVertex>
            {
                { 10, new BlocksVertex(10, new Vector3(0, 0, 0)) },
                { 20, new BlocksVertex(20, new Vector3(1, 0, 0)) },
                { 30, new BlocksVertex(30, new Vector3(0, 1, 0)) },
            };
            var face = new BlocksFace(
                7, new List<int> { 10, 20, 30 }.AsReadOnly(), vertices,
                new FaceProperties(8));
            var mesh = new MMesh(
                3, new Vector3(2, 3, 4), Quaternion.Euler(0, 90, 0), MMesh.GROUP_NONE,
                vertices, new Dictionary<int, BlocksFace> { { face.id, face } });
            var file = new PeltzerFile(
                new Metadata("test", "", "1"), 1,
                new List<PeltzerMaterial> { new PeltzerMaterial(8) },
                new List<MMesh> { mesh });

            var (poly, recipe, warnings) = BlocksReader.Convert(file, "test.blocks");

            CollectionAssert.AreEquivalent(
                new[]
                {
                    new Vector3(0, -0.5f, 0.5f),
                    new Vector3(0, -0.5f, -0.5f),
                    new Vector3(0, 0.5f, 0.5f),
                },
                poly.ListVerticesByPoints());
            var bounds = new Bounds(poly.Vertices[0].Position, Vector3.zero);
            foreach (Polyhydra.Core.Vertex vertex in poly.Vertices)
            {
                bounds.Encapsulate(vertex.Position);
            }
            Assert.That(bounds.center, Is.EqualTo(Vector3.zero));
            Assert.That(poly.Faces, Has.Count.EqualTo(1));
            Assert.That(recipe.GeneratorType, Is.EqualTo(GeneratorTypes.GeometryData));
            CollectionAssert.AreEqual(new[] { 2, 1, 0 }, recipe.Faces[0]);
            Assert.That(recipe.FaceTags[0], Does.Contain("#F44336"));
            Assert.That(
                (Color32)PreviewPolyhedron.GetFaceColorForStrokes(poly, recipe, 0),
                Is.EqualTo(new Color32(244, 67, 54, 255)));
            var (rebuiltPoly, _) = PolyBuilder.BuildFromPolyDef(recipe);
            Assert.That(rebuiltPoly.Vertices, Has.Count.EqualTo(poly.Vertices.Count));
            Assert.That(rebuiltPoly.Faces, Has.Count.EqualTo(poly.Faces.Count));
            Assert.That(
                (Color32)PreviewPolyhedron.GetFaceColorForStrokes(
                    rebuiltPoly, recipe, 0),
                Is.EqualTo(new Color32(244, 67, 54, 255)));
            var roundTrippedRecipe = PolyRecipe.FromDef(
                new EditableModelDefinition(recipe));
            var (roundTrippedPoly, _) = PolyBuilder.BuildFromPolyDef(roundTrippedRecipe);
            Assert.That(roundTrippedPoly.Vertices, Has.Count.EqualTo(poly.Vertices.Count));
            Assert.That(roundTrippedPoly.Faces, Has.Count.EqualTo(poly.Faces.Count));
            Assert.That(warnings, Is.Empty);
        }

        [TestCase(".blocks")]
        [TestCase(".POLY")]
        [TestCase(".Peltzer")]
        public void RecognizesNativeExtension(string extension)
        {
            Assert.That(BlocksReader.IsSupportedExtension(extension), Is.True);
        }

        [Test]
        public void BlocksCatalogPrefersNativeFileOverObjFallback()
        {
            var files = new[]
            {
                @"C:\Blocks\example\model.obj",
                @"C:\Blocks\example\model-triangulated.obj",
                @"C:\Blocks\example\model.blocks",
            };

            string selected = (string)sm_GetPreferredBlocksModelFile.Invoke(
                null, new object[] { files });

            Assert.That(selected, Is.EqualTo(@"C:\Blocks\example\model.blocks"));
        }

        [Test]
        public void GeneratedModelInitializesMeshSplitCollections()
        {
            var model = new Model(Model.Location.Generated("test"));

            Assert.That(model.m_SplitMeshPaths, Is.Not.Null);
            Assert.That(model.m_NotSplittableMeshPaths, Is.Not.Null);
        }
    }
}
