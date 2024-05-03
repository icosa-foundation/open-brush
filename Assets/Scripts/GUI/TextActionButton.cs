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

using System;
using UnityEngine;
using TMPro;

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
