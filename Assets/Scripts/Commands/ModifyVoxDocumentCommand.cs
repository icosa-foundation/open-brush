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

namespace TiltBrush
{
    /// Records one voxel-edit gesture as before/after VOX document snapshots.
    public sealed class ModifyVoxDocumentCommand : BaseCommand
    {
        private readonly ModelWidget m_Widget;
        private readonly byte[] m_Before;
        private readonly byte[] m_After;
        private readonly bool m_PreserveSourceData;

        public ModifyVoxDocumentCommand(
            ModelWidget widget,
            byte[] before,
            byte[] after,
            BaseCommand parent = null)
            : base(parent)
        {
            m_Widget = widget;
            m_Before = (byte[])before.Clone();
            m_After = (byte[])after.Clone();
            m_PreserveSourceData = widget.EditableVoxDocument.HasPreservedSourceData;
        }

        public override bool NeedsSave => true;
        public override bool IsAvailable => m_Widget != null;

        protected override void OnUndo()
        {
            Apply(m_Before);
        }

        protected override void OnRedo()
        {
            Apply(m_After);
        }

        private void Apply(byte[] bytes)
        {
            if (m_Widget == null)
            {
                return;
            }

            m_Widget.AdoptEditableVoxDocument(RuntimeVoxDocument.FromBytes(
                bytes,
                m_PreserveSourceData));
            if (m_Widget.Showing && m_Widget.gameObject.activeInHierarchy)
            {
                m_Widget.RefreshEditableVoxMeshes();
            }
            SaveLoadScript.m_Instance?.SketchChanged();
        }
    }
}
