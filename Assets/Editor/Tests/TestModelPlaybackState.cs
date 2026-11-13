// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.
using System;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TiltBrush
{
    internal class TestModelPlaybackState
    {
        [TestCase(false)]
        [TestCase(true)]
        public void HiddenAnimatedCloneCanBeSavedBeforeOnEnable(bool legacy)
        {
            var template = new GameObject("Hidden animated model template");
            template.SetActive(false);
            GameObject clone = null;
            var clip = new AnimationClip { name = "walk", legacy = legacy };
            try
            {
                var animation = template.AddComponent<PlayGltfAnimationClip>();
                var serialized = new SerializedObject(animation);
                var clips = serialized.FindProperty("m_Clips");
                clips.arraySize = 1;
                clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
                serialized.FindProperty("m_SelectedClip").objectReferenceValue = clip;
                serialized.FindProperty("m_Loop").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                clone = UnityEngine.Object.Instantiate(template);
                var clonedAnimation = clone.GetComponent<PlayGltfAnimationClip>();
                Assert.IsFalse(clone.activeInHierarchy);
                Assert.IsTrue(clonedAnimation.Initialized, "The serialized clip must survive cloning.");
                Assert.AreEqual(0, clonedAnimation.Time);

                var playback = clone.AddComponent<ModelPlaybackController>();
                playback.Bind(clone.transform);
                var state = playback.GetSaveState();
                Assert.IsNotNull(state);
                Assert.AreEqual(0, state.Time);
                Assert.AreEqual("walk", state.Clip);
                Assert.AreEqual(true, state.Loop);

                playback.Restore(new ModelPlaybackState
                {
                    Paused = true, Time = 1.25, Clip = "walk", Loop = false, Volume = 0.35f
                });
                var pending = playback.GetSaveState();
                Assert.AreEqual(1.25, pending.Time);
                Assert.AreEqual("walk", pending.Clip);
                Assert.AreEqual(false, pending.Loop);
                Assert.IsTrue(pending.Paused);
                Assert.AreEqual(0.35f, pending.Volume);
            }
            finally
            {
                if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
                UnityEngine.Object.DestroyImmediate(template);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

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
