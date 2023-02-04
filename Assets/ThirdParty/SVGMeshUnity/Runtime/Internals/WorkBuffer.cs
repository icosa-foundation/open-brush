using System;
using System.Linq;
using UnityEngine;

namespace SVGMeshUnity.Internals
{
    public class WorkBuffer<T>
    {
        public WorkBuffer(int size = 32)
        {
            GrowSize = size;
            PrivateData = new T[size];
        }
        
        public T[] Data
        {
            get { return PrivateData; }
        }
        public int UsedSize
        {
            get { return PrivateUsedSize; }
        }

        public Func<T> NewForClass;

        private int GrowSize;
        private T[] PrivateData;
        private int PrivateUsedSize;

        private void Grow(int size)
        {
            var newPrivateData = new T[size];
            PrivateData.CopyTo(newPrivateData, 0);
            PrivateData = newPrivateData;
        }

        private void GrowIfNeeded()
        {
            if (PrivateData.Length == PrivateUsedSize)
            {
                Grow(PrivateData.Length + GrowSize);
            }
        }

        public void Extend(int size)
        {
            if (PrivateData.Length < size)
            {
                Grow(size);
            }
        }

        public void Fill(ref T val, int n)
        {
            if (PrivateData.Length < n)
            {
                Grow(n);
            }

            for (var i = 0; i < n; ++i)
            {
                PrivateData[i] = val;
            }

            PrivateUsedSize = n;
        }

        public void Push(ref T val)
        {
            GrowIfNeeded();
            PrivateData[PrivateUsedSize] = val;
            ++PrivateUsedSize;
        }

        public T Push()
        {
            GrowIfNeeded();

            var val = PrivateData[PrivateUsedSize];

            if (val == null)
            {
                val = NewForClass();
                PrivateData[PrivateUsedSize] = val;
            }

            ++PrivateUsedSize;

            return val;
        }

        public T Pop()
        {
            var val = PrivateData[PrivateUsedSize - 1];
            --PrivateUsedSize;
            return val;
        }

        public T Insert(int index)
        {
            if (index == PrivateUsedSize)
            {
                return Push();
            }

            GrowIfNeeded();

            var val = PrivateData[PrivateUsedSize];

            for (var i = PrivateUsedSize - 1; i >= index; --i)
            {
                PrivateData[i + 1] = PrivateData[i];
            }

            if (val == null)
            {
                val = NewForClass();
            }

            PrivateData[index] = val;

            ++PrivateUsedSize;

            return val;
        }

        public void RemoveAt(int index)
        {
            var old = PrivateData[index];
            
            for (var i = index; i < PrivateUsedSize - 1; ++i)
            {
                PrivateData[i] = PrivateData[i + 1];
            }

            PrivateData[PrivateUsedSize - 1] = old;

            --PrivateUsedSize;
        }

        public static void Sort<G>(WorkBuffer<G> buf) where G : IComparable<G>
        {
            Internals.Sort<G>.QuickSort(buf.PrivateData, 0, buf.PrivateUsedSize - 1);
        }

        public void RemoveLast(int n)
        {
            PrivateUsedSize -= n;
        }

        public void Clear()
        {
            PrivateUsedSize = 0;
        }

        public void Dump()
        {
            Debug.Log(PrivateData.Take(PrivateUsedSize).Aggregate("", (_, s) => _ + s.ToString() + "\n"));
        }

        public void DumpHash()
        {
            Debug.LogFormat("{0}{1}", PrivateUsedSize, PrivateData.Select(_ => string.Format("{0:x}",_ != null ? _.GetHashCode() : 0)).Aggregate("", (_, s) => _ + ", " + s));
        }
    }
}