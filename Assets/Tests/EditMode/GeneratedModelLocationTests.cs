// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TiltBrush.Tests
{
    public class GeneratedModelLocationTests
    {
        [Test]
        public void DistinctGeneratedDefinitionsRemainDistinctDictionaryKeys()
        {
            var location = Type.GetType("TiltBrush.Model+Location, Assembly-CSharp");
            Assert.NotNull(location);
            var generated = location.GetMethod("Generated");
            object first = generated.Invoke(null, new object[] { "first" });
            object same = generated.Invoke(null, new object[] { "first" });
            object second = generated.Invoke(null, new object[] { "second" });
            Assert.AreEqual(first, same);
            Assert.AreEqual(first.GetHashCode(), same.GetHashCode());
            Assert.AreNotEqual(first, second);
            var definitions = new Dictionary<object, string> { [first] = "frame", [second] = "capsule" };
            Assert.AreEqual(2, definitions.Count);
            Assert.AreEqual("frame", definitions[same]);
        }
    }
}
