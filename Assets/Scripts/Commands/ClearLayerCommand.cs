using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public class ClearLayerCommand : BaseCommand
    {
        private BatchManager m_BatchManager;
        private List<BatchPool> batchPool;

        public ClearLayerCommand(BatchManager batchManager)
        {
            m_BatchManager = batchManager;
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnRedo()
        {
            AudioManager.m_Instance.PlayRedoSound(m_BatchManager.Canvas.transform.position);
            foreach (var batch in m_BatchManager.AllBatches())
            {
                foreach (var subset in batch.m_Groups)
                {
                    batch.DisableSubset(subset);
                }
            }
        }

        protected override void OnUndo()
        {
            AudioManager.m_Instance.PlayUndoSound(m_BatchManager.Canvas.transform.position);
            foreach (var batch in m_BatchManager.AllBatches())
            {
                foreach (var subset in batch.m_Groups)
                {
                    batch.EnableSubset(subset);
                }
            }
        }

    }
}
