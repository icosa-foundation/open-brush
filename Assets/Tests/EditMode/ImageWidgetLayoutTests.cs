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
    public class ImageWidgetLayoutTests
    {
        private static Type AppType(string name) => Type.GetType($"TiltBrush.{name}, Assembly-CSharp", true);

        [Test]
        public void ImageIndexKeepsLayoutAlignedWithItsTransformAndExistingOpacity()
        {
            const string json = @"{'FilePath':'Images/shared.png','AspectRatio':2.0,
                'Transforms':[[[1,2,3],[0,0,0,1],4],[[5,6,7],[0,0,0,1],8]],
                'Opacities':[1.0,0.75],'Layouts':[null,{'Transform':[2,0,0,0,0.5,1,0,0,0,0,1,0,0,0,0,1],
                'Tint':[0.25,0.5,1.0],'BlendMode':2}]}";
            var settings = new JsonSerializerSettings
            {
                ContractResolver = (IContractResolver)Activator.CreateInstance(AppType("CustomJsonContractResolver"))
            };
            var index = JsonConvert.DeserializeObject(json, AppType("TiltImages75"), settings);
            string output;
            using (var text = new StringWriter())
            using (var writer = (JsonWriter)Activator.CreateInstance(AppType("CustomJsonWriter"), text))
            {
                JsonSerializer.Create(settings).Serialize(writer, index);
                writer.Flush(); output = text.ToString();
            }
            var data = JObject.Parse(output);
            Assert.AreEqual(JTokenType.Null, data["Layouts"][0].Type);
            Assert.AreEqual(8, (float)data["Transforms"][1][2]);
            Assert.AreEqual(.75, (float)data["Opacities"][1]);
            Assert.AreEqual(.25, (float)data["Layouts"][1]["Tint"][0]);
            Assert.AreEqual(.5, (float)data["Layouts"][1]["Transform"][4]);
            Assert.AreEqual(2, (int)data["Layouts"][1]["BlendMode"]);
            object layout = data["Layouts"][1].ToObject(AppType("ImageWidgetLayout"));
            layout.GetType().GetMethod("Validate").Invoke(layout, null);
        }

        [Test]
        public void InvalidImageMatricesAndTintComponentsAreRejected()
        {
            var type = AppType("ImageWidgetLayout");
            object layout = Activator.CreateInstance(type);
            type.GetProperty("Transform").SetValue(layout, new float[16]);
            Assert.IsInstanceOf<ArgumentException>(Assert.Throws<TargetInvocationException>(() => type.GetMethod("Validate").Invoke(layout, null)).InnerException);
            type.GetProperty("Transform").SetValue(layout, null);
            type.GetProperty("Tint").SetValue(layout, new[] { float.NaN, 1f, 1f });
            Assert.IsInstanceOf<ArgumentException>(Assert.Throws<TargetInvocationException>(() => type.GetMethod("Validate").Invoke(layout, null)).InnerException);
        }
    }
}
