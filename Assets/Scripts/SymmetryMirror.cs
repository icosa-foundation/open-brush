// Copyright 2024 The Open Brush Authors
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

namespace TiltBrush
{
    /// A mirror the user draws with: the thing the symmetry widget stands for.
    ///
    /// A mirror has an identity of its own, separate from its settings. Strokes are linked to the
    /// mirror they were drawn under, not to the values it happened to have at the time, so moving
    /// a mirror can carry its strokes with it however much its settings have changed since.
    ///
    /// One mirror is active at a time - the one the widget is showing - but a sketch keeps the
    /// ones it has finished with, so they can be recalled and their strokes edited again.
    public class SymmetryMirror
    {
        public Guid Id { get; }

        /// What the mirror is set to now. The settings a given group of strokes was drawn under
        /// are recorded on the group, which is what lets the two drift apart.
        public SymmetrySettingsSnapshot Settings { get; set; }

        public SymmetryMirror(Guid id, SymmetrySettingsSnapshot settings)
        {
            Id = id;
            Settings = settings;
        }

        public override string ToString() => $"Mirror {Id.ToString().Substring(0, 8)} ({Settings})";
    }

    /// The mirrors in the current sketch, and which one the widget is showing.
    public static class SymmetryMirrors
    {
        private static readonly Dictionary<Guid, SymmetryMirror> m_Mirrors =
            new Dictionary<Guid, SymmetryMirror>();
        private static SymmetryMirror m_Active;

        /// Mirrors are kept even when nothing references them, so that a sketch can offer them
        /// back to the user.
        public static IEnumerable<SymmetryMirror> All => m_Mirrors.Values;

        public static int Count => m_Mirrors.Count;

        /// The mirror the widget is showing, which new strokes are linked to. Null only before
        /// anything has been drawn with symmetry.
        public static SymmetryMirror Active
        {
            get { return m_Active; }
            set
            {
                if (value != null && !m_Mirrors.ContainsKey(value.Id))
                {
                    m_Mirrors[value.Id] = value;
                }
                m_Active = value;
            }
        }

        /// The active mirror, created from the symmetry settings in force if there isn't one.
        public static SymmetryMirror EnsureActive()
        {
            if (m_Active == null)
            {
                Active = Create(SymmetrySettingsSnapshot.FromCurrentSettings());
            }
            return m_Active;
        }

        /// Starts a new mirror and makes it active, leaving the strokes of the previous one
        /// behind: this is how a second symmetric object is begun without disturbing the first.
        public static SymmetryMirror Create(SymmetrySettingsSnapshot settings)
        {
            var mirror = new SymmetryMirror(Guid.NewGuid(), settings);
            m_Mirrors[mirror.Id] = mirror;
            m_Active = mirror;
            return mirror;
        }

        public static SymmetryMirror Get(Guid id)
        {
            m_Mirrors.TryGetValue(id, out var mirror);
            return mirror;
        }

        /// Used when loading: finds the mirror with this id, or records the one the file
        /// describes. Does not change which mirror is active.
        public static SymmetryMirror GetOrCreate(Guid id, SymmetrySettingsSnapshot settings)
        {
            if (id == Guid.Empty) { return null; }
            if (!m_Mirrors.TryGetValue(id, out var mirror))
            {
                mirror = new SymmetryMirror(id, settings);
                m_Mirrors[id] = mirror;
            }
            return mirror;
        }

        /// Makes a mirror active and puts the symmetry settings back the way it has them, so the
        /// widget shows it again. Moves no strokes: recalling a mirror only changes what the
        /// user is drawing and editing with.
        public static void Recall(SymmetryMirror mirror)
        {
            if (mirror == null) { return; }
            Active = mirror;
            mirror.Settings?.ApplyToCurrentSettings();
        }

        public static void Clear()
        {
            m_Mirrors.Clear();
            m_Active = null;
        }
    }
} // namespace TiltBrush
