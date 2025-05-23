// Copyright 2025 The Open Brush Authors
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
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public class SketchbookPanelFilterPopupWindow : OptionsPopUpWindow
    {
        [SerializeField] private GameObject m_CategoryButtonGroup;
        [SerializeField] private List<GameObject> m_OrderByButtonGroups;
        [SerializeField] private ToggleButton m_CuratedButton;

        public void HandleCancelButton()
        {
            RequestClose(true);
        }

        public override void Init(GameObject rParent, string sText)
        {
            base.Init(rParent, sText);
            var currentQuery = ((SketchbookPanel)m_ParentPanel).CurrentQuery;
            m_OrderByButtonGroups.ForEach(x => x.SetActive(false));
            CurrentOrderByGroup.SetActive(true);
            var currentOrderByBtn = CurrentOrderByGroup.GetComponentsInChildren<RadioButton>().FirstOrDefault(x => x.m_Value == currentQuery.OrderBy);
            if (currentOrderByBtn != null) currentOrderByBtn.IsToggledOn = true;
            var currentCategoryBtn = m_CategoryButtonGroup.GetComponentsInChildren<RadioButton>().FirstOrDefault(x => x.m_Value == currentQuery.Category);
            if (currentCategoryBtn != null) currentCategoryBtn.IsToggledOn = true;
            m_CuratedButton.IsToggledOn = currentQuery.Curated == CuratedChoices.TRUE;
        }

        private GameObject CurrentOrderByGroup
        {
            get
            {
                var currentSet = ((SketchbookPanel)m_ParentPanel).CurrentSketchSetType;
                return GetCurrentButtonGroup(currentSet);
            }
        }

        public GameObject GetCurrentButtonGroup(SketchSetType setType)
        {
            return m_OrderByButtonGroups[(int)setType];
        }


        private string GetRadioGroupValue(GameObject group)
        {
            return group.GetComponentsInChildren<RadioButton>().FirstOrDefault(x => x.IsToggledOn)?.m_Value;
        }

        public void HandleOKButton()
        {
            SketchSetType currentSet = ((SketchbookPanel)m_ParentPanel).CurrentSketchSetType;

            var currentCategory = GetRadioGroupValue(m_CategoryButtonGroup);
            var currentOrderBy = GetRadioGroupValue(CurrentOrderByGroup);

            SketchCatalog.m_Instance.UpdateOrderBy(currentSet, currentOrderBy);
            SketchCatalog.m_Instance.UpdateCategory(currentSet, currentCategory);
            SketchCatalog.m_Instance.UpdateCurated(currentSet, m_CuratedButton.IsToggledOn ? CuratedChoices.TRUE : CuratedChoices.ANY);
            ((SketchbookPanel)m_ParentPanel).RefreshCurrentSet();
            RequestClose(true);
        }
    }
}
