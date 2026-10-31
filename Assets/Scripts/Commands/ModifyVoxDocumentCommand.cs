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
using UnityEngine;

namespace TiltBrush
{
    /// Records a voxel edit using changed cells, or snapshots for document-wide operations.
    public sealed class ModifyVoxDocumentCommand : BaseCommand
    {
        private readonly ModelWidget m_Widget;
        private readonly RuntimeVoxDocument m_Document;
        private readonly RuntimeVoxDocument.Snapshot m_Before;
        private readonly RuntimeVoxDocument.Snapshot m_After;
        private readonly RuntimeVoxDocument.RuntimeModel m_Model;
        private readonly Dictionary<Vector3Int, byte> m_BeforeCells;
        private readonly Dictionary<Vector3Int, byte> m_AfterCells;
        private readonly Color32[] m_BeforePalette;
        private readonly Color32[] m_AfterPalette;

        public ModifyVoxDocumentCommand(
            ModelWidget widget,
            RuntimeVoxDocument.Snapshot before,
            RuntimeVoxDocument.Snapshot after,
            BaseCommand parent = null)
            : base(parent)
        {
            m_Widget = widget;
            m_Document = widget.EditableVoxDocument;
            m_Before = before;
            m_After = after;
        }

        public ModifyVoxDocumentCommand(
            ModelWidget widget, RuntimeVoxDocument document, RuntimeVoxDocument.RuntimeModel model,
            Dictionary<Vector3Int, byte> before, Dictionary<Vector3Int, byte> after,
            Color32[] beforePalette, Color32[] afterPalette, BaseCommand parent = null)
            : base(parent)
        {
            m_Widget = widget;
            m_Document = document;
            m_Model = model;
            m_BeforeCells = new Dictionary<Vector3Int, byte>(before);
            m_AfterCells = new Dictionary<Vector3Int, byte>(after);
            m_BeforePalette = (Color32[])beforePalette.Clone();
            m_AfterPalette = (Color32[])afterPalette.Clone();
        }

        public override bool NeedsSave => true;
        public override bool IsAvailable => m_Widget != null;

        protected override void OnUndo()
        {
            if (m_BeforeCells != null) ApplyCells(m_BeforeCells, m_BeforePalette);
            else Apply(m_Before);
        }

        protected override void OnRedo()
        {
            if (m_AfterCells != null) ApplyCells(m_AfterCells, m_AfterPalette);
            else Apply(m_After);
        }

        private void ApplyCells(Dictionary<Vector3Int, byte> cells, Color32[] palette)
        {
            if (m_Widget == null || !ReferenceEquals(m_Widget.EditableVoxDocument, m_Document) ||
                !m_Document.ContainsModel(m_Model)) return;
            var document = m_Document;
            var model = m_Model;
            Array.Copy(palette, document.Palette, palette.Length);
            foreach (var cell in cells)
            {
                if (cell.Value == 0) model.RemoveVoxel(cell.Key);
                else model.AddOrUpdateVoxel(cell.Key, cell.Value);
            }
            if (m_Widget.Showing && m_Widget.gameObject.activeInHierarchy)
            {
                m_Widget.RefreshEditableVoxMeshes();
                m_Widget.EditableVoxDocument.ViewState.VisualsDirty = false;
            }
            SaveLoadScript.m_Instance?.SketchChanged();
        }

        private void Apply(RuntimeVoxDocument.Snapshot snapshot)
        {
            if (m_Widget == null || !ReferenceEquals(m_Widget.EditableVoxDocument, m_Document))
            {
                return;
            }

            m_Document.RestoreSnapshot(snapshot);
            if (m_Widget.Showing && m_Widget.gameObject.activeInHierarchy)
            {
                m_Widget.RefreshEditableVoxMeshes();
                m_Widget.EditableVoxDocument.ViewState.VisualsDirty = false;
            }
            SaveLoadScript.m_Instance?.SketchChanged();
        }
    }
}
