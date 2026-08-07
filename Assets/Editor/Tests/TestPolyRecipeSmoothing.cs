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

using System.Linq;
using NUnit.Framework;

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
    }
}
