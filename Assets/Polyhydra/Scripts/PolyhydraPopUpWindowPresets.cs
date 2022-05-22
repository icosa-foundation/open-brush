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
using Polyhydra.Core;
using TMPro;
using UnityEngine;


namespace TiltBrush
{

    public class PolyhydraPopUpWindowPresets : PolyhydraPopUpWindowBase
    {

        public float xSpacing = 2.5f;
        public float ySpacing = .25f;

        protected override List<string> GetButtonList()
        {
            var dirInfo = new DirectoryInfo(ParentPanel.m_PresetsPath);
            FileInfo[] AllFileInfo = dirInfo.GetFiles("*.json");
            return AllFileInfo.Select(f => f.Name).ToList();
        }

        public override Texture2D GetButtonTexture(string presetName)
        {
            presetName = presetName.Replace(".json", ".png");
            var path = Path.Combine(ParentPanel.m_PresetsPath, presetName);
            var fileData = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2);
            tex.LoadImage(fileData);
            return tex;
        }

        protected override string GetButtonTexturePath(string action)
        {
            // Not used in this subclass
            throw new NotImplementedException();
        }
        
        public override void HandleButtonPress(string presetName)
        {
            ParentPanel.LoadPresetFromFile(Path.Combine(ParentPanel.m_PresetsPath, presetName));
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
