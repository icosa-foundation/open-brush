// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.
using System;
using Newtonsoft.Json;
using NUnit.Framework;

namespace TiltBrush
{
    internal class TestModelPlaybackState
    {
        [Test]
        public void OlderModelMetadataHasNoPlaybackState()
        {
            var model = JsonConvert.DeserializeObject<TiltModels75>("{\"AssetId\":\"example\"}");
            Assert.IsNull(model.PlaybackStates);
            StringAssert.DoesNotContain("PlaybackStates", JsonConvert.SerializeObject(model));
        }

        [Test]
        public void PlaybackMetadataPreservesInstanceSlotsAndMute()
        {
            var model = new TiltModels75
            {
                AssetId = "example",
                PlaybackStates = new[] { null, new ModelPlaybackState
                {
                    Paused = true, Time = 40.5, StopTime = 2.25, Chapter = 3,
                    Volume = 0, Clip = "walk", Loop = false
                }}
            };
            var restored = JsonConvert.DeserializeObject<TiltModels75>(JsonConvert.SerializeObject(model));
            Assert.AreEqual(2, restored.PlaybackStates.Length);
            Assert.IsNull(restored.PlaybackStates[0]);
            var state = restored.PlaybackStates[1];
            Assert.IsTrue(state.Paused);
            Assert.AreEqual(40.5, state.Time);
            Assert.AreEqual(2.25, state.StopTime);
            Assert.AreEqual(3, state.Chapter);
            Assert.AreEqual(0, state.Volume);
            Assert.AreEqual("walk", state.Clip);
            Assert.AreEqual(false, state.Loop);
            Assert.AreEqual(1, JsonConvert.DeserializeObject<ModelPlaybackState>("{}").Volume);
        }

        [TestCase(true, false, 40.5)]
        [TestCase(false, true, 40.5)]
        [TestCase(false, false, 0)]
        public void RestoreCopiesStateAndResetsOnlyPlayingPositions(bool paused, bool clone, double time)
        {
            var saved = new ModelPlaybackState { Paused = paused, Time = 40.5, StopTime = 2, Chapter = 3, Volume = 0.35f };
            var restored = saved.ForRestore(clone);
            Assert.AreEqual(time, restored.Time);
            Assert.AreEqual(time == 0 ? 0 : 2, restored.StopTime);
            Assert.AreEqual(3, restored.Chapter); // Resolved to the authored chapter start after loading.
            Assert.AreEqual(0.35f, restored.Volume);
            restored.Volume = 0;
            Assert.AreEqual(0.35f, saved.Volume);
            Assert.AreEqual(40.5, saved.Time);
        }

        [TestCase(true, 0, 0)]
        [TestCase(false, 0, 0)]
        [TestCase(true, 2, 37800)]
        [TestCase(false, 2, 37801)]
        public void ChapterRestartUsesAuthoredMarkerClock(bool hasPlays, int chapter, long ticks)
        {
            Assert.AreEqual(ticks / 12600.0,
                ModelPlaybackState.GetChapterStartTime(new long[] { 12600, 25200, 37800 }, hasPlays, chapter), 1e-9);
        }

    }
}
