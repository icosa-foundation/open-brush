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
    /// Prev/next navigation for a selected file's chapters or the loaded native IMM's viewpoints.
    /// </summary>
    public class QuillChapterNavButton : BaseButton
    {
        [UnityEngine.SerializeField] private bool m_IsNext;
        [UnityEngine.SerializeField] private bool m_IsViewpoint;
        [UnityEngine.SerializeField] private bool m_IsContinue;

        private UnityEngine.Texture2D m_PauseTexture;
        private bool? m_ShowingPause;

        public void RefreshPlaybackState()
        {
            bool pause = NativeImmImport.CanPause;
            SetButtonAvailable(pause || NativeImmImport.CanContinue);
            if (m_ShowingPause == pause) return;
            if (pause && m_PauseTexture == null)
                m_PauseTexture = UnityEngine.Resources.Load<UnityEngine.Texture2D>("Icons/pause");
            SetButtonTexture(pause ? m_PauseTexture : m_ButtonTexture);
            SetDescriptionText(pause ? "Pause IMM" : "Play / Continue IMM");
            m_ShowingPause = pause;
        }

        protected override void OnButtonPressed()
        {
            base.OnButtonPressed();
            if (m_IsContinue)
            {
                if (NativeImmImport.CanPause) NativeImmImport.PausePlayback();
                else NativeImmImport.ContinuePlayback();
                return;
            }
            if (m_IsViewpoint)
            {
                NativeImmImport.CycleViewpoint(m_IsNext ? 1 : -1);
                return;
            }
            var panel = GetComponentInParent<QuillLibraryPanel>();
            if (m_IsNext) panel?.OnNextChapterPressed();
            else panel?.OnPrevChapterPressed();
        }
    }
}
