// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.
using System;
using MoonSharp.Interpreter;
using NUnit.Framework;

namespace TiltBrush
{
    internal class TestModelPlaybackApi
    {
        [TestCase(0, 2, 0)]
        [TestCase(-1, 2, 1)]
        [TestCase(-2, 2, 0)]
        public void ModelIndicesSupportNegativeOffsets(int index, int count, int expected)
        {
            Assert.AreEqual(expected, ApiMethods.NormalizeModelPlaybackIndex(index, count));
        }
        [TestCase(0, 0)]
        [TestCase(2, 2)]
        [TestCase(-3, 2)]
        [TestCase(int.MinValue, 2)]
        public void InvalidModelIndicesAreUsefulCompletedErrors(int index, int count)
        {
            var result = ApiMethods.InvokeModelPlayback(() => ApiMethods.NormalizeModelPlaybackIndex(index, count).ToString());
            StringAssert.StartsWith($"error: Model index {index} is out of range ({count} active models)", result);
        }
        [Test]
        public void ExpectedPlaybackFailuresCompleteWithAnError()
        {
            StringAssert.StartsWith("error: ", ApiMethods.InvokeModelPlayback(() => throw new ArgumentOutOfRangeException("index")));
            Assert.AreEqual("error: Model playback is busy",
                ApiMethods.InvokeModelPlayback(() => throw new InvalidOperationException("Model playback is busy")));
            Assert.AreEqual("error: This model has no animation clips",
                ApiMethods.InvokeModelPlayback(() => throw new InvalidOperationException("This model has no animation clips")));
            Assert.Throws<NullReferenceException>(() =>
                ApiMethods.InvokeModelPlayback(() => throw new NullReferenceException("unexpected failure")));
        }

        [Test]
        public void MalformedHttpParametersCompleteWithAnError()
        {
            Assert.AreEqual("error: Expected a number", ApiManager.InvokeEndpointForStatus(() => throw new FormatException("Expected a number")));
            StringAssert.StartsWith("error: ", ApiManager.InvokeEndpointForStatus(() => throw new ArgumentException("Missing index")));
            Assert.Throws<InvalidOperationException>(() => ApiManager.InvokeEndpointForStatus(() => throw new InvalidOperationException("unexpected failure")));
        }

        [Test]
        public void LuaReportsDeletedModelsThroughPcall()
        {
            UserData.RegisterType<ModelApiWrapper>();
            var script = new Script();
            script.Globals["model"] = new ModelApiWrapper(null);
            var result = script.DoString("local ok, err = pcall(function() model:Pause() end); return ok, tostring(err)");
            Assert.IsFalse(result.Tuple[0].Boolean);
            StringAssert.Contains("Model instance no longer exists", result.Tuple[1].String);
        }
    }
}
