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
    /// Carries a rigid move of one copy of a linked group to the other copies, without rebuilding
    /// them: the fast path of decision 8 for translations, rotations and uniform scale.
    ///
    /// When copy s moves by xf (canvas space), copy j's exact new place is C·xf·C⁻¹ applied to
    /// where it is, C being the mirror's transform from s to j. That is a proper rigid transform
    /// even when C is a reflection, so moving the copy's existing vertices gives what rebuilding
    /// would. Batched copies move in place; others (and any in-place move that fails) have their
    /// control points moved and their geometry rebuilt. Erased copies stay erased.
    ///
    /// Copies the selection preview has already moved by exactly this much can be adopted rather
    /// than moved again (AdoptFromPreview), so baking a selection costs nothing for them.
    public class TransformSymmetryCopiesCommand : BaseCommand
    {
        private readonly List<Stroke> m_Copies = new List<Stroke>();
        private readonly List<TrTransform> m_Steps = new List<TrTransform>();
        // Copies whose first move was done by the preview.
        private readonly HashSet<Stroke> m_Adopted = new HashSet<Stroke>(new ReferenceComparer<Stroke>());

        /// 'xf' is the canvas-space move 'driver' undergoes. Copies listed in 'movedRaw' have
        /// already been moved by xf itself (they were part of the same edit), so they get only
        /// the correction to their mirrored move.
        public TransformSymmetryCopiesCommand(Stroke driver, TrTransform xf,
            ICollection<Stroke> movedRaw = null, BaseCommand parent = null) : base(parent)
        {
            if (!SymmetryPeerEditing.IsLinked(driver)) { return; }
            foreach (var copy in driver.SymmetryPeerGroup.Strokes)
            {
                // A selected copy is moved by whatever moves the selection.
                if (ReferenceEquals(copy, driver) ||
                    SelectionManager.m_Instance.IsStrokeSelected(copy) ||
                    !SymmetryPeerEditing.TryGetPeerSymmetryTransform(driver, copy, out var toCopy))
                {
                    continue;
                }
                TrTransform step = toCopy * xf * toCopy.inverse;
                if (movedRaw != null && movedRaw.Contains(copy)) { step = step * xf.inverse; }
                if (!step.IsFinite() || step == TrTransform.identity) { continue; }
                m_Copies.Add(copy);
                m_Steps.Add(step);
            }
        }

        public override bool NeedsSave => m_Copies.Count > 0;

        /// For edits that move selected strokes within the selection - each stroke's control
        /// points changing by its own left transform L, in selection-canvas space - rather than
        /// by moving the selection itself, which the preview and the deselect bake already
        /// follow. A selected stroke's resting place moves by S·L·S⁻¹ in canvas space (S the
        /// selection transform), and its unselected copies by their mirrored version of that,
        /// applied where they are: the preview's own displacement of them stays valid.
        /// Adds one child command per linked group to 'parent'.
        public static void ForSelectionEdit(IEnumerable<(Stroke stroke, TrTransform local)> edits,
            BaseCommand parent)
        {
            TrTransform selection = SelectionManager.m_Instance.SelectionTransform;
            var seen = new HashSet<SymmetryStrokeGroup>();
            foreach (var (stroke, local) in edits)
            {
                if (local == TrTransform.identity || !SymmetryPeerEditing.IsLinked(stroke) ||
                    !seen.Add(stroke.SymmetryPeerGroup))
                {
                    continue;
                }
                new TransformSymmetryCopiesCommand(
                    stroke, selection * local * selection.inverse, null, parent);
            }
        }

        /// Takes over copies the selection preview has already moved by their step, so the
        /// next redo leaves them where they are. Call before the preview is hidden.
        public void AdoptFromPreview()
        {
            for (int i = 0; i < m_Copies.Count; ++i)
            {
                if (SymmetryPeerPreview.TryCommit(m_Copies[i], m_Steps[i]))
                {
                    m_Adopted.Add(m_Copies[i]);
                }
            }
        }

        protected override void OnRedo()
        {
            for (int i = 0; i < m_Copies.Count; ++i)
            {
                if (m_Adopted.Remove(m_Copies[i])) { continue; }
                MirrorStrokeEdits.ApplyLeftTransform(m_Copies[i], m_Steps[i]);
            }
        }

        protected override void OnUndo()
        {
            for (int i = m_Copies.Count - 1; i >= 0; --i)
            {
                MirrorStrokeEdits.ApplyLeftTransform(m_Copies[i], m_Steps[i].inverse);
            }
        }
    }
} // namespace TiltBrush
