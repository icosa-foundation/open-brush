// Copyright 2020 The Tilt Brush Authors
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

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace TiltBrush
{
    public class DuplicateSelectionCommand : BaseCommand
    {
        private List<Stroke> m_SelectedStrokes;
        private List<GrabWidget> m_SelectedWidgets;

        private List<Stroke> m_DuplicatedStrokes;
        private List<GrabWidget> m_DuplicatedWidgets;
        // A selected copy stands for its linked group. Duplicating it duplicates the group: these
        // are the duplicates of its other copies, which are placed but never selected.
        private readonly List<Stroke> m_DuplicatedCopies = new List<Stroke>();
        private bool m_FirstRedo = true;
        private readonly List<GrabWidget> m_DuplicatedWidgetPeers = new List<GrabWidget>();
        private readonly List<TransformWidgetPeersCommand> m_OriginalWidgetMoves = new List<TransformWidgetPeersCommand>();
        // The original groups' other copies, baked with the selection's move as a deselect
        // would bake them. Held here rather than as children: Merge expects its only child to
        // be the stamp's selection move.
        private readonly List<TransformSymmetryCopiesCommand> m_OriginalCopyMoves =
            new List<TransformSymmetryCopiesCommand>();

        private TrTransform m_OriginTransform;
        private TrTransform m_DuplicateTransform;

        private CanvasScript m_CurrentCanvas;

        private bool m_DupeInPlace;
        private bool m_NoSymmetrySpecialCase;

        public DuplicateSelectionCommand(TrTransform xf, BaseCommand parent = null) : base(parent)
        {
            m_CurrentCanvas = App.ActiveCanvas;
            m_OriginTransform = SelectionManager.m_Instance.SelectionTransform;
            m_DuplicateTransform = xf;
            m_DupeInPlace = m_OriginTransform == m_DuplicateTransform;

            // Gather duplicate transforms based on current symmetry mode.
            // Use Unity transforms and Matrix4x4 because we are going
            // to be dealing with non-uniform scale.

            // Save selected strokes.
            m_SelectedStrokes = SelectionManager.m_Instance.SelectedStrokes.ToList();
            // Save selected widgets.
            m_SelectedWidgets = SelectionManager.m_Instance.SelectedWidgets.ToList();

            m_DuplicatedStrokes = new List<Stroke>();
            m_DuplicatedWidgets = new List<GrabWidget>();
            var xfSymmetriesGS = PointerManager.m_Instance.GetSymmetriesForCurrentMode();
            if (xfSymmetriesGS.Count == 0)
            {
                // Special case for non-symmetry to match legacy code. Duplicate
                // selection into selection canvas, deselect the old selection,
                // and change the selection transform to apply the transform
                // parameter. Is this necessary...? Probably better safe than
                // sorry.
                m_NoSymmetrySpecialCase = true;

                // The copies the preview displaces go back to rest, so their duplicates can be
                // placed from where they really are.
                SymmetryPeerPreview.Hide();

                // Duplicate strokes.
                var duplicatedGroups = new HashSet<SymmetryStrokeGroup>();
                foreach (var stroke in m_SelectedStrokes)
                {
                    var duplicate = SketchMemoryScript.m_Instance.DuplicateStroke(
                        stroke, App.Scene.SelectionCanvas, null);
                    m_DuplicatedStrokes.Add(duplicate);
                    if (SymmetryPeerEditing.IsLinked(stroke) &&
                        duplicatedGroups.Add(stroke.SymmetryPeerGroup))
                    {
                        DuplicateLinkedGroup(stroke, duplicate);
                    }
                }

                // Duplicate widgets.
                foreach (var widget in m_SelectedWidgets)
                {
                    var duplicate = widget.Clone();
                    m_DuplicatedWidgets.Add(duplicate);
                    if (widget.SymmetryPeerGroup != null) { DuplicateWidgetGroup(widget, duplicate); }
                }
            }
            else
            {
                // The new way, which works with arbitrary mirror matrices.
                // Leave selection untouched and apply all transforms at 
                // creation time.

                if (!m_DupeInPlace)
                {
                    // Apply transform parameter.
                    var xfDelta = m_DuplicateTransform * m_OriginTransform.inverse;
                    var appScale = TrTransform.S(App.Scene.Pose.scale);
                    var xfDeltaScaleAdj = appScale * xfDelta;
                    xfDeltaScaleAdj.scale = xfDelta.scale;
                    for (int i = 0; i < xfSymmetriesGS.Count; i++)
                    {
                        xfSymmetriesGS[i] = xfSymmetriesGS[i] * xfDeltaScaleAdj;
                    }
                }

                // Pre-calculate left transforms for canvas space.
                var xfSymmetriesCS = new List<TrTransform>(xfSymmetriesGS);
                var xfGSfromCS = App.Scene.SelectionCanvas.Pose;
                var xfCSfromGS = m_CurrentCanvas.Pose.inverse;
                for (int i = 0; i < xfSymmetriesGS.Count; i++)
                {
                    xfSymmetriesCS[i] = xfCSfromGS * xfSymmetriesGS[i] * xfGSfromCS;
                }

                // With a linked mirror active, each selected stroke produces its own new set of
                // symmetry peers under it, rather than joining the source's group.
                var mirror = SymmetryMirrors.LinkingMirror;
                bool linkMirrorCopies = mirror != null;

                // With plain symmetry, a selected copy's linked partners are duplicated through
                // the symmetry too, as if they had been selected. They aren't in the selection
                // canvas, so their transforms start from their own canvas. Only for a duplicate
                // made in place: a moved duplicate would need each partner's mirrored move.
                if (!linkMirrorCopies && m_DupeInPlace)
                {
                    var xfCSfromGSPartners = m_CurrentCanvas.Pose.inverse;
                    foreach (var copy in SymmetryPeerEditing.PeersOutside(m_SelectedStrokes))
                    {
                        for (int i = 0; i < xfSymmetriesGS.Count; i++)
                        {
                            var xfCopy = xfCSfromGSPartners * xfSymmetriesGS[i] * copy.Canvas.Pose;
                            m_DuplicatedStrokes.Add(SketchMemoryScript.m_Instance.DuplicateStroke(
                                copy, m_CurrentCanvas, xfCopy, absoluteScale: true));
                        }
                    }
                }

                // Duplicate strokes.
                foreach (var stroke in m_SelectedStrokes)
                {
                    var group = linkMirrorCopies ? new SymmetryStrokeGroup(mirror) : null;
                    for (int i = 0; i < xfSymmetriesCS.Count; i++)
                    {
                        var duplicate = SketchMemoryScript.m_Instance.DuplicateStroke(
                            stroke, m_CurrentCanvas, xfSymmetriesCS[i], absoluteScale: true);
                        if (linkMirrorCopies)
                        {
                            duplicate.JoinSymmetryGroup(group, i);
                        }
                        m_DuplicatedStrokes.Add(duplicate);
                    }
                    group?.CaptureInstances();
                }

                // Duplicate widgets.
                foreach (var widget in m_SelectedWidgets)
                {
                    // Generally speaking we want both sides of 2d media to appear
                    // when duplicating using multi-mirror.
                    bool duplicateAsTwoSided = widget is Media2dWidget;
                    var widgetGroup = linkMirrorCopies && SymmetryWidgetGroup.CanLink(widget)
                        ? new SymmetryWidgetGroup(mirror) : null;

                    for (int i = 0; i < xfSymmetriesGS.Count; i++)
                    {
                        var duplicatedWidget = widget.Clone();
                        var widgetXf = Coords.AsGlobal[duplicatedWidget.GrabTransform_GS];
                        widgetXf.scale = duplicatedWidget.GetSignedWidgetSize();

                        if (duplicateAsTwoSided)
                        {
                            ((Media2dWidget)duplicatedWidget).TwoSided = true;
                        }

                        var mat = xfSymmetriesGS[i] * widgetXf;
                        duplicatedWidget.GrabTransform_GS.SetPositionAndRotation(
                            position: mat.translation,
                            rotation: mat.rotation);
                        duplicatedWidget.SetSignedWidgetSize(mat.scale);
                        if (widgetGroup != null) { duplicatedWidget.SetSymmetryGroup(widgetGroup, i); }

                        m_DuplicatedWidgets.Add(duplicatedWidget);
                    }
                }

                if (m_DuplicatedWidgets.Count > 0)
                {
                    SelectionManager.m_Instance.RegisterWidgetsInSelectionCanvas(m_DuplicatedWidgets);
                    SelectionManager.m_Instance.DeselectWidgets(m_DuplicatedWidgets, m_CurrentCanvas);
                }
            }

            GroupManager.MoveStrokesToNewGroups(
                m_DuplicatedStrokes.Concat(m_DuplicatedCopies).ToList(), null);
        }

        /// Duplicates the rest of a selected copy's linked group as a new linked group under the
        /// same mirror. The duplicate of the selected copy is selected with no join transform, so
        /// the preview moves its partners by the whole selection transform; they therefore start
        /// where the selected copy's selection-space points mirror to: each original partner
        /// carried by C·J⁻¹·C⁻¹ (J the selection transform when the original was selected).
        private void DuplicateLinkedGroup(Stroke selected, Stroke duplicate)
        {
            var group = selected.SymmetryPeerGroup;
            TrTransform joined = SelectionManager.m_Instance.SelectionTransformWhenSelected(selected);
            var newGroup = new SymmetryStrokeGroup(group.Mirror);
            duplicate.JoinSymmetryGroup(newGroup, selected.SymmetryPointerIndex);
            foreach (var copy in group.Strokes)
            {
                if (ReferenceEquals(copy, selected) || !copy.IsGeometryEnabled ||
                    SelectionManager.m_Instance.IsStrokeSelected(copy) ||
                    !SymmetryPeerEditing.TryGetPeerSymmetryTransform(selected, copy, out var toCopy))
                {
                    continue;
                }
                var copyDuplicate = SketchMemoryScript.m_Instance.DuplicateStroke(copy, copy.Canvas, null);
                MirrorStrokeEdits.ApplyLeftTransform(copyDuplicate, toCopy * joined.inverse * toCopy.inverse);
                copyDuplicate.JoinSymmetryGroup(newGroup, copy.SymmetryPointerIndex);
                m_DuplicatedCopies.Add(copyDuplicate);
            }
            newGroup.CaptureInstances();

            // The original stays where the selection put it, so its partners take the move.
            m_OriginalCopyMoves.Add(new TransformSymmetryCopiesCommand(
                selected, SymmetryPeerEditing.SelectionMovement(m_OriginTransform, joined)));
        }

        public override bool NeedsSave { get { return true; } }

        private void DuplicateWidgetGroup(GrabWidget source, GrabWidget duplicate)
        {
            var joined = SelectionManager.m_Instance.SelectionTransformWhenSelected(source);
            var group = new SymmetryWidgetGroup(source.SymmetryPeerGroup.Mirror);
            duplicate.SetSymmetryGroup(group, source.SymmetryPointerIndex);
            foreach (var peer in source.SymmetryPeerGroup.ActiveMembers)
            {
                if (peer == source || !SymmetryWidgetGroup.TryGetPeerTransform(source, peer, out var toPeer)) { continue; }
                var copy = peer.Clone();
                copy.SetCanvas(peer.Canvas);
                copy.LocalTransform = toPeer * joined.inverse * toPeer.inverse * copy.LocalTransform;
                copy.SetSymmetryGroup(group, peer.SymmetryPointerIndex);
                m_DuplicatedWidgetPeers.Add(copy);
            }
            m_OriginalWidgetMoves.Add(new TransformWidgetPeersCommand(source,
                SymmetryPeerEditing.SelectionMovement(m_OriginTransform, joined)));
        }

        protected override void OnRedo()
        {
            // Clone already accounts for the initial creation. Subsequent redo restores that cost.
            if (!m_FirstRedo)
            {
                foreach (var widget in m_DuplicatedWidgets.Concat(m_DuplicatedWidgetPeers))
                {
                    if (widget.SymmetryPeerGroup != null)
                    { TiltMeterScript.m_Instance.AdjustMeterWithWidget(widget.GetTiltMeterCost(), up: true); }
                }
            }
            m_FirstRedo = false;
            foreach (var widget in m_DuplicatedWidgetPeers) { widget.RestoreFromToss(); }
            foreach (var move in m_OriginalWidgetMoves) { move.Redo(); }
            // Place duplicated strokes.
            foreach (var stroke in m_DuplicatedStrokes.Concat(m_DuplicatedCopies))
            {
                switch (stroke.m_Type)
                {
                    case Stroke.Type.BrushStroke:
                        {
                            BaseBrushScript brushScript = stroke.m_Object.GetComponent<BaseBrushScript>();
                            if (brushScript)
                            {
                                brushScript.HideBrush(false);
                            }
                        }
                        break;
                    case Stroke.Type.BatchedBrushStroke:
                        {
                            stroke.m_BatchSubset.m_ParentBatch.EnableSubset(stroke.m_BatchSubset);
                        }
                        break;
                    default:
                        Debug.LogError("Unexpected: redo NotCreated duplicate stroke");
                        break;
                }
                TiltMeterScript.m_Instance.AdjustMeter(stroke, up: true);
            }

            // Place duplicated widgets.
            for (int i = 0; i < m_DuplicatedWidgets.Count; ++i)
            {
                m_DuplicatedWidgets[i].RestoreFromToss();
            }

            if (m_NoSymmetrySpecialCase)
            {
                if (m_SelectedStrokes != null)
                {
                    SelectionManager.m_Instance.DeselectStrokes(m_SelectedStrokes, m_CurrentCanvas);
                }
                foreach (var copyMove in m_OriginalCopyMoves) { copyMove.Redo(); }

                if (m_SelectedWidgets != null)
                {
                    SelectionManager.m_Instance.DeselectWidgets(m_SelectedWidgets, m_CurrentCanvas);
                }

                SelectionManager.m_Instance.RegisterStrokesInSelectionCanvas(m_DuplicatedStrokes);
                SelectionManager.m_Instance.RegisterWidgetsInSelectionCanvas(m_DuplicatedWidgets);

                // Set selection widget transforms.
                SelectionManager.m_Instance.SelectionTransform = m_DuplicateTransform;
                SelectionManager.m_Instance.UpdateSelectionWidget();
            }
        }

        protected override void OnUndo()
        {
            foreach (var widget in m_DuplicatedWidgets.Concat(m_DuplicatedWidgetPeers))
            {
                if (widget.SymmetryPeerGroup != null)
                { TiltMeterScript.m_Instance.AdjustMeterWithWidget(widget.GetTiltMeterCost(), up: false); }
            }
            foreach (var widget in m_DuplicatedWidgetPeers) { widget.Hide(); }
            for (int i = m_OriginalWidgetMoves.Count - 1; i >= 0; --i) { m_OriginalWidgetMoves[i].Undo(); }
            for (int i = m_OriginalCopyMoves.Count - 1; i >= 0; --i) { m_OriginalCopyMoves[i].Undo(); }

            // Remove duplicated strokes.
            foreach (var stroke in m_DuplicatedStrokes.Concat(m_DuplicatedCopies))
            {
                switch (stroke.m_Type)
                {
                    case Stroke.Type.BrushStroke:
                        {
                            BaseBrushScript brushScript = stroke.m_Object.GetComponent<BaseBrushScript>();
                            if (brushScript)
                            {
                                brushScript.HideBrush(true);
                            }
                        }
                        break;
                    case Stroke.Type.BatchedBrushStroke:
                        {
                            stroke.m_BatchSubset.m_ParentBatch.DisableSubset(stroke.m_BatchSubset);
                        }
                        break;
                    default:
                        Debug.LogError("Unexpected: undo NotCreated duplicate stroke");
                        break;
                }
                TiltMeterScript.m_Instance.AdjustMeter(stroke, up: false);
            }

            // Remove duplicated widgets.
            for (int i = 0; i < m_DuplicatedWidgets.Count; ++i)
            {
                m_DuplicatedWidgets[i].Hide();
            }

            if (m_NoSymmetrySpecialCase)
            {
                SelectionManager.m_Instance.DeregisterStrokesInSelectionCanvas(m_DuplicatedStrokes);
                SelectionManager.m_Instance.DeregisterWidgetsInSelectionCanvas(m_DuplicatedWidgets);

                // Reset the selection transform before we select strokes.
                SelectionManager.m_Instance.SelectionTransform = m_OriginTransform;

                // Select strokes.
                if (m_SelectedStrokes != null)
                {
                    SelectionManager.m_Instance.SelectStrokes(m_SelectedStrokes);
                }
                if (m_SelectedWidgets != null)
                {
                    SelectionManager.m_Instance.SelectWidgets(m_SelectedWidgets);
                }

                SelectionManager.m_Instance.UpdateSelectionWidget();
            }
        }

        public override bool Merge(BaseCommand other)
        {
            if (!m_DupeInPlace)
            {
                return false;
            }

            // If we duplicated a selection in place (the stamp feature), subsequent movements of
            // the selection should get bundled up with this command as a child.
            MoveWidgetCommand move = other as MoveWidgetCommand;
            if (move != null)
            {
                if (m_Children.Count == 0)
                {
                    m_Children.Add(other);
                }
                else
                {
                    MoveWidgetCommand childMove = m_Children[0] as MoveWidgetCommand;
                    Debug.Assert(childMove != null);
                    return childMove.Merge(other);
                }
                return true;
            }
            return false;
        }
    }
} // namespace TiltBrush
