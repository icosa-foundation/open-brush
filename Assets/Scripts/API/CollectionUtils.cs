using System;
using System.Collections.Generic;

namespace TiltBrush
{
    internal static class CollectionUtils
    {
        public static void AddRange<T>(this IList<T> initial, IEnumerable<T> collection)
        {
            if (initial == null)
                throw new ArgumentNullException(nameof(initial));
            if (collection == null)
                return;
            foreach (T obj in collection)
                initial.Add(obj);
        }
    }
}
