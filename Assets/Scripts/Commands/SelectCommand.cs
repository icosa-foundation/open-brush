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

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{

    /// A command to select or deselect a set of strokes and widgets.
    ///
    /// The command holds a collection of objects and acts as either a selection command
    /// or deselection command for these objects.
    ///
    /// The act of selecting means to move the objects from the active canvas to the
    /// selection canvas, and deselecting moves the objects from the selection canvas
    /// back to the original canvas.
    ///
    /// It also remembers the selection's transform from before the selection/deselection
    /// took place. This is used in the case of undoing a deselection of all selected
    /// objects to preserve the selection's grab widget orientation.
    public class SelectCommand : BaseCommand
    {
        private List<Stroke> m_Strokes;
        private List<GrabWidget> m_Widgets;
        private TrTransform m_InitialTransform;
        private bool m_Deselect;
        private bool m_Initial;
        private bool m_Final;
        private bool m_CheckForClearedSelection;
        private bool m_IsGrabbingGroup;
        private bool m_IsEndGrabbingGroup;
        private CanvasScript m_TargetCanvas; // Override original canvas as target for deselection.
        // Strokes the symmetry drew alongside the ones being deselected, each with the transform
        // that mirrors the selection's move onto it. Empty unless peer editing is on and this
        // deselect is about to bake a move into the strokes.
        private readonly List<Stroke> m_PeerStrokes = new List<Stroke>();
        private readonly List<TrTransform> m_PeerTransforms = new List<TrTransform>();
        private readonly Dictionary<Stroke, TrTransform> m_JoinTransforms =
            new Dictionary<Stroke, TrTransform>(new ReferenceComparer<Stroke>());
        private readonly Dictionary<Stroke, CanvasScript> m_SourceCanvases =
            new Dictionary<Stroke, CanvasScript>(new ReferenceComparer<Stroke>());
        private bool m_MovedStrokeSinceJoin;

        override public bool NeedsSave
        {
            get
            {
                // We only need to save if objects have been moved, and that only
                // occurs when a transformed selection has been deselecting, which
                // rebakes that object into the original canvas.
                return m_Deselect &&
                    (m_InitialTransform != TrTransform.identity || m_MovedStrokeSinceJoin);
            }
        }

        public void ResetInitialTransform()
        {
            m_InitialTransform = TrTransform.identity;
        }

        /// The command takes additional ownership over the objects passed, copying its
        /// objects into a new array that is never mutated.
        ///
        /// Initial pose is the pose of the selection from before the selection/deselection
        /// takes place. It's needed because if the action is to deselect the very last objects,
        /// the SelectionManager will automatically reset the selection transform to identity. And
        /// then if we want to undo that selection, we need to restore its initial transform so that
        /// the selection widget is appropriately rotated.
        ///
        /// Preserving the orientation of the selection widget is important for two reasons:
        /// 1: Aesthetics. If a user deselects a rotated selection then immediately undoes that
        ///    deselection, it would be jarring if the selection bounds rotated.
        /// 2: To preserve undoing transformations of the selection widget. When the user grabs and moves
        ///    the selection, the selection widget maintains its own movewidget commands on the stack, so
        ///    it'd expect the widget not to have been moved by an outside party between redoing a move
        ///    and then undoing it.
        public SelectCommand(
            ICollection<Stroke> strokes,
            ICollection<GrabWidget> widgets,
            TrTransform initialTransform,
            bool deselect = false, bool initial = false, bool checkForClearedSelection = false,
            bool isGrabbingGroup = false, bool isEndGrabbingGroup = false,
            CanvasScript targetCanvas = null,
            BaseCommand parent = null)
            : base(parent)
        {
            var selectedGroups = new Dictionary<SketchGroupTag, HashSet<CanvasScript>>();

            var strokesNotGrouped = new HashSet<Stroke>();
            if (strokes != null)
            {
                // Get strokes that are not grouped and groups among selected strokes.
                foreach (var stroke in strokes)
                {
                    if (stroke.Group == SketchGroupTag.None)
                    {
                        strokesNotGrouped.Add(stroke);
                    }
                    else
                    {
                        AddSelectedGroup(selectedGroups, stroke.Group, GetSelectionScopeCanvas(stroke, deselect));
                    }
                }
            }

            var widgetsNotGrouped = new HashSet<GrabWidget>();
            if (widgets != null)
            {
                // Get widgets that are not grouped and groups among selected widgets.
                foreach (var widget in widgets)
                {
                    if (widget.Group == SketchGroupTag.None)
                    {
                        widgetsNotGrouped.Add(widget);
                    }
                    else
                    {
                        AddSelectedGroup(selectedGroups, widget.Group, GetSelectionScopeCanvas(widget, deselect));
                    }
                }
            }

            // Get the grouped strokes.
            var strokesGrouped = new HashSet<Stroke>();
            foreach (var groupCanvases in selectedGroups)
            {
                foreach (var canvas in groupCanvases.Value)
                {
                    strokesGrouped.UnionWith(
                        GetStrokesInGroup(groupCanvases.Key, canvas, deselect));
                }
            }

            // Get the grouped widgets.
            var widgetsGrouped = new HashSet<GrabWidget>();
            foreach (var groupCanvases in selectedGroups)
            {
                foreach (var canvas in groupCanvases.Value)
                {
                    widgetsGrouped.UnionWith(
                        GetWidgetsInGroup(groupCanvases.Key, canvas, deselect));
                }
            }

            m_Strokes = new List<Stroke>();
            m_Strokes.AddRange(strokesGrouped);
            m_Strokes.AddRange(strokesNotGrouped);

            m_Widgets = new List<GrabWidget>();
            m_Widgets.AddRange(widgetsGrouped);
            m_Widgets.AddRange(widgetsNotGrouped);

            m_InitialTransform = initialTransform;
            m_Deselect = deselect;
            m_Initial = initial;
            m_CheckForClearedSelection = checkForClearedSelection;
            m_IsGrabbingGroup = isGrabbingGroup;
            m_IsEndGrabbingGroup = isEndGrabbingGroup;
            m_TargetCanvas = targetCanvas;

            GatherSymmetryPeers();
        }

        /// Deselecting is the point at which a moved selection is baked back into its strokes, so
        /// it is also the point at which the symmetry peers of those strokes move to match. The
        /// peers are worked out now, while the strokes are still where the move left them.
        private void GatherSymmetryPeers()
        {
            // Construction captures the movement but does not change sketch geometry. The
            // selection interaction restores preview when the command is executed.
            if (!m_Deselect || m_Strokes == null)
            {
                return;
            }

            var handled = new HashSet<Stroke>(m_Strokes, new ReferenceComparer<Stroke>());
            foreach (var stroke in m_Strokes)
            {
                // A stroke added to a selection that had already been moved has only moved by
                // what the selection did after it joined.
                TrTransform joined =
                    SelectionManager.m_Instance.SelectionTransformWhenSelected(stroke);
                m_JoinTransforms[stroke] = joined;
                m_SourceCanvases[stroke] = stroke.m_PreviousCanvas;
                TrTransform moved = SymmetryPeerEditing.SelectionMovement(m_InitialTransform, joined);
                if (moved == TrTransform.identity) { continue; }
                m_MovedStrokeSinceJoin = true;

                // A stroke moved into a different canvas no longer has a shared canvas-space
                // relationship with its peers. Undo can make that relationship active again.
                if (m_TargetCanvas != null && m_TargetCanvas != stroke.m_PreviousCanvas)
                {
                    continue;
                }

                foreach (var peer in SymmetryPeerEditing.PeersOf(stroke))
                {
                    if (!handled.Add(peer)) { continue; }
                    // A peer that is still selected carries the selection's move itself, and will
                    // bake it in when it is deselected in turn.
                    if (SelectionManager.m_Instance.IsStrokeSelected(peer)) { continue; }
                    if (SymmetryPeerEditing.TryGetPeerSymmetryTransform(
                            stroke, peer, out TrTransform toPeer))
                    {
                        TrTransform peerXf = SymmetryPeerEditing.PeerSelectionMovement(
                            toPeer, m_InitialTransform, joined);
                        if (!peerXf.IsFinite()) { continue; }
                        m_PeerStrokes.Add(peer);
                        m_PeerTransforms.Add(peerXf);
                    }
                }
            }
        }

        private static void AddSelectedGroup(
            Dictionary<SketchGroupTag, HashSet<CanvasScript>> selectedGroups,
            SketchGroupTag group, CanvasScript canvas)
        {
            if (!selectedGroups.TryGetValue(group, out var canvases))
            {
                canvases = selectedGroups[group] = new HashSet<CanvasScript>();
            }
            canvases.Add(canvas);
        }

        private static CanvasScript GetSelectionScopeCanvas(Stroke stroke, bool deselect)
        {
            return deselect ? stroke.m_PreviousCanvas ?? stroke.Canvas : stroke.Canvas;
        }

        private static CanvasScript GetSelectionScopeCanvas(GrabWidget widget, bool deselect)
        {
            return deselect ? widget.m_PreviousCanvas ?? widget.Canvas : widget.Canvas;
        }

        private static IEnumerable<Stroke> GetStrokesInGroup(
            SketchGroupTag group, CanvasScript canvas, bool deselect)
        {
            return deselect
                ? SelectionManager.m_Instance.SelectedStrokesInGroup(group, canvas)
                : SelectionManager.m_Instance.StrokesInGroup(group, canvas);
        }

        private static IEnumerable<GrabWidget> GetWidgetsInGroup(
            SketchGroupTag group, CanvasScript canvas, bool deselect)
        {
            return deselect
                ? SelectionManager.m_Instance.SelectedWidgetsInGroup(group, canvas)
                : SelectionManager.m_Instance.WidgetsInGroup(group, canvas);
        }

        protected override void OnRedo()
        {
            SymmetryPeerPreview.Hide();
            if (m_Deselect)
            {
                // Peers must be in their own layers before the move is written into them.
                if (m_Strokes != null)
                {
                    SelectionManager.m_Instance.DeselectStrokes(m_Strokes, m_TargetCanvas);
                }
                if (m_Widgets != null)
                {
                    SelectionManager.m_Instance.DeselectWidgets(m_Widgets, m_TargetCanvas);
                }
                TransformItems.TransformEach(m_PeerStrokes, m_PeerTransforms);
            }
            else
            {
                if (m_Strokes != null)
                {
                    SelectionManager.m_Instance.SelectStrokes(m_Strokes);
                }
                if (m_Widgets != null)
                {
                    SelectionManager.m_Instance.SelectWidgets(m_Widgets);
                }
            }

            SelectionManager.m_Instance.UpdateSelectionWidget();

            if (m_CheckForClearedSelection)
            {
                if (!SelectionManager.m_Instance.HasSelection)
                {
                    SelectionManager.m_Instance.RemoveFromSelection(false);
                }
            }

            App.Switchboard.TriggerSelectionChanged();
        }

        protected override void OnUndo()
        {
            SymmetryPeerPreview.Hide();
            // In the future, we should check for a cleared selection that happen on a redo of this
            // command.
            m_CheckForClearedSelection = true;

            SelectionManager.m_Instance.SelectionTransform = m_InitialTransform;
            if (m_Deselect)
            {
                TransformItems.TransformEach(
                    m_PeerStrokes, m_PeerTransforms.Select(xf => xf.inverse).ToList());
                if (m_Strokes != null)
                {
                    SelectionManager.m_Instance.SelectStrokes(m_Strokes);
                    SelectionManager.m_Instance.RestoreSelectionSourceCanvases(m_SourceCanvases);
                    SelectionManager.m_Instance.RestoreSelectionJoinTransforms(m_JoinTransforms);
                }
                if (m_Widgets != null)
                {
                    SelectionManager.m_Instance.SelectWidgets(m_Widgets);
                }
            }
            else
            {
                if (m_Strokes != null)
                {
                    SelectionManager.m_Instance.DeselectStrokes(m_Strokes);
                }
                if (m_Widgets != null)
                {
                    SelectionManager.m_Instance.DeselectWidgets(m_Widgets, m_TargetCanvas);
                }
            }

            SelectionManager.m_Instance.UpdateSelectionWidget();

            if (m_CheckForClearedSelection)
            {
                if (!SelectionManager.m_Instance.HasSelection)
                {
                    SelectionManager.m_Instance.RemoveFromSelection(false);
                }
            }

            App.Switchboard.TriggerSelectionChanged();
        }

        public override bool Merge(BaseCommand other)
        {
            var newSelectCommand = other as SelectCommand;
            if (m_Final) { return false; }
            if (m_IsGrabbingGroup)
            {
                if (other is MoveWidgetCommand || other is DeleteSelectionCommand)
                {
                    // Merge with moves and deletes while grabbing a group.
                    m_Children.Add(other);
                    return true;
                }
                else if (newSelectCommand != null && newSelectCommand.m_IsGrabbingGroup)
                {
                    // Merge with other selections while grabbing a group.
                    if (newSelectCommand.m_IsEndGrabbingGroup)
                    {
                        // We've hit the end of the grabbing group gestures, so finalize it.
                        m_Final = true;
                    }
                    m_Children.Add(other);
                    return true;
                }
            }
            if (m_Deselect)
            {
                if (other is GroupStrokesAndWidgetsCommand)
                {
                    m_Children.Add(other);
                    m_Final = true;
                    return true;
                }
                else if (other is DeleteStrokeCommand)
                {
                    m_Children.Add(other);
                    return true;
                }
            }
            if (newSelectCommand != null)
            {
                if (newSelectCommand.m_Deselect == m_Deselect && !newSelectCommand.m_Initial)
                {
                    m_Children.Add(other);
                    return true;
                }
            }
            return false;
        }
    }
} // namespace TiltBrush
