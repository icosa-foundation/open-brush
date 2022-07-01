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
using System.IO;
using System.Linq;
using UnityEngine;


namespace TiltBrush
{

    public class PolyhydraPopUpWindowPresets : PolyhydraPopUpWindowBase
    {

        public override void Init(GameObject rParent, string sText)
        {
            ParentPanel = rParent.GetComponent<PolyhydraPanel>();
            FirstButtonIndex = ParentPanel.CurrentPresetPage * ButtonsPerPage;
            base.Init(rParent, sText);
        }

        private FileInfo[] GetDirectoryListing()
        {
            var dirInfo = new DirectoryInfo(ParentPanel.DefaultPresetsDirectory());
            return dirInfo.GetFiles("*.json");
        }

        protected override List<string> GetButtonList()
        {
            FileInfo[] AllFileInfo = GetDirectoryListing();
            return AllFileInfo.Select(f => f.Name.Replace(".json", ""))
                .Skip(FirstButtonIndex).Take(ButtonsPerPage).ToList();
        }

        public override Texture2D GetButtonTexture(string presetName)
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

        public override void HandleButtonPress(string presetName)
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
