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

    public class LuaDocsGenerator : Editor
    {
        [MenuItem("Open Brush/API/Generate Lua Docs")]
        static void GenerateDocs()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("You can only run this whilst in Play Mode");
                return;
            }

            Script script = new Script();

            // Initializing this list triggers the docs generation via RegisterApiClasses
            LuaDocsRegistration.ApiDocClasses = new List<LuaDocsClass>();
            LuaManager.Instance.RegisterApiClasses(script);

            // List wrappers aren't places in the script's namespace but we do want to register them
            // so we can generate docs for them.
            LuaDocsRegistration.RegisterForDocs(typeof(CameraPathListApiWrapper), false);
            LuaDocsRegistration.RegisterForDocs(typeof(EnvironmentListApiWrapper), false);
            LuaDocsRegistration.RegisterForDocs(typeof(GuideListApiWrapper), false);
            LuaDocsRegistration.RegisterForDocs(typeof(ImageListApiWrapper), false);
            LuaDocsRegistration.RegisterForDocs(typeof(LayerListApiWrapper), false);
            LuaDocsRegistration.RegisterForDocs(typeof(ModelListApiWrapper), false);
            LuaDocsRegistration.RegisterForDocs(typeof(VideoListApiWrapper), false);
            LuaDocsRegistration.RegisterForDocs(typeof(StrokeListApiWrapper), false);

            // Manually add some entries that aren't added the standard way
            var vectorProp = new LuaDocsType {PrimitiveType = LuaDocsPrimitiveType.UserData, CustomTypeName = "Vector3"};
            var rotationProp = new LuaDocsType {PrimitiveType = LuaDocsPrimitiveType.UserData, CustomTypeName = "Rotation"};
            var toolApiDocClass = new LuaDocsClass
            {
                Name = "Tool",
                Methods = new List<LuaDocsMethod>(),
                Description = "A class to interact with Scripted Tools",
                Properties = new List<LuaDocsProperty>
                {
                    new() {Name="startPosition", PropertyType = vectorProp, Description = "The position of the tool when the trigger was pressed"},
                    new() {Name="endPosition", PropertyType = vectorProp, Description = "The position of the tool when the trigger was released"},
                    new() {Name="vector", PropertyType = vectorProp, Description = "The vector from startPosition to endPosition"},
                    new() {Name="rotation", PropertyType = rotationProp, Description = "The rotation from startPosition to endPosition"},
                }
            };
            LuaDocsRegistration.ApiDocClasses.Add(toolApiDocClass);

            // JSON docs if needed
            // var json = JsonConvert.SerializeObject(LuaDocsRegistration.ApiDocClasses, Formatting.Indented);
            // File.WriteAllText(Path.Join(docsPath, "docs.json"), json);

            // Generate __autocomplete.lua
            var autocomplete = new StringBuilder();
            string autocompleteFilePath = Path.Combine("Assets/Resources/LuaModules", "__autocomplete.lua");
            foreach (var klass in LuaDocsRegistration.ApiDocClasses)
            {
                // Only top level classes are included in autocomplete
                // This excludes all ListWrapper classes which can't be instantiated directly
                // And don't have any useful static members
                if (!klass.IsTopLevelClass) continue;
                autocomplete.Append(klass.AutocompleteSerialize());
            }
            File.WriteAllText(autocompleteFilePath, autocomplete.ToString());
            LuaManager.Instance.CopyLuaModules(); // Update the copy in User docs (also done on app start)

            // Generate markdown docs
            string docsPath = Path.Join(ApiManager.Instance.UserScriptsPath(), "LuaDocs");
            if (!Directory.Exists(docsPath)) Directory.CreateDirectory(docsPath);
            foreach (var klass in LuaDocsRegistration.ApiDocClasses)
            {
                var markDown = klass.MarkdownSerialize();
                File.WriteAllText(Path.Join(docsPath, $"{klass.Name.ToLower()}.md"), markDown);
            }
            
            // Done
            LuaDocsRegistration.ApiDocClasses = null;
            Debug.Log($"Finished Generating Lua Autocomplete");
        }
    }
}
