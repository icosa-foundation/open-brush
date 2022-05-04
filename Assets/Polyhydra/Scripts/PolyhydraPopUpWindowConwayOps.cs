// Copyright 2020 The Tilt Brush Authors
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
using System.Linq;
using Polyhydra.Core;
using UnityEngine;


namespace TiltBrush
{

    public class PolyhydraPopUpWindowConwayOps : PolyhydraPopUpWindowBase
    {

        [NonSerialized] protected int OpStackIndex = 0;

        protected override string[] GetButtonList()
        {
            return Enum.GetNames(typeof(PolyMesh.Operation)).Skip(FirstButtonIndex).Take(ButtonsPerPage).ToArray();
        }

        public override void SetPopupCommandParameters(int commandParam, int commandParam2)
        {
            base.SetPopupCommandParameters(commandParam, commandParam2);
            OpStackIndex = commandParam;
        }

        protected override string GetButtonTexturePath(int i)
        {
            return $"IconButtons/{(PolyMesh.Operation)i}";
        }

        public override void HandleButtonPress(int relativeButtonIndex)
        {
            int absoluteButtonIndex = relativeButtonIndex + FirstButtonIndex;
            var ops = ParentPanel.PolyhydraModel.ConwayOperators;

            PolyHydraEnums.OpConfig opConfig = PolyHydraEnums.OpConfigs[(PolyMesh.Operation)absoluteButtonIndex];

            var op = ops[OpStackIndex];
            op.opType = (PolyMesh.Operation)absoluteButtonIndex;
            op.amount = opConfig.amountDefault;
            op.amount2 = opConfig.amount2Default;

            ops[OpStackIndex] = op;
            ParentPanel.PolyhydraModel.ConwayOperators = ops;
            ParentPanel.ButtonsConwayOps[OpStackIndex].SetButtonTexture(GetButtonTexture(relativeButtonIndex));
            ParentPanel.ButtonsConwayOps[OpStackIndex].SetDescriptionText(GetButtonList()[relativeButtonIndex]);

            if (opConfig.usesFaces)
            {
                ParentPanel.ButtonsFaceSel[OpStackIndex].gameObject.SetActive(true);
            }
            else
            {
                ParentPanel.ButtonsFaceSel[OpStackIndex].gameObject.SetActive(false);
            }

            if (opConfig.usesAmount)
            {
                ParentPanel.SlidersConwayOps[OpStackIndex * 2].gameObject.SetActive(true);
                ParentPanel.SlidersConwayOps[OpStackIndex * 2].Min = opConfig.amountSafeMin;
                ParentPanel.SlidersConwayOps[OpStackIndex * 2].Max = opConfig.amountSafeMax;
                ParentPanel.SlidersConwayOps[OpStackIndex * 2].UpdateValueAbsolute(opConfig.amountDefault);
            }
            else
            {
                ParentPanel.SlidersConwayOps[OpStackIndex * 2].gameObject.SetActive(false);
            }

            if (opConfig.usesAmount2)
            {
                ParentPanel.SlidersConwayOps[OpStackIndex * 2 + 1].gameObject.SetActive(true);
                ParentPanel.SlidersConwayOps[OpStackIndex * 2 + 1].Min = opConfig.amount2SafeMin;
                ParentPanel.SlidersConwayOps[OpStackIndex * 2 + 1].Max = opConfig.amount2SafeMax;
                ParentPanel.SlidersConwayOps[OpStackIndex * 2 + 1].UpdateValueAbsolute(opConfig.amount2Default);
            }
            else
            {
                ParentPanel.SlidersConwayOps[OpStackIndex * 2 + 1].gameObject.SetActive(false);
            }
        }

        public void NextPage()
        {
            if (FirstButtonIndex + ButtonsPerPage < Enum.GetNames(typeof(PolyMesh.Operation)).Length)
            {
                FirstButtonIndex += ButtonsPerPage;
                CreateButtons();

            }
        }
        public void PrevPage()
        {
            FirstButtonIndex -= ButtonsPerPage;
            FirstButtonIndex = Mathf.Max(0, FirstButtonIndex);
            CreateButtons();
        }

    }
} // namespace TiltBrush
