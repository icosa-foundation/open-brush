using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace TiltBrush
{
    internal class TestPolyRecipeSerialization
    {
        private const string Legacy = @"{
            'GeneratorType': 8, 'GeneratorParameters': { 'type': 10 },
            'Operations': [{ 'operation': 96, 'param1': .2, 'param2': .4,
                'disabled': true, 'param1Randomize': true, 'filterType': 0,
                'filterParamFloat': 3.75, 'filterParamInt': 1,
                'paramColor': [0.2, 0.4, 0.6, 1] }], 'AutoSmoothAngle': 35
        }";

        [Test]
        public void LegacyLoadAndCurrentSavePreserveModifierState()
        {
            var preset = PolyRecipePreset.FromJson(Legacy, null);
            var recipe = preset.Recipe;
            var op = recipe.Operators[0];
            Assert.That(op.disabled, Is.True);
            Assert.That(op.amountRandomize, Is.True);
            Assert.That(op.filterParamFloat, Is.EqualTo(3.75f));
            Assert.That(op.filterParamInt, Is.EqualTo(1));
            Assert.That(recipe.PostProcessing.AutoSmoothAngle, Is.EqualTo(35));
            JObject saved = preset.ToJsonObject(recipe);
            Assert.That(saved["Recipe"].Value<int>("Version"), Is.EqualTo(1));
            Assert.That(saved["Operations"], Is.Null);
            Assert.That(saved["AutoSmoothAngle"], Is.Null);
            var again = PolyRecipe.FromJson(saved.ToString(), null);
            var settings = again.Operators[0].ToModifierSettings();
            Assert.That(settings.StringParameter, Is.EqualTo("#336699"));
            Assert.That(settings.FilterParamInt, Is.EqualTo(1));
            Assert.That(settings.Parameter2, Is.EqualTo(.4f));
            Assert.That(settings.Active, Is.False);
        }

        [Test]
        public void ClonedPostProcessingIsIndependent()
        {
            var recipe = PolyRecipe.FromJson(Legacy, null);
            var clone = recipe.Clone();
            clone.PostProcessing.AutoSmoothAngle = 70;
            Assert.That(recipe.PostProcessing.AutoSmoothAngle, Is.EqualTo(35));
        }

        [TestCase("{ 'RecipeVersion': 2 }")]
        [TestCase("{ 'RecipeVersion': 1, 'Operations': [] }")]
        [TestCase("{ 'Modifiers': [] }")]
        public void RejectsUnsupportedOrMixedFormats(string json)
            => Assert.Catch(() => PolyRecipe.FromJson(json, null));

        [Test]
        public void UnversionedSectorAndVersionedLegacyRectangleRemainDistinct()
        {
            var legacy = PolyRecipe.FromJson(@"{ 'GeneratorType': 3,
                'GeneratorParameters': { 'type': 10, 'b': .5 }, 'Operations': [] }", null);
            var current = PolyRecipe.FromJson(@"{ 'RecipeVersion': 1, 'GeneratorType': 3,
                'GeneratorParameters': { 'type': 10, 'width': 2, 'height': 1 }, 'Modifiers': [] }", null);
            Assert.That((int)legacy.ShapeType, Is.EqualTo(5));
            Assert.That((string)current.SourceDefinition.Recipe.ToJson()["Generator"]["Type"], Is.EqualTo("rectangle"));
        }
    }
}
