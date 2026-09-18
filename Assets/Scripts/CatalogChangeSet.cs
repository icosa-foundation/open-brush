using System;
using System.Collections.Generic;
using System.Linq;

namespace TiltBrush
{
    internal static class CatalogChangeSet
    {
        internal static string[] GetChangedDetectedPaths(
            IEnumerable<string> changedPaths, IEnumerable<string> detectedPaths,
            StringComparer pathComparer)
        {
            var detected = new HashSet<string>(detectedPaths, pathComparer);
            return changedPaths.Where(detected.Contains).Distinct(pathComparer).ToArray();
        }
    }
}
