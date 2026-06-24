// Copyright 2026 The Open Brush Authors
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

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint("load.quill", "Loads a Quill sketch from the given path")]
        public static void LoadQuill(string path, int maxStrokes = 0, bool loadAnimations = false, string layerName = null, int chapterIndex = -1)
        {
            Quill.Load(path, maxStrokes, loadAnimations, layerName, flattenHierarchy: true, chapterIndex: chapterIndex);
        }
    }
}
