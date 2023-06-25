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
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace TiltBrush
{
    public class LuaAutocompleteGenerator : Editor
    {
        [MenuItem("Open Brush/API/Generate Lua Autocomplete File")]
        static void Generate()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("You can only run this whilst in Play Mode");
                return;
            }

            Script script = new Script();
            LuaManager.ApiDocClasses = new List<ApiDocClass>();

            string docsPath = Path.Join(App.SupportPath(), "API Docs");
            if (!Directory.Exists(docsPath))
            {
                Directory.CreateDirectory(docsPath);
            }

            LuaManager.Instance.RegisterApiClasses(script);
            
            // Manually add some entries that aren't added the standard way
            var vectorProp = new ApiDocType
            {
                PrimitiveType = ApiDocPrimitiveType.UserData,
                CustomTypeName = "Vector3"
            };
            var rotationProp = new ApiDocType
            {
                PrimitiveType = ApiDocPrimitiveType.UserData,
                CustomTypeName = "Rotation"
            };
            var toolApiDocClass = new ApiDocClass
            {
                Name = "Tool",
                Methods = new List<ApiDocMethod>(),
                Properties = new List<ApiDocProperty>
                {
                    new() {Name="startPosition", PropertyType = vectorProp},
                    new() {Name="endPosition", PropertyType = vectorProp},
                    new() {Name="vector", PropertyType = vectorProp},
                    new() {Name="rotation", PropertyType = rotationProp},
                }
            };
            LuaManager.ApiDocClasses.Add(toolApiDocClass);

            // JSON docs if needed
            // var json = JsonConvert.SerializeObject(LuaManager.ApiDocClasses, Formatting.Indented);
            // File.WriteAllText(Path.Join(docsPath, "docs.json"), json);

            // Generate __autocomplete.lua
            var autocomplete = new StringBuilder();
            string autocompleteFilePath = Path.Combine("Assets/Resources/LuaModules", "__autocomplete.lua");
            foreach (var klass in LuaManager.ApiDocClasses)
            {
                autocomplete.Append(klass.AutocompleteSerialize());
            }
            File.WriteAllText(autocompleteFilePath, autocomplete.ToString());
            LuaManager.Instance.CopyLuaModules(); // Update the copy in User docs (also done on app start)

            // Generate markdown docs
            foreach (var klass in LuaManager.ApiDocClasses)
            {
                var markDown = klass.MarkdownSerialize();
                File.WriteAllText(Path.Join(docsPath, $"{klass.Name}.md"), markDown);
            }
            
            // Done
            LuaManager.ApiDocClasses = null;
            Debug.Log($"Finished Generating Lua Autocomplete");
        }
    }
}
