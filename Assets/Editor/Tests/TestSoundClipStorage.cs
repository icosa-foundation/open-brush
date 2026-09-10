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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace TiltBrush
{
    internal class TestSoundClipStorage
    {
        private string m_Root;
        private LocalUserStorageBackend m_Backend;

        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(Path.GetTempPath(), $"open-brush-saf-sounds-{Guid.NewGuid():N}");
            Directory.CreateDirectory(m_Root);
            m_Backend = new LocalUserStorageBackend(_ => m_Root);
        }

        [TearDown]
        public void TearDown()
        {
            Assert.IsTrue(Path.GetFullPath(m_Root).StartsWith(
                Path.Combine(Path.GetTempPath(), "open-brush-saf-sounds-"), StringComparison.Ordinal));
            Directory.Delete(m_Root, recursive: true);
        }

        [Test]
        public void SoundQuery_FindsNestedAudioAndPreservesLogicalPaths()
        {
            string nested = Path.Combine(m_Root, "Nested");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "clip.WAV"), "audio");
            File.WriteAllText(Path.Combine(nested, "ignored.txt"), "text");

            StorageTreeResult result = SoundClipCatalog.QuerySafSoundClips(
                m_Backend, "", new[] { ".wav" }, null);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Entries.Count);
            SoundClip clip = SoundClipCatalog.CreateSafSoundClip(m_Backend, result.Entries.Single());
            Assert.AreEqual("Nested/clip.WAV", clip.PersistentPath);
            Assert.AreEqual("clip.WAV", clip.HumanName);
            Assert.AreEqual(Path.Combine(nested, "clip.WAV"), clip.AbsolutePath);
            Assert.IsFalse(clip.IsInitialized);
        }

        [Test]
        public void SoundDefaults_DoNotModifyExistingLibrary()
        {
            File.WriteAllText(Path.Combine(m_Root, "custom.wav"), "user audio");
            var defaults = new Dictionary<string, byte[]> { ["default.wav"] = new byte[] { 1, 2 } };

            StorageTreeResult result = SoundClipCatalog.QuerySafSoundClips(
                m_Backend, "", new[] { ".wav" }, defaults);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("user audio", File.ReadAllText(Path.Combine(m_Root, "custom.wav")));
            Assert.IsFalse(File.Exists(Path.Combine(m_Root, "default.wav")));
        }

        [Test]
        public void SoundDefaults_SeedEmptyLibraryThroughStorageBackend()
        {
            var defaults = new Dictionary<string, byte[]> { ["default.wav"] = new byte[] { 1, 2 } };

            StorageTreeResult result = SoundClipCatalog.QuerySafSoundClips(
                m_Backend, "", new[] { ".wav" }, defaults);

            Assert.IsTrue(result.Success, result.Error);
            CollectionAssert.AreEqual(defaults["default.wav"], File.ReadAllBytes(Path.Combine(m_Root, "default.wav")));
            Assert.AreEqual("default.wav", result.Entries.Single().RelativeDisplayPath);
        }
    }
}
