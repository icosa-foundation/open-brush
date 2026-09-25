// Copyright 2026 The Open Brush Authors
using System.Collections.Generic;
using System.Linq;

namespace TiltBrush
{
    // Watcher callbacks produce changes; the main-thread scan consumes a batch.
    // Keep one synchronization object across batches.
    internal sealed class CatalogChangeQueue
    {
        private readonly object m_Lock = new object();
        private readonly HashSet<string> m_Paths = new HashSet<string>();
        public void Add(string path)
        {
            if (path == null) { return; }
            lock (m_Lock) { m_Paths.Add(path); }
        }
        public string[] Drain()
        {
            lock (m_Lock)
            {
                string[] paths = m_Paths.ToArray();
                m_Paths.Clear();
                return paths;
            }
        }
        public void Clear()
        {
            lock (m_Lock) { m_Paths.Clear(); }
        }
    }
}
