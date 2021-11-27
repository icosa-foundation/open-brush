using System;

namespace SVGMeshUnity.Internals
{
    public static class Sort<T> where T : IComparable<T>
    {
        public static void QuickSort(T[] elements, int left, int right)
        {
            var i = left;
            var j = right;
            var pivot = elements[(left + right) >> 1];
 
            while (i <= j)
            {
                while (elements[i].CompareTo(pivot) < 0)
                {
                    ++i;
                }
 
                while (elements[j].CompareTo(pivot) > 0)
                {
                    --j;
                }
 
                if (i <= j)
                {
                    // Swap
                    var tmp = elements[i];
                    elements[i] = elements[j];
                    elements[j] = tmp;
 
                    i++;
                    j--;
                }
            }
 
            // Recursive calls
            if (left < j)
            {
                QuickSort(elements, left, j);
            }
 
            if (i < right)
            {
                QuickSort(elements, i, right);
            }
        }
    }
}