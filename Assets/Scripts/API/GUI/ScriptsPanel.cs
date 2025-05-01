// Copyright 2022 The Tilt Brush Authors
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

namespace TiltBrush
{
    public class ScriptsPanel : BasePanel
    {

        public BaseButton SymmetryScriptButton;
        public BaseButton PointerScriptButton;
        public BaseButton ToolScriptButton;
        public ToggleButton BackgroundScriptsButton;

        protected override void OnEnablePanel()
        {
            base.OnEnablePanel();
            // Safe to run multiple times as it checks m_IsInitialized
            LuaManager.Instance?.Init();
        }

        public void InitScriptUiNav()
        {
            foreach (var nav in GetComponentsInChildren<ScriptUiNav>())
            {
                nav.Init();
            }
            foreach (var nav in GetComponentsInChildren<ScriptUiNavMultiple>())
            {
                nav.Init();
            }
        }

        public void TogglePointerScript(ToggleButton btn)
        {
            LuaManager.Instance.EnablePointerScript(btn.IsToggledOn);
        }

        public void ToggleBackgroundScripts(ToggleButton btn)
        {
            LuaManager.Instance.EnableBackgroundScripts(btn.IsToggledOn);
        }

        public void ConfigureScriptButton(LuaApiCategory category, string scriptName, string description)
        {
            BaseButton btn = category switch
            {
                LuaApiCategory.PointerScript => PointerScriptButton,
                LuaApiCategory.ToolScript => ToolScriptButton,
                LuaApiCategory.SymmetryScript => SymmetryScriptButton,
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
            if (btn)
            {
                btn.SetDescriptionText($"{category}: {scriptName}");
                if (description != null)
                {
                    btn.SetExtraDescriptionText(description);
                }
            }
        }

        public void HandleGoogleDriveSync()
        {
            if (!App.DriveSync.IsFolderOfTypeSynced(DriveSync.SyncedFolderType.Scripts))
            {
                App.DriveSync.ToggleSyncOnFolderOfType(DriveSync.SyncedFolderType.Scripts);
            }
            App.DriveSync.SyncLocalFilesAsync().AsAsyncVoid();
        }
    }
}
