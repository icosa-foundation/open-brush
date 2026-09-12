using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    public class TestDefaultMediaSeeder
    {
        private string m_Directory;
        private string m_Key;
        private static readonly string[] kLegacy = { "Defaults/old.bin" };
        private static readonly string[] kCurrent = { "Defaults/old.bin", "Defaults/new.bin" };

        [Test]
        public void SafMigration_HandlesOnlyLegacyResourcesAndPreservesDeletionState()
        {
            var migrated = DefaultMediaSeeder.GetHandledFiles(null, true, kLegacy);
            Assert.IsTrue(migrated.Contains(kLegacy[0]));
            Assert.IsFalse(migrated.Contains(kCurrent[1]));
            migrated.Add(kCurrent[1]);
            var reopened = DefaultMediaSeeder.GetHandledFiles(string.Join("\n", migrated), true, kLegacy);
            CollectionAssert.AreEquivalent(kCurrent, reopened);
            Assert.IsEmpty(DefaultMediaSeeder.GetHandledFiles(null, false, kLegacy));
            Assert.IsEmpty(DefaultMediaSeeder.GetHandledFiles("", true, kLegacy));
        }

        [SetUp]
        public void SetUp()
        {
            m_Directory = Path.Combine(Path.GetTempPath(), $"DefaultMediaSeeder-{Guid.NewGuid():N}");
            m_Key = $"DefaultMediaSeeder-Test-{Guid.NewGuid():N}";
            Directory.CreateDirectory(m_Directory);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(m_Directory, true);
            DefaultMediaSeeder.Reset(m_Key);
            PlayerPrefs.DeleteKey(m_Key);
            PlayerPrefs.Save();
        }

        private void Seed(bool existing = false, string[] defaults = null)
        {
            DefaultMediaSeeder.Seed(m_Directory, defaults ?? kCurrent, kLegacy, m_Key,
                existing, _ => new byte[] { 1, 2, 3 });
        }

        [Test]
        public void FreshInstallCopiesAllDefaultsAndPreservesLaterDeletions()
        {
            Seed();
            Assert.IsTrue(File.Exists(Path.Combine(m_Directory, "old.bin")));
            Assert.IsTrue(File.Exists(Path.Combine(m_Directory, "new.bin")));
            File.Delete(Path.Combine(m_Directory, "old.bin"));
            File.Delete(Path.Combine(m_Directory, "new.bin"));
            Seed();
            Assert.IsEmpty(Directory.GetFiles(m_Directory));
        }

        [Test]
        public void ExistingInstallSkipsDeletedLegacyButCopiesLaterDefaults()
        {
            Seed(existing: true);
            Assert.IsFalse(File.Exists(Path.Combine(m_Directory, "old.bin")));
            Assert.IsTrue(File.Exists(Path.Combine(m_Directory, "new.bin")));
            File.Delete(Path.Combine(m_Directory, "new.bin"));
            Seed(existing: true, defaults: new[] { "Defaults/new.bin", "Defaults/future.bin" });
            Assert.IsFalse(File.Exists(Path.Combine(m_Directory, "new.bin")));
            Assert.IsTrue(File.Exists(Path.Combine(m_Directory, "future.bin")));
        }

        [Test]
        public void LegacyFlagAloneTriggersMigration()
        {
            PlayerPrefs.SetInt(m_Key, 1);
            Seed();
            Assert.IsFalse(File.Exists(Path.Combine(m_Directory, "old.bin")));
            Assert.IsTrue(File.Exists(Path.Combine(m_Directory, "new.bin")));
        }

        [Test]
        public void PopulatedLibraryTriggersMigrationWithoutLegacyFlag()
        {
            string destination = Path.Combine(m_Directory, "new.bin");
            File.WriteAllText(destination, "user content");
            Seed();
            Assert.IsFalse(File.Exists(Path.Combine(m_Directory, "old.bin")));
            Assert.AreEqual("user content", File.ReadAllText(destination));
            File.Delete(destination);
            Seed();
            Assert.IsFalse(File.Exists(destination));
        }

        [Test]
        public void FailedCopyIsRetriedAndDoesNotBlockOtherDefaults()
        {
            DefaultMediaSeeder.Seed(m_Directory, kCurrent, kLegacy, m_Key, false,
                file => file == kCurrent[0]
                    ? throw new IOException("simulated read failure") : new byte[] { 1 });
            Assert.IsFalse(File.Exists(Path.Combine(m_Directory, "old.bin")));
            Assert.IsTrue(File.Exists(Path.Combine(m_Directory, "new.bin")));
            // The newly populated folder must not re-trigger legacy migration.
            Seed();
            Assert.IsTrue(File.Exists(Path.Combine(m_Directory, "old.bin")));
        }

        [Test]
        public void FailedMoveLeavesNoPartialFileAndCanBeRetried()
        {
            string destination = Path.Combine(m_Directory, "new.bin");
            Directory.CreateDirectory(destination);
            Seed();
            Assert.IsEmpty(Directory.GetFiles(m_Directory));
            Directory.Delete(destination);
            Seed();
            Assert.IsTrue(File.Exists(destination));
        }

        [Test]
        public void CategoriesAbsentFromLegacyReleaseStillCopyForExistingUsers()
        {
            DefaultMediaSeeder.Seed(m_Directory, kCurrent, null, m_Key, true,
                _ => new byte[] { 1 });
            Assert.AreEqual(2, Directory.GetFiles(m_Directory).Length);
        }

        [Test]
        public void EmptyInitialCatalogDoesNotPreventFutureAdditions()
        {
            Seed(defaults: Array.Empty<string>());
            Seed(existing: true);
            Assert.AreEqual(2, Directory.GetFiles(m_Directory).Length);
        }

        [Test]
        public void NormalizesResourceSeparatorsAndBackgroundBytesExtension()
        {
            DefaultMediaSeeder.Seed(m_Directory, new[] { "Defaults\\image.jpg.bytes" },
                null, m_Key, false, _ => new byte[] { 1 }, stripBytes: true);
            string destination = Path.Combine(m_Directory, "image.jpg");
            Assert.IsTrue(File.Exists(destination));
            File.Delete(destination);
            DefaultMediaSeeder.Seed(m_Directory, new[] { "Defaults/image.jpg.bytes" },
                null, m_Key, false, _ => new byte[] { 1 }, stripBytes: true);
            Assert.IsFalse(File.Exists(destination));
        }

        [Test]
        public void FirstRunResetAllowsDefaultsToBeCopiedAgain()
        {
            Seed();
            foreach (string file in Directory.GetFiles(m_Directory)) { File.Delete(file); }
            DefaultMediaSeeder.Reset(m_Key);
            Seed();
            Assert.AreEqual(2, Directory.GetFiles(m_Directory).Length);
        }
    }
}
