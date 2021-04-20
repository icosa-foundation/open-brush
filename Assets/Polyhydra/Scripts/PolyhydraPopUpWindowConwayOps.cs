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
using Conway;


namespace TiltBrush {

public class PolyhydraPopUpWindowConwayOps : PolyhydraPopUpWindowBase {
  
    [NonSerialized] protected int OpIndex = 0;


    protected override string[] GetButtonList()
    {
      return Enum.GetNames(typeof(Ops)).Take(24).ToArray();
    }

    protected override string GetButtonTexturePath(int i)
    {
      return $"IconButtons/{(Ops) i}";
    }

    public override void HandleButtonPress(int buttonIndex)
    {
      var ops = ParentPanel.PolyhydraModel.ConwayOperators;
      PolyHydraEnums.OpConfig opConfig = PolyHydraEnums.OpConfigs[(Ops)buttonIndex];
      while (ops.Count < OpIndex)
      {
        ops.Add(new VrUiPoly.ConwayOperator());
      }
      
      var op = ops[OpIndex];
      op.opType = (Ops)buttonIndex;
      op.amount = opConfig.amountDefault;
      op.amount2 = opConfig.amount2Default;
      
      ops[OpIndex] = op;
      ParentPanel.PolyhydraModel.ConwayOperators = ops;
      ParentPanel.ButtonsConwayOps[OpIndex].SetButtonTexture(GetButtonTexture(buttonIndex));

      if (opConfig.usesAmount)
      {
        ParentPanel.SlidersConwayOps[OpIndex * 2].gameObject.SetActive(true);
        ParentPanel.SlidersConwayOps[OpIndex * 2].Min = opConfig.amountSafeMin;
        ParentPanel.SlidersConwayOps[OpIndex * 2].Max = opConfig.amountSafeMax;
        ParentPanel.SlidersConwayOps[OpIndex * 2].UpdateValueAbsolute(opConfig.amountDefault);
      }
      else
      {
        ParentPanel.SlidersConwayOps[OpIndex * 2].gameObject.SetActive(false);
      }

      if (opConfig.usesAmount2)
      {
        ParentPanel.SlidersConwayOps[OpIndex * 2 + 1].gameObject.SetActive(true);
        ParentPanel.SlidersConwayOps[OpIndex * 2 + 1].Min = opConfig.amount2SafeMin;
        ParentPanel.SlidersConwayOps[OpIndex * 2 + 1].Max = opConfig.amount2SafeMax;
        ParentPanel.SlidersConwayOps[OpIndex * 2 + 1].UpdateValueAbsolute(opConfig.amount2Default);
      }
      else
      {
        ParentPanel.SlidersConwayOps[OpIndex * 2 + 1].gameObject.SetActive(false);
      }
    }



}
}  // namespace TiltBrush
