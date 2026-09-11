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

using System.Collections.Generic;
using System.Linq;

namespace TiltBrush
{
    /// Retains the source strokes unchanged and swaps their visibility with cropped replacements.
    internal sealed class CropStrokesCommand : BaseCommand
    {
        private readonly Stroke[] m_OriginalList;
        private readonly Dictionary<Stroke, List<Stroke>> m_Replacements;
        private readonly List<Stroke> m_Result;
        private readonly Stroke[] m_CroppedList;
        private readonly HashSet<Stroke> m_SelectedOriginals;
        private readonly Stroke m_LastSelected;
        private bool m_Created;
        private bool m_Applied;

        public CropStrokesCommand(Stroke[] originals, Dictionary<Stroke, List<Stroke>> replacements,
            List<Stroke> result, List<Stroke> liveList, BaseCommand parent = null) : base(parent)
        {
            m_OriginalList = originals;
            m_Replacements = replacements;
            m_Result = liveList;
            m_CroppedList = result.ToArray();
            var selection = SelectionManager.m_Instance;
            m_SelectedOriginals = new HashSet<Stroke>(replacements.Keys.Where(
                stroke => selection != null && selection.IsStrokeSelected(stroke)));
            m_LastSelected = selection != null ? selection.LastSelectedStroke : null;
        }

        public override bool NeedsSave => true;

        protected override void OnRedo()
        {
            // API/ToolScript groups execute once immediately, then again when the group is recorded.
            if (m_Applied) return;
            if (!m_Created)
            {
                // Build every replacement before hiding any source, so a brush failure keeps the originals.
                try
                {
                    foreach (var replacement in m_Replacements.Values.SelectMany(value => value))
                    {
                        SketchMemoryScript.m_Instance.MemoryListAdd(replacement);
                        replacement.Recreate(null, replacement.m_IntendedCanvas);
                    }
                    m_Created = true;
                }
                catch
                {
                    DestroyReplacements();
                    throw;
                }
                foreach (var replacement in m_Replacements.Values.SelectMany(value => value))
                    TiltMeterScript.m_Instance.AdjustMeter(replacement, up: true);
            }
            else
            {
                foreach (var replacement in m_Replacements.Values.SelectMany(value => value))
                    replacement.Hide(false);
            }

            var selection = SelectionManager.m_Instance;
            foreach (var entry in m_Replacements)
            {
                DeregisterIfSelected(entry.Key);
                entry.Key.Hide(true);
                if (selection != null && m_SelectedOriginals.Contains(entry.Key))
                    selection.RegisterStrokesInSelectionCanvas(entry.Value);
            }
            if (selection != null && m_LastSelected != null && m_Replacements.TryGetValue(m_LastSelected, out var parts))
                selection.LastSelectedStroke = parts.LastOrDefault();
            m_Result.Clear();
            m_Result.AddRange(m_CroppedList);
            m_Applied = true;
        }

        protected override void OnUndo()
        {
            if (!m_Applied) return;
            foreach (var replacement in m_Replacements.Values.SelectMany(value => value))
            {
                DeregisterIfSelected(replacement);
                replacement.Hide(true);
            }
            foreach (var original in m_Replacements.Keys) original.Hide(false);
            var selection = SelectionManager.m_Instance;
            if (selection != null)
            {
                selection.RegisterStrokesInSelectionCanvas(m_SelectedOriginals);
                selection.LastSelectedStroke = m_LastSelected;
            }
            // Lua callers retain this list: keep it synchronized with Undo as well as Redo.
            m_Result.Clear();
            m_Result.AddRange(m_OriginalList);
            m_Applied = false;
        }

        private static void DeregisterIfSelected(Stroke stroke)
        {
            var selection = SelectionManager.m_Instance;
            if (selection != null && selection.IsStrokeSelected(stroke))
                selection.DeregisterStrokesInSelectionCanvas(new[] { stroke });
        }

        private void DestroyReplacements()
        {
            foreach (var stroke in m_Replacements.Values.SelectMany(value => value))
            {
                if (stroke.m_NodeByTime.List != null) SketchMemoryScript.m_Instance.RemoveMemoryObject(stroke);
                if (stroke.m_Type != Stroke.Type.NotCreated) stroke.DestroyStroke();
            }
        }

        protected override void OnDispose()
        {
            // Called when discarded redo history is cleared or the sketch is reset.
            // Sources belong to their existing history entries; this command owns only its replacements.
            DestroyReplacements();
        }
    }
}
