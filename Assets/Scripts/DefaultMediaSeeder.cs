using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TiltBrush
{
    public static class DefaultMediaSeeder
    {
        private static string TrackingKey(string legacyKey) => $"{legacyKey}.HandledFilesV1";

        public static void Reset(string legacyKey)
        {
            PlayerPrefs.DeleteKey(TrackingKey(legacyKey));
        }

        // Entries mean handled, not necessarily copied: existing files and migrated defaults
        // must also stay deleted if the user subsequently removes them.
        public static void Seed(string directory, string[] defaults, string[] legacyDefaults,
            string legacyKey, bool hasPlayedBefore, Func<string, byte[]> loadResource,
            bool stripBytes = false)
        {
            string key = TrackingKey(legacyKey);
            bool initialized = PlayerPrefs.HasKey(key);
            var handled = new HashSet<string>(
                PlayerPrefs.GetString(key, "").Split(new[] { '\n' },
                    StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
            if (!initialized && (hasPlayedBefore || PlayerPrefs.GetInt(legacyKey, 0) != 0 ||
                Directory.GetFileSystemEntries(directory).Length != 0))
            {
                foreach (string file in legacyDefaults ?? Array.Empty<string>())
                {
                    handled.Add(file.Replace('\\', '/'));
                }
            }

            foreach (string file in defaults ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(file)) { continue; }
                string resourcePath = file.Replace('\\', '/');
                if (handled.Contains(resourcePath)) { continue; }
                string name = Path.GetFileName(resourcePath);
                if (stripBytes && name.EndsWith(".bytes", StringComparison.Ordinal))
                {
                    name = Path.GetFileNameWithoutExtension(name);
                }
                string destination = Path.Combine(directory, name);
                string temporary = null;
                try
                {
                    if (!File.Exists(destination))
                    {
                        byte[] bytes = loadResource(resourcePath);
                        // A failed write must not leave a partial destination that would be
                        // mistaken for an existing user file on the next startup.
                        temporary = Path.Combine(directory, $".default-media-{Guid.NewGuid():N}.tmp");
                        File.WriteAllBytes(temporary, bytes);
                        File.Move(temporary, destination);
                    }
                    handled.Add(resourcePath);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException ||
                    e is InvalidOperationException)
                {
                    Debug.LogWarning($"[DefaultMediaSeeder] Could not seed {resourcePath}: {e.Message}");
                }
                finally
                {
                    if (temporary != null && File.Exists(temporary))
                    {
                        try { File.Delete(temporary); }
                        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                        {
                            Debug.LogWarning($"[DefaultMediaSeeder] Could not remove {temporary}: {e.Message}");
                        }
                    }
                }
            }

            // Persist even an empty set, so migration is only applied once per category.
            var sorted = new List<string>(handled);
            sorted.Sort(StringComparer.Ordinal);
            string state = string.Join("\n", sorted);
            if (!initialized || state != PlayerPrefs.GetString(key, ""))
            {
                PlayerPrefs.SetString(key, state);
                PlayerPrefs.Save();
            }
        }
    }
}
