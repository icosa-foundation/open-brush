// Copyright 2022 The Open Brush Authors
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
using System.IO;
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
            ParentPanel = rParent.GetComponent<PolyhydraPanel>();
            FirstButtonIndex = ParentPanel.CurrentPresetPage * ButtonsPerPage;
            m_ColorBackground = m_Background.GetComponent<MeshRenderer>().sharedMaterial;
            base.Init(rParent, sText);
            ParentPanel = rParent.GetComponent<PolyhydraPanel>();
            _buttons = new List<GameObject>();
            CreateButtons();
        }

        protected virtual void CreateButtons()
        {
            foreach (var btn in _buttons)
            {
                Destroy(btn);
            }
            _buttons = new List<GameObject>();
            List<string> buttonActionNames = GetButtonList();
            int columns = 4;
            for (int buttonIndex = 0; buttonIndex < buttonActionNames.Count; buttonIndex++)
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

                PolyhydraShapeGalleryButton rButtonScript = rButton.GetComponent<PolyhydraShapeGalleryButton>();
                rButtonScript.parentPopup = this;
                rButtonScript.SetDescriptionText(buttonActionNames[buttonIndex].Replace("_", ""));
                rButtonRenderer.material.mainTexture = GetButtonTexture(buttonActionNames[buttonIndex]);
                rButtonScript.ButtonAction = buttonActionNames[buttonIndex];
                rButtonScript.RegisterComponent();
                _buttons.Add(rButton);
            }
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

        public string LabelTextFormatter(string text)
        {
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            text = textInfo.ToTitleCase(text).Replace(" ", "_");
            return text;
        }

        private FileInfo[] GetDirectoryListing()
        {
            var dirInfo = new DirectoryInfo(ParentPanel.DefaultPresetsDirectory());
            return dirInfo.GetFiles("*.json");
        }

        protected List<string> GetButtonList()
        {
            FileInfo[] AllFileInfo = GetDirectoryListing();
            return AllFileInfo.Select(f => f.Name.Replace(".json", ""))
                .Skip(FirstButtonIndex).Take(ButtonsPerPage).ToList();
        }

        public Texture2D GetButtonTexture(string presetName)
        {
            presetName = $"{presetName}.png";
            var path = Path.Combine(ParentPanel.DefaultPresetsDirectory(), presetName);
            if (!File.Exists(path))
            {
                presetName = presetName.Replace(".png", ".jpg");
                path = Path.Combine(ParentPanel.DefaultPresetsDirectory(), presetName);
                if (!File.Exists(path))
                {
                    return Resources.Load<Texture2D>("Icons/bigquestion");
                }
            }
            return _GetButtonTexture(path);
        }

        private Texture2D _GetButtonTexture(string path)
        {

            var fileData = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2);
            tex.LoadImage(fileData);
            return tex;
        }

        public void HandleButtonPress(string presetName)
        {
            ParentPanel.HandleLoadPreset(Path.Combine(ParentPanel.DefaultPresetsDirectory(), $"{presetName}.json"));
        }

        public void NextPage()
        {
            if (FirstButtonIndex + ButtonsPerPage < GetDirectoryListing().Length) ;
            {
                FirstButtonIndex += ButtonsPerPage;
                CreateButtons();
            }
            ParentPanel.CurrentPresetPage = FirstButtonIndex / ButtonsPerPage;
        }

        public void PrevPage()
        {
            FirstButtonIndex -= ButtonsPerPage;
            FirstButtonIndex = Mathf.Max(0, FirstButtonIndex);
            CreateButtons();
            ParentPanel.CurrentPresetPage = FirstButtonIndex / ButtonsPerPage;
        }
    }
} // namespace TiltBrush
