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

using System.Collections.Generic;

namespace TiltBrush
{
    /// Carries the strokes of the active mirror along when the user moves the mirror.
    ///
    /// The symmetry transforms are read from PointerManager at the start and end of the move
    /// rather than derived here, so what the strokes follow is exactly what drawing would have
    /// produced at each pose.
    ///
    /// The strokes move when the mirror is let go of, not while it is being dragged: following it
    /// live means rewriting every affected stroke's geometry every frame, and a mirror can own an
    /// entire sketch's worth of strokes.
    public static class SymmetryMirrorMove
    {
        private static SymmetryMirror m_Mirror;
        private static List<TrTransform> m_TransformsAtStart;

        /// Call when the user takes hold of the mirror.
        public static void Begin()
        {
            m_Mirror = null;
            m_TransformsAtStart = null;
            if (!SymmetryPeerEditing.Enabled) { return; }

            var mirror = SymmetryMirrors.Active;
            if (mirror == null) { return; }
            var transforms = PointerManager.m_Instance.GetSymmetryTransforms_CS();
            if (transforms.Count == 0) { return; }

            m_Mirror = mirror;
            m_TransformsAtStart = transforms;
        }

        /// Call when the user lets go of it. Records the move as a single command, so that undo
        /// puts the strokes back.
        public static void End()
        {
            var mirror = m_Mirror;
            var before = m_TransformsAtStart;
            m_Mirror = null;
            m_TransformsAtStart = null;
            if (mirror == null || before == null) { return; }

            var after = PointerManager.m_Instance.GetSymmetryTransforms_CS();
            if (after.Count != before.Count || Unchanged(before, after)) { return; }

            var command = new MoveMirrorStrokesCommand(
                mirror, before, after, SymmetrySettingsSnapshot.FromCurrentSettings());
            if (command.MovesAnything)
            {
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
            }
            else
            {
                // Nothing was drawn under this mirror yet; just keep its settings current.
                mirror.Settings = SymmetrySettingsSnapshot.FromCurrentSettings();
            }
        }

        private static bool Unchanged(IList<TrTransform> before, IList<TrTransform> after)
        {
            for (int i = 0; i < before.Count; ++i)
            {
                if (!TrTransform.Approximately(before[i], after[i])) { return false; }
            }
            return true;
        }
    }
} // namespace TiltBrush
