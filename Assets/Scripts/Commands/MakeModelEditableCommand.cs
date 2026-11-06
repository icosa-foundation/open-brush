using Polyhydra.Core;
using TiltBrush.MeshEditing;
using UnityEngine;

namespace TiltBrush
{
    // Owns the replacement widget; the original remains owned by its creation command.
    public class MakeModelEditableCommand : BaseCommand
    {
        private readonly ModelWidget m_Source;
        private readonly PolyMesh m_Poly;
        private readonly PolyRecipe m_Recipe;
        private readonly TrTransform m_SourceTransform;
        private readonly int m_SourceCost;
        private EditableModelWidget m_Editable;

        public MakeModelEditableCommand(ModelWidget source, PolyMesh poly, PolyRecipe recipe)
        {
            m_Source = source;
            m_Poly = poly;
            m_Recipe = recipe.Clone();
            m_SourceTransform = source.GetSaveTransform();
            m_SourceCost = source.GetTiltMeterCost();
        }

        public override bool NeedsSave => true;

        protected override void OnRedo()
        {
            if (m_Editable == null)
            {
                m_Editable = EditableModelManager.m_Instance.GeneratePolyMesh(
                    m_Poly, m_Recipe, m_SourceTransform, recordCommand: false);
                m_Editable.SetCanvas(m_Source.Canvas);
                m_Editable.LocalTransform = m_SourceTransform;
                m_Editable.SetSignedWidgetSize(m_SourceTransform.scale);
                m_Editable.Group = m_Source.Group;
                if (m_Source.Pinned) { m_Editable.PinFromSave(); }
                m_Editable.TrySetCanvasKeywordsFromObject(m_Source.transform);
            }
            else
            {
                Restore(m_Editable, m_SourceTransform);
                TiltMeterScript.m_Instance.AdjustMeterWithWidget(m_Editable.GetTiltMeterCost(), up: true);
            }
            m_Source.Hide();
            TiltMeterScript.m_Instance.AdjustMeterWithWidget(m_SourceCost, up: false);
            WidgetManager.m_Instance.RefreshPinAndUnpinLists();
        }

        protected override void OnUndo()
        {
            m_Editable.Hide();
            TiltMeterScript.m_Instance.AdjustMeterWithWidget(m_Editable.GetTiltMeterCost(), up: false);
            Restore(m_Source, m_SourceTransform);
            TiltMeterScript.m_Instance.AdjustMeterWithWidget(m_SourceCost, up: true);
            WidgetManager.m_Instance.RefreshPinAndUnpinLists();
        }

        private static void Restore(ModelWidget widget, TrTransform transform)
        {
            widget.gameObject.SetActive(true);
            widget.RestoreFromToss();
            widget.LocalTransform = transform;
            widget.SetSignedWidgetSize(transform.scale);
        }

        protected override void OnDispose()
        {
            if (m_Editable != null)
            {
                WidgetManager.m_Instance.UnregisterGrabWidget(m_Editable.gameObject);
                Object.Destroy(m_Editable.gameObject);
            }
        }
    }
}
