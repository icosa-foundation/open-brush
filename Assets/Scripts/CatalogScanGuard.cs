using System;

namespace TiltBrush
{
    internal static class CatalogScanGuard
    {
        /// The root is fixed for the lifetime of a run, so a scan can only be superseded by a
        /// newer scan, a replaced backend, or the user navigating elsewhere.
        internal static bool IsCurrent(
            int generation, int currentGeneration,
            object backend, object currentBackend,
            string directory, string currentDirectory,
            StringComparer pathComparer)
        {
            return generation == currentGeneration &&
                ReferenceEquals(backend, currentBackend) &&
                pathComparer.Equals(directory, currentDirectory);
        }
    }
}
