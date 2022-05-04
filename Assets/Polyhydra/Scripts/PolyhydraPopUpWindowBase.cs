using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public abstract class PolyhydraPopUpWindowBase : PopUpWindow
    {
        public int ButtonsPerPage = 16;

        [SerializeField] protected float m_ColorTransitionDuration;
        [SerializeField] protected GameObject ButtonPrefab;
        [NonSerialized] public int FirstButtonIndex = 0;

        protected float m_ColorTransitionValue;
        protected Material m_ColorBackground;
        protected PolyhydraPanel ParentPanel;

        override protected void BaseUpdate()
        {

            base.BaseUpdate();

            m_UIComponentManager.SetColor(Color.white);

            // TODO: Make linear into smooth step!
            if (m_ColorBackground &&
                m_TransitionValue == m_TransitionDuration &&
                m_ColorTransitionValue < m_ColorTransitionDuration)
            {
                m_ColorTransitionValue += Time.deltaTime;
                if (m_ColorTransitionValue > m_ColorTransitionDuration)
                {
                    m_ColorTransitionValue = m_ColorTransitionDuration;
                }
                float greyVal = 1 - m_ColorTransitionValue / m_ColorTransitionDuration;
                m_ColorBackground.color = new Color(greyVal, greyVal, greyVal);
            }
        }

        protected List<GameObject> _buttons;

        protected override void UpdateOpening()
        {
            if (m_ColorBackground && m_TransitionValue == 0)
            {
                m_ColorBackground.color = Color.white;
            }
            base.UpdateOpening();
        }

        protected override void UpdateClosing()
        {
            if (m_ColorBackground)
            {
                float greyVal = 1 - m_TransitionValue / m_TransitionDuration;
                m_ColorBackground.color = new Color(greyVal, greyVal, greyVal);
            }
            base.UpdateClosing();
        }

        public override void Init(GameObject rParent, string sText)
        {

            m_ColorBackground = m_Background.GetComponent<MeshRenderer>().material;
            base.Init(rParent, sText);
            ParentPanel = FindObjectOfType<PolyhydraPanel>();
            _buttons = new List<GameObject>();
            CreateButtons();
        }

        protected abstract string[] GetButtonList();

        protected virtual void CreateButtons()
        {
            foreach (var btn in _buttons)
            {
                Destroy(btn);
            }
            _buttons = new List<GameObject>();
            string[] buttonLabels = GetButtonList();
            int columns = 4;
            for (int buttonIndex = 0; buttonIndex < buttonLabels.Length; buttonIndex++)
            {

                GameObject rButton = Instantiate(ButtonPrefab);
                rButton.transform.parent = transform;
                rButton.transform.localRotation = Quaternion.identity;

                float xOffset = buttonIndex % columns;
                float yOffset = Mathf.FloorToInt(buttonIndex / (float)columns);
                Vector3 position = new Vector3(xOffset, -yOffset, 0);
                rButton.transform.localPosition = new Vector3(-0.52f, 0.15f, -0.08f) + (position * .35f);

                rButton.transform.localScale = Vector3.one * .3f;

                Renderer rButtonRenderer = rButton.GetComponent<Renderer>();
                rButtonRenderer.material.mainTexture = GetButtonTexture(buttonIndex);

                PolyhydraThingButton rButtonScript = rButton.GetComponent<PolyhydraThingButton>();
                rButtonScript.ButtonIndex = buttonIndex;
                rButtonScript.parentPopup = this;
                rButtonScript.SetDescriptionText(buttonLabels[buttonIndex]);
                rButtonScript.RegisterComponent();
                _buttons.Add(rButton);
            }
        }

        protected Texture2D GetButtonTexture(int buttonIndex)
        {
            return Resources.Load<Texture2D>(GetButtonTexturePath(buttonIndex + FirstButtonIndex));
        }

        protected abstract string GetButtonTexturePath(int i);
        override public void UpdateUIComponents(Ray rCastRay, bool inputValid, Collider parentCollider)
        {
            if (m_IsLongPressPopUp)
            {
                // Don't bother updating the popup if we're a long press and we're closing.
                if (m_CurrentState == State.Closing)
                {
                    return;
                }
                // If this is a long press popup and we're done holding the button down, get out.
                if (m_CurrentState == State.Standard && !inputValid)
                {
                    RequestClose();
                }
            }

            base.UpdateUIComponents(rCastRay, inputValid, parentCollider);
        }

        public abstract void HandleButtonPress(int ButtonIndex);

        public void PolyhydraThingButtonPressed(int ButtonIndex)
        {
            HandleButtonPress(ButtonIndex);
            ParentPanel.PolyhydraModel.RebuildPoly();
        }

    }
}
