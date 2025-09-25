﻿// Copyright 2022 The Open Brush Authors
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

using System.Globalization;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{

    public class PolyhydraPopUpWindowShapeGallery : PopUpWindow
    {

        public int ButtonsPerPage = 16;

        [SerializeField] protected float m_ColorTransitionDuration;
        [SerializeField] protected GameObject ButtonPrefab;
        [NonSerialized] public int FirstButtonIndex = 0;

        private Dictionary<string, TextAsset> ShapeGalleryJson;
        private Dictionary<string, Texture2D> ShapeGalleryIcons;

        protected float m_ColorTransitionValue;
        protected Material m_ColorBackground;
        protected PolyhydraModeTray ParentPanel;

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
            InitShapeGalleryItems();
            ParentPanel = rParent.GetComponentInChildren<PolyhydraModeTray>();
            FirstButtonIndex = ParentPanel.CurrentGalleryPage * ButtonsPerPage;
            m_ColorBackground = m_Background.GetComponent<MeshRenderer>().sharedMaterial;
            base.Init(rParent, sText);
            _buttons = new List<GameObject>();
            CreateButtons();
        }

        private void InitShapeGalleryItems()
        {
            ShapeGalleryJson = Resources.LoadAll<TextAsset>("Shape Gallery Presets").ToDictionary(i => i.name);
            ShapeGalleryIcons = Resources.LoadAll<Texture2D>("Shape Gallery Presets").ToDictionary(i => i.name);
        }

        protected virtual void CreateButtons()
        {
            foreach (var btn in _buttons)
            {
                Destroy(btn);
            }
            _buttons = new List<GameObject>();
            ItemListResults itemList = GetButtonList();
            int columns = 4;
            for (int buttonIndex = 0; buttonIndex < itemList.ItemCount; buttonIndex++)
            {
                string buttonName = itemList.Items[buttonIndex];
                GameObject rButton = Instantiate(ButtonPrefab, transform, true);
                rButton.transform.localRotation = Quaternion.identity;

                float xOffset = buttonIndex % columns;
                float yOffset = Mathf.FloorToInt(buttonIndex / (float)columns);
                Vector3 position = new Vector3(xOffset, -yOffset, 0);
                rButton.transform.localPosition = new Vector3(-0.52f, 0.15f, -0.08f) + (position * .35f);
                rButton.transform.localScale = Vector3.one * .3f;

                Renderer rButtonRenderer = rButton.GetComponent<Renderer>();

                PolyhydraShapeGalleryButton rButtonScript = rButton.GetComponent<PolyhydraShapeGalleryButton>();
                rButtonScript.parentPopup = this;
                rButtonScript.SetDescriptionText(buttonName.Replace("_", ""));
                rButtonRenderer.material.mainTexture = GetButtonTexture(buttonName);
                rButtonScript.ButtonAction = buttonName;
                rButtonScript.RegisterComponent();
                _buttons.Add(rButton);
            }

            // No previous nav on the first page
            m_PrevButton.SetActive(FirstButtonIndex != 0);

            // No next nav on last page
            m_NextButton.SetActive(itemList.NextPageExists);
        }

        public override void UpdateUIComponents(Ray rCastRay, bool inputValid, Collider parentCollider)
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

        public void PolyhydraThingButtonPressed(string action)
        {
            HandleButtonPress(action);
            PreviewPolyhedron.m_Instance.RebuildPoly();
        }

        protected ItemListResults GetButtonList()
        {
            var allItems = ShapeGalleryJson.Keys.ToList();
            int totalItemCount = allItems.Count;
            int nextPageButtonIndex = FirstButtonIndex + ButtonsPerPage;
            bool nextPageExists = nextPageButtonIndex <= totalItemCount;
            return new ItemListResults(
                allItems.Skip(FirstButtonIndex).Take(ButtonsPerPage).ToList(),
                nextPageExists
            );
        }

        public Texture2D GetButtonTexture(string presetName)
        {
            return ShapeGalleryIcons[presetName];
        }

        public void HandleButtonPress(string presetName)
        {
            PolyhydraPanel polyhydraPanel = PanelManager.m_Instance.GetPanelByType(BasePanel.PanelType.Polyhydra) as PolyhydraPanel;
            if (polyhydraPanel != null)
            {
                polyhydraPanel.HandleLoadPresetFromString(ShapeGalleryJson[presetName].text);
            }
        }

        public void NextPage()
        {
            FirstButtonIndex += ButtonsPerPage;
            CreateButtons();
            ParentPanel.CurrentGalleryPage = FirstButtonIndex / ButtonsPerPage;
        }

        public void PrevPage()
        {
            FirstButtonIndex -= ButtonsPerPage;
            FirstButtonIndex = Mathf.Max(0, FirstButtonIndex);
            CreateButtons();
            ParentPanel.CurrentGalleryPage = FirstButtonIndex / ButtonsPerPage;
        }
    }
} // namespace TiltBrush
