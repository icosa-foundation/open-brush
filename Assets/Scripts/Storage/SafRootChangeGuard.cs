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
using System.IO;
using UnityEngine;

namespace TiltBrush
{
    /// The one surviving root check.
    ///
    /// Open Brush assumes the shared folder is fixed for the life of an installation, which is why
    /// the per-operation root comparisons that used to run throughout the catalogs, the write
    /// transaction and the publisher have all been removed. That assumption is about the user's
    /// behaviour, not about the URI: a reinstall, a provider change, or a re-grant through the
    /// recovery path can all hand back a different root.
    ///
    /// Without a check, a changed root would silently write into app-private state left behind by
    /// the previous one - staged outputs, publication records, seeding flags - with nothing to
    /// notice. So the root is recorded once and compared at startup, and anything derived from it
    /// is discarded when it differs.
    public static class SafRootChangeGuard
    {
        private const string kRecordedRootPreference = "GooglePlayStorage.RecordedRootIdentity";

        /// Compares the selected root against the one this installation last used, and clears
        /// local state derived from the old root when they differ. Call once, after the folder
        /// grant is in place and before catalogs read anything.
        public static void ReconcileAtStartup()
        {
            if (!OpenBrushStorage.IsGooglePlayStorageMode)
            {
                return;
            }
            string current = AndroidSafStorage.GetSelectedRootIdentity();
            if (string.IsNullOrEmpty(current))
            {
                return;
            }

            string recorded = PlayerPrefs.GetString(kRecordedRootPreference, null);
            if (string.Equals(recorded, current, StringComparison.Ordinal))
            {
                return;
            }

            if (!string.IsNullOrEmpty(recorded))
            {
                Debug.LogWarning(
                    "SAF_ROOT The shared folder differs from the one last used; " +
                    "discarding local state derived from it.");
                DiscardDerivedLocalState();
            }

            PlayerPrefs.SetString(kRecordedRootPreference, current);
            PlayerPrefs.Save();
        }

        /// App-private state keyed to a particular root. None of it is canonical - the shared
        /// folder holds everything that matters - so discarding it costs only rework.
        private static void DiscardDerivedLocalState()
        {
            foreach (string path in new[]
            {
                OpenBrushStorage.LocalStagingPath,
                OpenBrushStorage.LocalExportStagingPath,
                Path.Combine(Application.persistentDataPath, "OpenBrushSafRecovery"),
                Path.Combine(Application.persistentDataPath, "OpenBrushSafPublications"),
            })
            {
                TryDeleteDirectory(path);
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (Exception e) when (
                e is IOException || e is UnauthorizedAccessException)
            {
                // Stale state left in place is recoverable; a failed startup is not.
                Debug.LogWarning($"SAF_ROOT Could not discard {path}: {e.Message}");
            }
        }
    }
}
