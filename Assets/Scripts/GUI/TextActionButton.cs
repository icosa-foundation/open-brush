using TMPro;
using UnityEngine;
namespace TiltBrush
{
    public class TextActionButton : ActionButton
    {
        public GameObject m_Highlight;
        public string m_ButtonLabel;
        public Color m_ColorSelected;
        public Color m_ColorDeselected;


        protected override void Awake()
        {
            base.Awake();
            SetTextLabel();
            SetButtonSelected(false);
        }

        [ContextMenu("Set Text Label")]
        private void SetTextLabel()
        {
            GetComponentInChildren<TextMeshPro>().text = m_ButtonLabel;
        }

        public override void SetButtonSelected(bool bSelected)
        {
            base.SetButtonSelected(bSelected);
            m_Highlight.SetActive(bSelected);
            var color = bSelected ? m_ColorSelected : m_ColorDeselected;
            m_Highlight.GetComponent<MeshRenderer>().material.color = color;
        }
    }
}
