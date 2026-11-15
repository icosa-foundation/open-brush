// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace TiltBrush.Tests
{
    public class LightWidgetSerializationTests
    {
        private static Type AppType(string name) => Type.GetType($"TiltBrush.{name}, Assembly-CSharp", true);

        [Test]
        public void LightStateRoundTripsAndLegacyMissingFieldsRemainOptional()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = (IContractResolver)Activator.CreateInstance(AppType("CustomJsonContractResolver"))
            };
            string Save(object value)
            {
                using (var text = new StringWriter())
                using (var writer = (JsonWriter)Activator.CreateInstance(AppType("CustomJsonWriter"), text))
                {
                    JsonSerializer.Create(settings).Serialize(writer, value);
                    writer.Flush(); return text.ToString();
                }
            }
            const string json = @"{'PunctualLightType':2,'Intensity':4,'Range':30,'Enabled':false,'Shadows':1,
                'Transform':[[1,2,3],[0,0,0,1],2],'LayerId':3,'GroupId':7}";
            var restored = JsonConvert.DeserializeObject(json, AppType("TiltLights"), settings);
            var data = JObject.Parse(Save(restored));
            Assert.IsFalse((bool)data["Enabled"]);
            Assert.AreEqual(1, (int)data["Shadows"]);
            Assert.AreEqual(4, (float)data["Intensity"]);
            Assert.AreEqual(30, (float)data["Range"]);
            Assert.AreEqual(3, (int)data["LayerId"]);
            Assert.AreEqual(7, (int)data["GroupId"]);
            var legacy = JsonConvert.DeserializeObject("{'PunctualLightType':2,'Range':10}", AppType("TiltLights"), settings);
            var legacyData = JObject.Parse(Save(legacy));
            Assert.IsNull(legacyData["Enabled"]);
            Assert.IsNull(legacyData["Shadows"]);
            Assert.IsNull(AppType("TiltLights").GetField("OuterConeAngle").GetValue(legacy));
        }
    }
}
