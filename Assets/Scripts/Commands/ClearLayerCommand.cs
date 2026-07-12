using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public class ClearLayerCommand : BaseCommand
    {
        private CanvasScript m_Layer;
        private BatchSubset[] m_ActiveSubsets;
        private GrabWidget[] m_Widgets;

        public ClearLayerCommand(int layerIndex, BaseCommand parent = null) : base(parent)
        {
            m_Layer = App.Scene.GetCanvasByLayerIndex(layerIndex);
            Init();
        }

        public ClearLayerCommand(CanvasScript canvas)
        {
            m_Layer = canvas;
            Init();
        }

        private void Init()
        {
            m_ActiveSubsets = m_Layer.BatchManager.AllBatches()
                .SelectMany(batch => batch.m_Groups)
                .Where(subset => subset.m_Active)
                .ToArray();
            m_Widgets = m_Layer.GetComponentsInChildren<GrabWidget>();
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnRedo()
        {
            AudioManager.m_Instance.PlayRedoSound(m_Layer.transform.position);
            foreach (var subset in m_ActiveSubsets)
            {
                if (subset.m_ParentBatch != null)
                {
                    subset.m_ParentBatch.DisableSubset(subset);
                }
            }

            foreach (var widget in m_Widgets)
            {
                widget.Hide();
            }
        }

        protected override void OnUndo()
        {
            AudioManager.m_Instance.PlayUndoSound(m_Layer.transform.position);
            foreach (var subset in m_ActiveSubsets)
            {
                if (subset.m_ParentBatch != null)
                {
                    subset.m_ParentBatch.EnableSubset(subset);
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
