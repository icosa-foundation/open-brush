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
    /// A linked mirror: a mirror the user created explicitly, which owns the strokes drawn with it.
    ///
    /// A mirror has an identity of its own, separate from its settings. The settings are the
    /// mirror's current state - where it is and how many copies it makes - and changing them
    /// changes this mirror, carrying its strokes along; it never makes a different one. Strokes
    /// drawn with it are grouped, and an edit to one member of a group applies to all of them.
    ///
    /// Plain symmetry has no mirror object at all: strokes drawn with it are ordinary strokes.
    public class SymmetryMirror
    {
        public Guid Id { get; }

        /// The shared settings for every linked group belonging to this mirror.
        public SymmetrySettingsSnapshot Settings { get; set; }

        /// Linked groups belonging to this mirror use one canvas-space transform basis.
        public CanvasScript Canvas { get; internal set; }

        public SymmetryMirror(Guid id, SymmetrySettingsSnapshot settings)
        {
            Id = id;
            Settings = settings;
        }

        public override string ToString() => $"Mirror {Id.ToString().Substring(0, 8)} ({Settings})";
    }

    /// The linked mirrors in the current sketch, and which one the widget is showing.
    ///
    /// Mirrors are only ever created by the user asking for one (NewLinked). Drawing, moving the
    /// widget, changing settings and undo never create or switch mirrors on their own; every
    /// change of the active mirror goes through ActivateMirrorCommand, so undo restores it.
    public static class SymmetryMirrors
    {
        private static readonly Dictionary<Guid, SymmetryMirror> m_Mirrors =
            new Dictionary<Guid, SymmetryMirror>();
        // The same mirrors, oldest first, so that recalling one by position is stable.
        private static readonly List<SymmetryMirror> m_Order = new List<SymmetryMirror>();
        private static SymmetryMirror m_Active;
        // Set while a command is putting settings back, so the change isn't recorded again.
        private static bool m_ApplyingSettings;

        /// The linked mirrors, oldest first.
        public static IReadOnlyList<SymmetryMirror> All => m_Order;

        public static int Count => m_Order.Count;

        /// The linked mirror the widget is showing, or null for plain symmetry.
        ///
        /// Only commands and loading set this; user actions go through the methods below so that
        /// they can be undone.
        public static SymmetryMirror Active
        {
            get { return m_Active; }
            internal set
            {
                if (!ReferenceEquals(m_Active, value))
                {
                    SymmetryMirrorMove.End();
                    SymmetryPeerPreview.Hide();
                }
                if (value != null) { Register(value); }
                m_Active = value;
            }
        }

        /// The active linked mirror if the widget is currently showing it, else null. Turning
        /// symmetry off leaves the mirror active, so turning it back on resumes it; switching to
        /// a different symmetry mode leaves it dormant until that mode returns.
        public static SymmetryMirror Showing
        {
            get
            {
                var pm = PointerManager.m_Instance;
                if (m_Active?.Settings == null || pm == null ||
                    !IsSpatial(pm.CurrentSymmetryMode) ||
                    m_Active.Settings.Mode != pm.CurrentSymmetryMode)
                {
                    return null;
                }
                return m_Active;
            }
        }

        /// The mirror new symmetric strokes join, or null if they should be ordinary strokes.
        /// A mirror's transforms are in its own canvas, so drawing into another layer is plain.
        public static SymmetryMirror LinkingMirror
        {
            get
            {
                var mirror = Showing;
                if (mirror == null) { return null; }
                mirror.Canvas ??= App.Scene.ActiveCanvas;
                return mirror.Canvas == App.Scene.ActiveCanvas ? mirror : null;
            }
        }

        internal static bool IsSpatial(PointerManager.SymmetryMode mode) =>
            mode == PointerManager.SymmetryMode.SinglePlane ||
            mode == PointerManager.SymmetryMode.MultiMirror;

        // ---- User actions; each records an undoable command --------------------------------- //

        /// Creates a new linked mirror from the current settings and makes it active. Always a
        /// fresh mirror, even while another linked mirror is active.
        public static SymmetryMirror NewLinked()
        {
            var settings = SymmetrySettingsSnapshot.FromCurrentSettings();
            if (settings == null || !IsSpatial(settings.Mode)) { return null; }
            var mirror = new SymmetryMirror(Guid.NewGuid(), settings)
            {
                Canvas = App.Scene.ActiveCanvas
            };
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new ActivateMirrorCommand(mirror, created: true));
            return mirror;
        }

        /// Makes an earlier linked mirror active again and puts the widget where it has it.
        public static void Recall(SymmetryMirror mirror)
        {
            if (mirror == null || ReferenceEquals(mirror, m_Active)) { return; }
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new ActivateMirrorCommand(mirror, created: false));
        }

        /// Makes the mirror that owns this stroke active.
        public static void RecallFromStroke(Stroke stroke)
        {
            Recall(stroke?.SymmetryPeerGroup?.Mirror);
        }

        /// Switches to plain symmetry. Linked mirrors keep their strokes.
        public static void UsePlain()
        {
            if (m_Active == null) { return; }
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new ActivateMirrorCommand(null, created: false));
        }

        // ---- Settings ----------------------------------------------------------------------- //

        /// Called whenever a symmetry setting changes (PointerManager.CalculateMirrors). The
        /// widget is the active mirror, so a change to its settings changes that mirror and its
        /// strokes follow: a change that keeps the number of copies moves them, and one that
        /// doesn't rebuilds each group from one of its members. Pose changes arrive through
        /// SymmetryMirrorMove instead, so only the parameters are compared here.
        public static void NoteSettingsChanged()
        {
            if (m_ApplyingSettings || SymmetryMirrorMove.IsMoving) { return; }
            var mirror = Showing;
            if (mirror == null) { return; }
            var settings = SymmetrySettingsSnapshot.FromCurrentSettings(mirror.Canvas);
            if (settings == null || mirror.Settings.SameParameters(settings)) { return; }
            EndSelectionOwnedBy(mirror);

            BaseCommand command = mirror.Settings.HasCompatibleTopology(settings)
                ? (BaseCommand)new MoveMirrorSettingsCommand(mirror, mirror.Settings, settings)
                : new RegenerateMirrorStrokesCommand(mirror, mirror.Settings, settings);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
        }

        /// Ends the selection (baking any move into its strokes) if it holds a copy owned by
        /// 'mirror'. A selected copy is staged in the selection canvas with its peers displaced
        /// by the live preview, so a mirror move or settings change can't carry it; committing
        /// the selection first gives both operations a settled group to work on.
        internal static void EndSelectionOwnedBy(SymmetryMirror mirror)
        {
            var selection = SelectionManager.m_Instance;
            if (mirror == null || selection == null || !selection.HasSelection) { return; }
            bool owned = false;
            foreach (var stroke in selection.SelectedStrokes)
            {
                owned |= ReferenceEquals(stroke.SymmetryPeerGroup?.Mirror, mirror);
            }
            if (owned) { selection.ClearActiveSelection(); }
        }

        /// Puts the symmetry settings and widget back as a snapshot has them without the change
        /// being recorded or carrying any strokes; for commands restoring an earlier state.
        internal static void ApplySettingsUnrecorded(SymmetrySettingsSnapshot settings)
        {
            if (settings == null) { return; }
            m_ApplyingSettings = true;
            try
            {
                settings.ApplyToCurrentSettings(recordCommand: false);
                PointerManager.m_Instance.CalculateMirrors();
            }
            finally
            {
                m_ApplyingSettings = false;
            }
        }

        /// The groups currently owned by a mirror.
        internal static List<SymmetryStrokeGroup> GroupsOf(SymmetryMirror mirror)
        {
            var groups = new List<SymmetryStrokeGroup>();
            var seen = new HashSet<SymmetryStrokeGroup>();
            foreach (var stroke in SketchMemoryScript.AllStrokes())
            {
                var group = stroke.SymmetryPeerGroup;
                if (group != null && ReferenceEquals(group.Mirror, mirror) && seen.Add(group))
                {
                    groups.Add(group);
                }
            }
            return groups;
        }

        // ---- Registry ----------------------------------------------------------------------- //

        internal static void Register(SymmetryMirror mirror)
        {
            if (mirror == null || m_Mirrors.ContainsKey(mirror.Id)) { return; }
            m_Mirrors[mirror.Id] = mirror;
            m_Order.Add(mirror);
        }

        /// Undoing the creation of a mirror takes it out of the list again.
        internal static void Unregister(SymmetryMirror mirror)
        {
            if (mirror == null || !m_Mirrors.Remove(mirror.Id)) { return; }
            m_Order.Remove(mirror);
            if (ReferenceEquals(m_Active, mirror)) { Active = null; }
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
                Register(mirror);
            }
            return mirror;
        }

        public static void Clear()
        {
            SymmetryMirrorMove.Forget();
            SymmetryPeerPreview.Hide();
            m_Mirrors.Clear();
            m_Order.Clear();
            m_Active = null;
        }
    }
} // namespace TiltBrush
