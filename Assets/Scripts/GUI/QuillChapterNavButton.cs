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
    /// <summary>
    /// Prev/next chapter navigation button for the Quill file browser.
    /// Finds its target QuillFileButton via GetComponentInParent at runtime.
    /// </summary>
    public class QuillChapterNavButton : BaseButton
    {
        [UnityEngine.SerializeField] private bool m_IsNext;

        protected override void OnButtonPressed()
        {
            base.OnButtonPressed();
            var target = GetComponentInParent<QuillFileButton>();
            if (m_IsNext) target?.OnNextChapter();
            else target?.OnPrevChapter();
        }
    }
}
