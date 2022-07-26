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

        private DirectoryInfo[] GetSubdirsListing()
        {

            var dirInfo = new DirectoryInfo(ParentPanel.CurrentPresetsDirectory);
            return dirInfo.GetDirectories();
        }

        private FileInfo[] GetPresetFilesList()
        {
            var dirInfo = new DirectoryInfo(ParentPanel.CurrentPresetsDirectory);
            return dirInfo.GetFiles("*.json");
        }

        protected override List<string> GetFoldersList()
        {
            // Folders are only visible on page 1
            if (FirstButtonIndex != 0) return new List<string>();

            DirectoryInfo[] presetFilesList = GetSubdirsListing();
            var dirNames = presetFilesList.Select(d => d.Name).ToList();
            if (!ParentPanel.PresetRootIsCurrent())
            {
                dirNames = dirNames.Prepend("..").ToList();
            }
            return dirNames;
        }

        protected override List<string> GetItemsList()
        {
            FileInfo[] presetFilesList = GetPresetFilesList();
            var visibleFolderCount = GetFoldersList().Count;
            return presetFilesList.Select(f => f.Name.Replace(".json", ""))
                .Skip(FirstButtonIndex).Take(ButtonsPerPage - visibleFolderCount).ToList();
        }

        public override Texture2D GetButtonTexture(string presetName)
        {
            presetName = $"{presetName}.png";
            var path = Path.Combine(ParentPanel.CurrentPresetsDirectory, presetName);
            if (!File.Exists(path))
            {
                presetName = presetName.Replace(".png", ".jpg");
                path = Path.Combine(ParentPanel.CurrentPresetsDirectory, presetName);
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

        public override void HandleButtonPress(string action, bool isFolder)
        {
            if (isFolder)
            {
                if (action == "..")
                {
                    var newDir = Directory.GetParent(ParentPanel.CurrentPresetsDirectory);
                    if (ParentPanel.IsPresetsSubdirOrSameDir(newDir.FullName))
                    {
                        ParentPanel.CurrentPresetsDirectory = newDir.FullName;
                    }
                }
                else
                {
                    ParentPanel.CurrentPresetsDirectory = Path.Combine(
                        ParentPanel.CurrentPresetsDirectory,
                        action
                    );
                }
                FirstButtonIndex = 0;
                CreateButtons();
            }
            else
            {
                ParentPanel.HandleLoadPresetFromPath(Path.Combine(ParentPanel.CurrentPresetsDirectory, $"{action}.json"));
                PreviewPolyhedron.m_Instance.RebuildPoly();
            }
        }

        public void NextPage()
        {
            if (FirstButtonIndex + ButtonsPerPage < GetPresetFilesList().Length) ;
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
