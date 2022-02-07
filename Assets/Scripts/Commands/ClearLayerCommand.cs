using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public class ClearLayerCommand : BaseCommand
    {
        private BatchManager batchManager;
        private List<BatchPool> batchPool;

        public ClearLayerCommand(BatchManager batch)
        {
            batchManager = batch;
            ResetPools(batch);
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnRedo()
        {
            //AudioManager.m_Instance.PlayRedoSound();


        }

        protected override void OnUndo()
        {
            //AudioManager.m_Instance.PlayUndoSound();
            //batchPool.
        }

        public void ResetPools(BatchManager batch)
        {
            //batchPool = batchManager.AllBatches();
            batchManager.ResetPools();
        }
    }
}
