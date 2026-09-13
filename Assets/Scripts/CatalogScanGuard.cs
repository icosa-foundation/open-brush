using System;
using System.Collections.Generic;

namespace TiltBrush
{
    internal static class CatalogScanGuard
    {
        internal static bool IsCurrent(
            int generation, int currentGeneration,
            object backend, object currentBackend,
            string rootIdentity, string currentRootIdentity,
            string directory, string currentDirectory,
            StringComparer pathComparer)
        {
            return generation == currentGeneration &&
                ReferenceEquals(backend, currentBackend) &&
                string.Equals(rootIdentity, currentRootIdentity, StringComparison.Ordinal) &&
                pathComparer.Equals(directory, currentDirectory);
        }
    }
}
