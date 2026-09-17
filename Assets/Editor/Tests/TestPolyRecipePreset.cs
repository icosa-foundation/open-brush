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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace TiltBrush
{
    internal class TestPolyRecipePreset
    {
        private const string kPresetJson = @"{
            'GeneratorType': 8,
            'GeneratorParameters': { 'type': 10 },
            'Operations': [
                { 'operation': 96, 'param1': 0.0, 'param2': 0.25, 'filterType': 0 },
                { 'operation': 96, 'param1': 0.0, 'param2': 0.75, 'filterType': 0 }
            ],
            'ExposedParameters': [
                {
                    'path': '$.Operations[0].param2',
                    'label': 'Roundness',
                    'min': 0.0,
                    'max': 1.0
                }
            ]
        }";

        [Test]
        public void ParsesRecipeAndExposedParameterMetadata()
        {
            PolyRecipePreset preset = PolyRecipePreset.FromJson(kPresetJson, null);

            Assert.That(preset.Recipe.Operators[0].amount2, Is.EqualTo(0.25f));
            Assert.That(preset.ExposedParameters, Has.Count.EqualTo(1));
            Assert.That(preset.ExposedParameters[0].Path, Is.EqualTo("$.Operations[0].param2"));
            Assert.That(preset.ExposedParameters[0].Label, Is.EqualTo("Roundness"));
            Assert.That(preset.ExposedParameters[0].Min, Is.EqualTo(0f));
            Assert.That(preset.ExposedParameters[0].Max, Is.EqualTo(1f));
            Assert.That(preset.ExposedParameters[0].InitialValue, Is.EqualTo(0.25f));
            Assert.That(preset.ExposedParameters[0].IsInteger, Is.False);
        }

        [Test]
        public void UpdatesReferencedOperationWithoutChangingOtherOperations()
        {
            PolyRecipePreset preset = PolyRecipePreset.FromJson(kPresetJson, null);

            PolyRecipe updated = preset.SetExposedParameterValue(
                preset.Recipe, 0, 0.5f, null);

            Assert.That(updated.Operators[0].amount2, Is.EqualTo(0.5f));
            Assert.That(updated.Operators[1].amount2, Is.EqualTo(0.75f));
        }

        [Test]
        public void SerializedRecipeRetainsExposedParameterDefinitions()
        {
            PolyRecipePreset preset = PolyRecipePreset.FromJson(kPresetJson, null);
            PolyRecipe updated = preset.SetExposedParameterValue(
                preset.Recipe, 0, 0.5f, null);

            JObject json = preset.ToJsonObject(updated);

            Assert.That(json.SelectToken("$.Operations[0].param2").Value<float>(),
                Is.EqualTo(0.5f));
            Assert.That(json.SelectToken("$.ExposedParameters[0].label").Value<string>(),
                Is.EqualTo("Roundness"));
        }

        [Test]
        public void NumericTokenSelectionRejectsNonNumericTargets()
        {
            var json = JObject.Parse(@"{
                'Operations': [ { 'disabled': false } ]
            }");

            Assert.Throws<JsonSerializationException>(() =>
                PolyRecipePreset.SelectNumericToken(json, "$.Operations[0].disabled"));
        }
    }
}
