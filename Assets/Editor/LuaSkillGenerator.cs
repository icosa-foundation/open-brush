// Copyright 2023 The Tilt Brush Authors
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
using System.Text;
using MoonSharp.Interpreter;
using UnityEditor;
using UnityEngine;

namespace TiltBrush
{

    public class LuaSkillGenerator : Editor
    {
        [MenuItem("Open Brush/API/Generate Lua Skill")]
        static void GenerateSkill()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("You can only run this whilst in Play Mode");
                return;
            }

            LuaDocsGenerator.GenerateDocs();

            string mainDocsSourcePath = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal),
                "open-brush-docs",
                "user-guide",
                "using-plugins"
            );
            string luaDocsSourcePath = Path.Join(LuaManager.Instance.UserPluginsPath(), "LuaDocs");
            string modulesSourcePath = Path.Join(LuaManager.Instance.UserPluginsPath(), "LuaModules");
            TextAsset[] examplePlugins = Resources.LoadAll<TextAsset>("LuaScriptExamples");

            string skillPath = Path.Join(LuaManager.Instance.UserPluginsPath(), "LuaSkill");
            string luaDocsDestinationPath = Path.Join(skillPath, "api-docs");
            string mainDocsDestinationPath = Path.Join(skillPath, "guides");
            string modulesDestinationPath = Path.Join(skillPath, "lua-modules");
            string examplesDestinationPath = Path.Join(skillPath, "examples");

            Directory.CreateDirectory(skillPath);
            Directory.CreateDirectory(luaDocsDestinationPath);
            Directory.CreateDirectory(mainDocsDestinationPath);
            Directory.CreateDirectory(modulesDestinationPath);
            Directory.CreateDirectory(examplesDestinationPath);

            foreach (var plugin in examplePlugins)
            {
                File.WriteAllText(Path.Combine(examplesDestinationPath, plugin.name + ".lua"), plugin.text);
            }

            foreach (var file in Directory.GetFiles(luaDocsSourcePath))
            {
                File.Copy(file, Path.Combine(luaDocsDestinationPath, Path.GetFileName(file)), true);
            }

            foreach (var file in Directory.GetFiles(modulesSourcePath))
            {
                File.Copy(file, Path.Combine(modulesDestinationPath, Path.GetFileName(file)), true);
            }

            if (Directory.Exists(mainDocsSourcePath))
            {
                foreach (var file in Directory.GetFiles(mainDocsSourcePath, "*", SearchOption.AllDirectories))
                {
                    var relative = file.Substring(mainDocsSourcePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var destFile = Path.Combine(mainDocsDestinationPath, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                    File.Copy(file, destFile, true);
                }
            }
        }
    }
}
