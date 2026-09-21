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
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TiltBrush
{
    internal sealed class ModelRestoreGate
    {
        private readonly object m_Lock = new object();
        private readonly HashSet<string> m_InFlightKeys = new HashSet<string>();
        private int m_Generation;

        public int Generation
        {
            get
            {
                lock (m_Lock)
                {
                    return m_Generation;
                }
            }
        }

        public void Invalidate()
        {
            lock (m_Lock)
            {
                ++m_Generation;
                m_InFlightKeys.Clear();
            }
        }

        public async Task<bool> RunAsync(
            string key,
            Func<Func<bool>, Task<bool>> restore,
            Action onSuccess,
            Func<bool> isSourceCurrent)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (restore == null) throw new ArgumentNullException(nameof(restore));
            if (onSuccess == null) throw new ArgumentNullException(nameof(onSuccess));
            if (isSourceCurrent == null) throw new ArgumentNullException(nameof(isSourceCurrent));

            int generation;
            lock (m_Lock)
            {
                generation = m_Generation;
                if (!m_InFlightKeys.Add(key))
                {
                    return false;
                }
            }

            Func<bool> current = () => generation == Generation && isSourceCurrent();
            try
            {
                if (!current())
                {
                    return false;
                }

                bool restored = await restore(current);
                if (!restored || !current())
                {
                    return false;
                }

                onSuccess();
                return true;
            }
            finally
            {
                lock (m_Lock)
                {
                    if (generation == m_Generation)
                    {
                        m_InFlightKeys.Remove(key);
                    }
                }
            }
        }
    }
}
