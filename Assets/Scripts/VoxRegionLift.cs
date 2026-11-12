// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    // One-time lift state. Shared placements retain their original definition;
    // the detached definition is reused on redo, including any bounds rebasing.
    internal sealed class VoxRegionLift
    {
        private readonly RuntimeVoxDocument.RuntimeModel m_Model;
        private readonly IReadOnlyDictionary<Vector3Int, byte> m_Cells;
        private readonly DefinitionState? m_Shared;
        private DefinitionState? m_Detached;

        private readonly struct DefinitionState
        {
            internal readonly RuntimeVoxDocument.VoxelDefinition Definition;
            private readonly Vector3 m_Offset;
            private readonly Vector3Int m_CellOffset;

            internal DefinitionState(RuntimeVoxDocument.RuntimeModel model)
            {
                Definition = model.Definition;
                m_Offset = model.TransformOffset;
                m_CellOffset = model.SourceCellOffset;
            }

            internal void Restore(RuntimeVoxDocument.RuntimeModel model)
            {
                model.Definition = Definition;
                model.TransformOffset = m_Offset;
                model.SourceCellOffset = m_CellOffset;
            }
        }

        internal VoxRegionLift(RuntimeVoxDocument document, VoxFloatingRegion region)
        {
            m_Model = region.Model;
            m_Cells = region.Payload;
            if (document.Models.Any(other => !ReferenceEquals(other, m_Model) &&
                    ReferenceEquals(other.Definition, m_Model.Definition)))
                m_Shared = new DefinitionState(m_Model);
        }

        internal void Redo()
        {
            if (m_Detached.HasValue)
            {
                m_Detached.Value.Restore(m_Model);
                return;
            }
            if (m_Shared.HasValue)
            {
                var source = m_Shared.Value.Definition;
                m_Model.Definition = new RuntimeVoxDocument.VoxelDefinition(source.Size, source.SourceModelId,
                    new Dictionary<Vector3Int, byte>(source.Cells));
            }
            SetLifted(true);
        }

        internal void Undo()
        {
            if (m_Shared.HasValue)
            {
                m_Detached = new DefinitionState(m_Model);
                m_Shared.Value.Restore(m_Model);
            }
            else SetLifted(false);
        }

        private void SetLifted(bool lifted)
        {
            foreach (var cell in m_Cells)
                m_Model.Definition.SetCell(cell.Key + m_Model.SourceCellOffset, lifted ? (byte)0 : cell.Value);
        }
    }
}
