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
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    internal class TestPolyRecipeSmoothing
    {
        [Test]
        public void AutoSmoothAngleParsesBuildsAndRoundTrips()
        {
            const string json = @"{
                'GeneratorType': 8,
                'GeneratorParameters': { 'type': 10 },
                'Operations': [],
                'AutoSmoothAngle': 91.0
            }";

            PolyRecipe recipe = PolyRecipe.FromJson(json, null);
            var (poly, _) = PolyBuilder.BuildFromPolyDef(recipe);
            var roundTrippedDefinition = new EditableModelDefinition(recipe);
            PolyRecipe roundTrippedRecipe = PolyRecipe.FromDef(roundTrippedDefinition);

            Assert.That(recipe.AutoSmoothAngle, Is.EqualTo(91f));
            Assert.That(poly.Halfedges.Any(edge => edge.IsEdgeSmooth), Is.True);
            Assert.That(roundTrippedDefinition.AutoSmoothAngle, Is.EqualTo(91f));
            Assert.That(roundTrippedRecipe.AutoSmoothAngle, Is.EqualTo(91f));
        }

        [Test]
        public void MissingAutoSmoothAngleKeepsEdgesHard()
        {
            const string json = @"{
                'GeneratorType': 8,
                'GeneratorParameters': { 'type': 10 },
                'Operations': []
            }";

            PolyRecipe recipe = PolyRecipe.FromJson(json, null);
            var (poly, _) = PolyBuilder.BuildFromPolyDef(recipe);

            Assert.That(recipe.AutoSmoothAngle, Is.Null);
            Assert.That(poly.Halfedges.Any(edge => edge.IsEdgeSmooth), Is.False);
        }

        [Test]
        public void ZeroAutoSmoothAngleKeepsEdgesHard()
        {
            PolyRecipe recipe = PolyRecipe.CreateDefault(null);
            recipe.AutoSmoothAngle = 0f;

            var (poly, _) = PolyBuilder.BuildFromPolyDef(recipe);

            Assert.That(poly.Halfedges.Any(edge => edge.IsEdgeSmooth), Is.False);
        }
        [Test]
        public void EdgeStrokesExcludeAutoSmoothedPrismEdges()
        {
            var poly = RadialSolids.Prism(32);
            poly.AutoSmooth(20f);

            var strokeEdges = PolyhydraTool.GetHardStrokeEdges(poly);

            Assert.That(strokeEdges, Has.Count.EqualTo(64));
            Assert.That(strokeEdges.All(edge => !edge.IsEdgeSmooth), Is.True);
        }

        [Test]
        public void FaceStrokesTraceBoundariesOfSmoothedPrismRegions()
        {
            var poly = RadialSolids.Prism(32);
            poly.AutoSmooth(20f);

            var paths = PolyhydraTool.GetFaceStrokePaths(poly);

            Assert.That(paths, Has.Count.EqualTo(4));
            Assert.That(paths.Sum(path => path.Edges.Count), Is.EqualTo(128));
            Assert.That(paths.SelectMany(path => path.Edges)
                .All(edge => !edge.IsEdgeSmooth), Is.True);
        }

        [Test]
        public void FaceStrokesRemainPerFaceWithoutSmoothing()
        {
            var poly = RadialSolids.Prism(32);

            var paths = PolyhydraTool.GetFaceStrokePaths(poly);

            Assert.That(paths, Has.Count.EqualTo(34));
            Assert.That(paths.Sum(path => path.Edges.Count), Is.EqualTo(192));
        }

        [Test]
        public void FullySmoothedClosedSurfaceHasNoFaceBoundaryStrokes()
        {
            var poly = RadialSolids.Prism(32);
            poly.AutoSmooth(180f);

            Assert.That(PolyhydraTool.GetHardStrokeEdges(poly), Is.Empty);
            Assert.That(PolyhydraTool.GetFaceStrokePaths(poly), Is.Empty);
        }

        [Test]
        public void ParameterizedPrimitivePresetsBuildWithValidJsonPaths()
        {
            string[] presetNames =
            {
                "Chamfer Cube", "Sphere", "Half Sphere", "Hollow Half Sphere",
                "Cylinder", "Chamfered Cylinder", "Tube", "Capsule", "Torus", "Egg",
                "Wedge", "Quarter Arc", "Cone", "Circle", "Wireframe Cube"
            };
            var planarPresets = new HashSet<string> { "Quarter Arc", "Circle" };
            string presetDirectory = Path.Combine(
                "Assets", "Polyhydra", "Resources", "Shape Gallery Presets");

            foreach (string presetName in presetNames)
            {
                string jsonText = File.ReadAllText(
                    Path.Combine(presetDirectory, $"{presetName}.json"));
                var json = JObject.Parse(jsonText);
                if (json["ExposedParameters"] is JArray parameters)
                {
                    Assert.That(parameters.Count, Is.LessThanOrEqualTo(3), presetName);
                    foreach (JObject parameter in parameters.OfType<JObject>())
                    {
                        Assert.DoesNotThrow(
                            () => PolyRecipePreset.SelectNumericToken(
                                json, parameter.Value<string>("path")),
                            $"{presetName}: {parameter.Value<string>("label")}");
                    }
                }

                PolyRecipe recipe = PolyRecipe.FromJson(jsonText, null);
                var (poly, _) = PolyBuilder.BuildFromPolyDef(recipe);
                Assert.That(poly.IsValid, Is.True, presetName);
                if (!planarPresets.Contains(presetName))
                {
                    Assert.That(poly.Halfedges.All(edge => edge.Pair != null), Is.True,
                        $"{presetName} has boundary edges");
                    Vector3 minimum = poly.Vertices[0].Position;
                    Vector3 maximum = minimum;
                    foreach (var vertex in poly.Vertices.Skip(1))
                    {
                        minimum = Vector3.Min(minimum, vertex.Position);
                        maximum = Vector3.Max(maximum, vertex.Position);
                    }
                    Vector3 size = maximum - minimum;
                    Assert.That(size.x, Is.GreaterThan(0.001f), $"{presetName} has no width");
                    Assert.That(size.y, Is.GreaterThan(0.001f), $"{presetName} has no height");
                    Assert.That(size.z, Is.GreaterThan(0.001f), $"{presetName} has no depth");
                }
            }
        }
    }
}
