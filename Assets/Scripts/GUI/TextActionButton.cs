using TMPro;
using UnityEngine;
namespace TiltBrush
{
    public class TextActionButton : ActionButton
    {
        public string m_ButtonLabel;

        protected override void Awake()
        {
            base.Awake();
            SetTextLabel();
        }

        [ContextMenu("Set Text Label")]
        private void SetTextLabel()
        {
            GetComponentInChildren<TextMeshPro>().text = m_ButtonLabel;
        }
    }
}
