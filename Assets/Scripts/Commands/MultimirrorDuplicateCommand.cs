// Copyright 2022 The Tilt Brush Authors
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
    public class MultimirrorDuplicateCommand : BaseCommand
    {
        private List<Stroke> m_SelectedStrokes;
        private List<GrabWidget> m_SelectedWidgets;

        private List<Stroke> m_DuplicatedStrokes;
        private List<GrabWidget> m_DuplicatedWidgets;

        private TrTransform m_Transform;
        private CanvasScript m_CurrentCanvas;
        private bool m_StampMode;

        public MultimirrorDuplicateCommand(TrTransform xf, bool stampMode, BaseCommand parent = null) : base(parent)
        {
            CanvasScript targetCanvas;
            m_CurrentCanvas = App.ActiveCanvas;
            m_StampMode = stampMode;
            m_Transform = xf;

            m_SelectedStrokes = SelectionManager.m_Instance.SelectedStrokes.ToList();
            m_DuplicatedStrokes = new List<Stroke>();
            List<Matrix4x4> matrices;

            matrices = PointerManager.m_Instance.CustomMirrorMatrices.ToList();

            if (m_StampMode)
            {
                targetCanvas = m_CurrentCanvas;
            }
            else
            {
                targetCanvas = App.Scene.SelectionCanvas;
            }

            foreach (var stroke in m_SelectedStrokes)
            {
                TrTransform strokeTransform_GS = Coords.AsGlobal[stroke.StrokeTransform];
                TrTransform tr_GS;
                var xfCenter_GS = TrTransform.FromTransform(PointerManager.m_Instance.SymmetryWidget);
                for (int i = 0; i < matrices.Count; i++)
                {
                    (TrTransform, TrTransform) trAndFix_WS;
                    trAndFix_WS = PointerManager.m_Instance.TrFromMatrixWithFixedReflections(matrices[i]);
                    tr_GS = xfCenter_GS * trAndFix_WS.Item1 * xfCenter_GS.inverse;                   // convert from widget-local coords to world coords
                    var tmp = tr_GS * strokeTransform_GS * trAndFix_WS.Item2; // Work around 2018.3.x Mono parse bug

                    // TODO strokes don't work correctly with reflections and I can't figure out why
                    // Same logic is working for widgets and pointers (whilst drawing)...
                    // So skip reflected strokes for now
                    if (trAndFix_WS.Item2 != TrTransform.identity) continue;

                    tmp = targetCanvas.Pose.inverse * tmp;
                    var duplicatedStroke = SketchMemoryScript.m_Instance.DuplicateStroke(stroke, targetCanvas, tmp);
                    m_DuplicatedStrokes.Add(duplicatedStroke);
                }
            }

            m_SelectedWidgets = SelectionManager.m_Instance.SelectedWidgets.ToList();
            m_DuplicatedWidgets = new List<GrabWidget>();
            foreach (var widget in m_SelectedWidgets)
            {
                TrTransform widgetTransform_GS = TrTransform.FromTransform(widget.transform);
                TrTransform tr_GS;
                var xfCenter_GS = TrTransform.FromTransform(PointerManager.m_Instance.SymmetryWidget);

                // Generally speaking we want both sides of 2d media to appear
                // when duplicating using multimirror
                bool duplicateAsTwoSided = widget is Media2dWidget;

                for (int i = 0; i < matrices.Count; i++)
                {
                    var duplicatedWidget = widget.Clone();
                    if (duplicateAsTwoSided) ((Media2dWidget)duplicatedWidget).TwoSided = true;

                    (TrTransform, TrTransform) trAndFix_WS;
                    trAndFix_WS = PointerManager.m_Instance.TrFromMatrixWithFixedReflections(matrices[i]);
                    tr_GS = xfCenter_GS * trAndFix_WS.Item1 * xfCenter_GS.inverse; // convert from widget-local coords to world coords
                    var tmp = tr_GS * widgetTransform_GS * trAndFix_WS.Item2;   // Work around 2018.3.x Mono parse bug
                    tmp.ToTransform(duplicatedWidget.transform);
                    duplicatedWidget.SetCanvas(m_CurrentCanvas);
                    m_DuplicatedWidgets.Add(duplicatedWidget);
                }
            }
            GroupManager.MoveStrokesToNewGroups(m_DuplicatedStrokes, null);
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnRedo()
        {
            if (m_SelectedStrokes != null)
            {
                if (m_StampMode)
                {
                    SelectionManager.m_Instance.DeselectStrokes(m_DuplicatedStrokes);
                }
                else
                {
                    SelectionManager.m_Instance.DeselectStrokes(m_SelectedStrokes);
                }
            }

            // Place duplicated strokes.
            foreach (var stroke in m_DuplicatedStrokes)
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

            if (m_StampMode)
            {
                SelectionManager.m_Instance.RegisterStrokesInSelectionCanvas(m_SelectedStrokes);
            }
            else
            {
                SelectionManager.m_Instance.RegisterStrokesInSelectionCanvas(m_DuplicatedStrokes);
            }

            // Place duplicated widgets.
            for (int i = 0; i < m_DuplicatedWidgets.Count; ++i)
            {
                m_DuplicatedWidgets[i].RestoreFromToss();
            }

            // Select widgets.
            if (m_DuplicatedWidgets != null)
            {
                if (m_StampMode)
                {
                    SelectionManager.m_Instance.SelectWidgets(m_DuplicatedWidgets);
                    SelectionManager.m_Instance.RegisterWidgetsInSelectionCanvas(m_DuplicatedWidgets);
                    SelectionManager.m_Instance.DeselectWidgets(m_DuplicatedWidgets);
                }
                else
                {
                    SelectionManager.m_Instance.SelectWidgets(m_DuplicatedWidgets);
                    SelectionManager.m_Instance.RegisterWidgetsInSelectionCanvas(m_DuplicatedWidgets);
                }
            }

            // Set selection widget transforms.
            SelectionManager.m_Instance.SelectionTransform = m_Transform;
            SelectionManager.m_Instance.UpdateSelectionWidget();
        }

        protected override void OnUndo()
        {
            // Remove duplicated strokes.
            foreach (var stroke in m_DuplicatedStrokes)
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

            // Deselect selected widgets.
            if (m_DuplicatedWidgets != null && !m_StampMode)
            {
                SelectionManager.m_Instance.DeselectWidgets(m_DuplicatedWidgets);
                SelectionManager.m_Instance.DeregisterWidgetsInSelectionCanvas(m_DuplicatedWidgets);
            }

            // Remove duplicated widgets.
            for (int i = 0; i < m_DuplicatedWidgets.Count; ++i)
            {
                m_DuplicatedWidgets[i].Hide();
            }

            // Reset the selection transform before we select strokes.
            SelectionManager.m_Instance.SelectionTransform = m_Transform;

            if (m_SelectedStrokes != null)
            {
                if (m_StampMode)
                {
                    // I don't think we need to do anything here
                    // My original stab at this below was buggy and created lots of duplicates
                    // SelectionManager.m_Instance.DeregisterStrokesInSelectionCanvas(m_SelectedStrokes);
                    // SelectionManager.m_Instance.SelectStrokes(m_DuplicatedStrokes);
                }
                else
                {
                    SelectionManager.m_Instance.DeregisterStrokesInSelectionCanvas(m_DuplicatedStrokes);
                    SelectionManager.m_Instance.SelectStrokes(m_SelectedStrokes);
                }
            }

            SelectionManager.m_Instance.UpdateSelectionWidget();
        }

        public override bool Merge(BaseCommand other)
        {
            if (!m_StampMode)
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
