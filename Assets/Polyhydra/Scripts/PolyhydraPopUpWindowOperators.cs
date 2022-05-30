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

using System;
using System.Collections.Generic;
using System.Linq;
using Polyhydra.Core;
using UnityEngine;


namespace TiltBrush
{

    public class PolyhydraPopUpWindowOperators : PolyhydraPopUpWindowBase
    {
        private HashSet<string> _disabledOps;
        
        public override void Init(GameObject rParent, string sText)
        {
            _disabledOps = new HashSet<string>
            {
                "Identity", "Weld", "RemoveTag", "VertexStellate"
            };
            ParentPanel = rParent.GetComponent<PolyhydraPanel>();
            FirstButtonIndex = ParentPanel.CurrentOperatorPage * ButtonsPerPage;
            base.Init(rParent, sText);
        }
        
        protected override List<string> GetButtonList()
        {
            return GetValidOps()
                .Skip(FirstButtonIndex)
                .Take(ButtonsPerPage)
                .ToList();
        }
        
        private IEnumerable<string> GetValidOps()
        {
            return Enum.GetNames(typeof(PolyMesh.Operation))
                .Where(x => !_disabledOps.Contains(x));
        }

        public override Texture2D GetButtonTexture(string action)
        {
            return ParentPanel.GetButtonTexture(PolyhydraButtonTypes.OperatorType, action);
        }

        public override void HandleButtonPress(string action)
        {
            PolyhydraPanel.FriendlyOpLabels.TryGetValue(action, out string friendlyLabel);
            ParentPanel.SetButtonTextAndIcon(PolyhydraButtonTypes.OperatorType, action, friendlyLabel);
            ParentPanel.ChangeCurrentOpType(action);
        }

        public void NextPage()
        {
            if (FirstButtonIndex + ButtonsPerPage < GetValidOps().Count())
            {
                FirstButtonIndex += ButtonsPerPage;
                CreateButtons();
            }
            ParentPanel.CurrentOperatorPage = FirstButtonIndex / ButtonsPerPage;
        }
        
        public void PrevPage()
        {
            FirstButtonIndex -= ButtonsPerPage;
            FirstButtonIndex = Mathf.Max(0, FirstButtonIndex);
            CreateButtons();
            ParentPanel.CurrentOperatorPage = FirstButtonIndex / ButtonsPerPage;
        }

    }
} // namespace TiltBrush
