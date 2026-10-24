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
    public class ImageMetadataTests
    {
        private static Type AppType(string name) => Type.GetType($"TiltBrush.{name}, Assembly-CSharp", true);

        private static JObject RoundTrip(string json)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = (IContractResolver)Activator.CreateInstance(AppType("CustomJsonContractResolver"))
            };
            var index = JsonConvert.DeserializeObject(json, AppType("TiltImages75"), settings);
            using (var text = new StringWriter())
            using (var writer = (JsonWriter)Activator.CreateInstance(AppType("CustomJsonWriter"), text))
            {
                JsonSerializer.Create(settings).Serialize(writer, index);
                writer.Flush();
                return JObject.Parse(text.ToString());
            }
        }

        [Test]
        public void ColorsAndBlendModesStayAlignedWithTransformsAndLegacyTint()
        {
            var data = RoundTrip(@"{'FilePath':'Images/shared.png','AspectRatio':2,
                'Transforms':[[[1,2,3],[0,0,0,1],4],[[5,6,7],[0,0,0,1],8]],
                'TintStates':[true,false], 'Colors':[[1,1,1,1],[0.25,0.5,1,0.75]], 'BlendModes':[0,3]}");
            Assert.AreEqual(8, (float)data["Transforms"][1][2]);
            Assert.AreEqual(.25, (float)data["Colors"][1][0]);
            Assert.AreEqual(.75, (float)data["Colors"][1][3]);
            Assert.AreEqual(3, (int)data["BlendModes"][1]);
            Assert.IsTrue((bool)data["TintStates"][0]);
            Assert.IsFalse((bool)data["TintStates"][1]);
            Assert.IsNull(data["Layouts"]);
            Assert.IsNull(data["Opacities"]);
        }

        [Test]
        public void EstablishedImageMetadataDoesNotAcquireNewDefaultArrays()
        {
            var data = RoundTrip(@"{'FileName':'old.png','AspectRatio':1.5,
                'Transforms':[[[1,2,3],[0,0,0,1],4]],'TintStates':[true]}");
            Assert.IsNull(data["Colors"]);
            Assert.IsNull(data["BlendModes"]);
            Assert.AreEqual(1.5, (float)data["AspectRatio"]);
            Assert.IsTrue((bool)data["TintStates"][0]);
            Assert.IsNull(AppType("TiltImages75").GetProperty("Layouts"));
            Assert.IsNull(AppType("TiltImages75").GetProperty("Opacities"));
        }
    }
}
