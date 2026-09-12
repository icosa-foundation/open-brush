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

                // Duplicate strokes.
                foreach (var stroke in m_SelectedStrokes)
                {
                    m_DuplicatedStrokes.Add(
                        SketchMemoryScript.m_Instance.DuplicateStroke(
                            stroke, App.Scene.SelectionCanvas, null));
                }

                // Duplicate widgets.
                foreach (var widget in m_SelectedWidgets)
                {
                    m_DuplicatedWidgets.Add(widget.Clone());
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

                // Duplicate strokes.
                foreach (var stroke in m_SelectedStrokes)
                {
                    for (int i = 0; i < xfSymmetriesCS.Count; i++)
                    {
                        m_DuplicatedStrokes.Add(
                            SketchMemoryScript.m_Instance.DuplicateStroke(
                                stroke, m_CurrentCanvas, xfSymmetriesCS[i], absoluteScale: true));
                    }
                }

                // Duplicate widgets.
                foreach (var widget in m_SelectedWidgets)
                {
                    // Generally speaking we want both sides of 2d media to appear
                    // when duplicating using multi-mirror.
                    bool duplicateAsTwoSided = widget is Media2dWidget;

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

                        m_DuplicatedWidgets.Add(duplicatedWidget);
                    }
                }

                if (m_DuplicatedWidgets.Count > 0)
                {
                    SelectionManager.m_Instance.RegisterWidgetsInSelectionCanvas(m_DuplicatedWidgets);
                    SelectionManager.m_Instance.DeselectWidgets(m_DuplicatedWidgets, m_CurrentCanvas);
                }
            }

            GroupManager.MoveStrokesToNewGroups(m_DuplicatedStrokes, null);
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnRedo()
        {
            // Place duplicated strokes.
            foreach (var stroke in m_DuplicatedStrokes)
            {
                stroke.Hide(false);
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
            // Remove duplicated strokes.
            foreach (var stroke in m_DuplicatedStrokes)
            {
                stroke.Hide(true);
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
