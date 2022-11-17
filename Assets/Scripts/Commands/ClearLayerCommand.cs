using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public class ClearLayerCommand : BaseCommand
    {
        private CanvasScript m_Layer;
        private List<BatchPool> batchPool;
        private GrabWidget[] m_Widgets;

        public ClearLayerCommand(int layerIndex, BaseCommand parent = null) : base(parent)
        {
            m_Layer = App.Scene.GetCanvasByLayerIndex(layerIndex);
            m_Widgets = m_Layer.GetComponentsInChildren<GrabWidget>();
        }

        public ClearLayerCommand(CanvasScript canvas)
        {
            m_Layer = canvas;
            m_Widgets = m_Layer.GetComponentsInChildren<GrabWidget>();
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnRedo()
        {
            var batchManager = m_Layer.BatchManager;
            AudioManager.m_Instance.PlayRedoSound(m_Layer.transform.position);
            foreach (var batch in batchManager.AllBatches())
            {
                foreach (var subset in batch.m_Groups)
                {
                    batch.DisableSubset(subset);
                }
            }

            foreach (var widget in m_Widgets)
            {
                widget.Hide();
            }
        }

        protected override void OnUndo()
        {
            var batchManager = m_Layer.BatchManager;
            AudioManager.m_Instance.PlayUndoSound(m_Layer.transform.position);
            foreach (var batch in batchManager.AllBatches())
            {
                foreach (var subset in batch.m_Groups)
                {
                    batch.EnableSubset(subset);
                }
            }

            foreach (var widget in m_Widgets)
            {
                widget.RestoreFromToss();
                widget.gameObject.SetActive(true);
            }
        }
    }
}
