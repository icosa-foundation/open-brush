// Copyright 2024 The Open Brush Authors
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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TiltBrush
{
    public class OpenImagePickerPopupButton : BaseButton
    {
        public Transform m_ImagePickerPopup;
        public string m_PropertyName;
        public int ImageIndex { get; set; }

        // Scripts can define a limited set of images to display in the picker
        public List<ReferenceImage> ImageSet;

        public ReferenceImage GetImage(int index)
        {
            if (ImageSet == null)
            {
                return ReferenceImageCatalog.m_Instance.IndexToImage(index);
            }
            return ImageSet[index];
        }

        [SerializeField] private UnityEvent<OpenImagePickerPopupButton> m_AfterPopupAction;

        protected override void OnButtonPressed()
        {
            BasePanel panel = m_Manager.GetPanelForPopUps();
            if (panel != null)
            {
                float zOffset = App.Config.m_SdkMode == SdkMode.Monoscopic ? 0.3f : -0.3f;
                panel.CreatePopUp(
                    m_ImagePickerPopup.gameObject,
                    transform.position + (transform.rotation * Vector3.forward * zOffset),
                    true, true
                );
                ResetState();
            }

            var popup = panel.PanelPopUp as ImagePickerPopup;
            if (popup != null)
            {
                popup.ActiveItemIndex = ImageIndex;
                popup.m_OpenerButton = this;
            }
        }

        override public void GazeRatioChanged(float gazeRatio)
        {
            GetComponent<Renderer>().material.SetFloat("_Distance", gazeRatio);
        }

        public void UpdateValue(Texture2D tex, string propertyName, int textureIndex, float aspect)
        {
            if (tex != null)
            {
                if (m_ButtonRenderer != null)
                {
                    SetButtonTexture(tex, aspect);
                }
                m_ButtonTexture = tex;
                GetComponent<MeshRenderer>().material.mainTexture = tex;
            }
            m_PropertyName = propertyName;
            ImageIndex = textureIndex;
            SetDescriptionText(propertyName);
        }

        public void OnItemSelected(int itemIndex)
        {
            ImageIndex = itemIndex;
            m_AfterPopupAction?.Invoke(this);
        }
        public int GetImageCount()
        {
            return ImageSet.Count;
        }

        public int FilenameToIndex(string filename)
        {
            if (ImageSet == null)
            {
                return ReferenceImageCatalog.m_Instance.FilenameToIndex(filename);
            }
            return ImageSet.FindIndex(image => image.FileName == filename);
        }
    }
} // namespace TiltBrush
