// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace TiltBrush.Tests
{
    public class TextWidgetLayoutTests
    {
        private static Type AppType(string name) => Type.GetType($"TiltBrush.{name}, Assembly-CSharp", true);

        [Test]
        public void AuthoredLayoutRoundTripsThroughSketchJsonIncludingShearAndUnknownFields()
        {
            var type = AppType("TextWidgetLayout");
            var settings = new JsonSerializerSettings
            {
                ContractResolver = (IContractResolver)Activator.CreateInstance(AppType("CustomJsonContractResolver"))
            };
            var input = JObject.Parse(@"{'FontSize':48.0,'AutoSize':true,'FontSizeMin':1.0,'FontSizeMax':1000.0,
                'FontStyle':3,'Alignment':258,'Wrapping':1,'RichText':true,'Size':[250.0,100.0],'Pivot':[0.0,1.0],
                'GlyphTransform':[1.0,0.0,0.0,0.0,0.5,2.0,0.0,0.0,0.0,0.0,1.0,0.0,3.0,4.0,5.0,1.0],'Future':{'value':42}}");
            object layout = input.ToObject(type, JsonSerializer.Create(settings));
            type.GetMethod("Validate").Invoke(layout, null);
            string json;
            using (var text = new StringWriter())
            using (var writer = (JsonWriter)Activator.CreateInstance(AppType("CustomJsonWriter"), text))
            {
                JsonSerializer.Create(settings).Serialize(writer, layout);
                writer.Flush();
                json = text.ToString();
            }
            var output = JObject.Parse(json);
            foreach (var field in input.Properties())
            {
                Assert.IsTrue(JToken.DeepEquals(field.Value, output[field.Name]), $"Lost text layout field {field.Name}");
            }
            type.GetMethod("Validate").Invoke(JsonConvert.DeserializeObject(json, type, settings), null);
        }

        [Test]
        public void LayoutRejectsInvalidRangesAndNonAffineTransforms()
        {
            var type = AppType("TextWidgetLayout");
            object layout = Activator.CreateInstance(type);
            type.GetProperty("FontSizeMin").SetValue(layout, 2f);
            Assert.IsInstanceOf<ArgumentException>(Assert.Throws<TargetInvocationException>(() => type.GetMethod("Validate").Invoke(layout, null)).InnerException);
            type.GetProperty("FontSizeMax").SetValue(layout, 10f);
            type.GetProperty("GlyphTransform").SetValue(layout, new float[16]);
            Assert.IsInstanceOf<ArgumentException>(Assert.Throws<TargetInvocationException>(() => type.GetMethod("Validate").Invoke(layout, null)).InnerException);
        }
    }
}
