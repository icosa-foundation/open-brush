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

namespace TiltBrush
{
    public enum TransformPanelToggleType
    {
        LockTranslateX,
        LockTranslateY,
        LockTranslateZ,
        LockRotateX,
        LockRotateY,
        LockRotateZ,
    }

    public enum TransformPanelActionType
    {
        AlignX,
        AlignY,
        AlignZ,
        DistributeX,
        DistributeY,
        DistributeZ,
    }

    public enum BoundsTypes
    {
        Min,
        Center,
        Max,
        Gaps,
    }

    public class TransformPanel : BasePanel
    {
        private BoundsTypes m_AlignBoundsType = BoundsTypes.Center;
        private BoundsTypes m_DistributeBoundsType = BoundsTypes.Center;

        public void HandleToggle(TransformPanelToggleButton btn)
        {
            switch (btn.m_ButtonType)
            {
                case TransformPanelToggleType.LockRotateX:
                    SelectionManager.m_Instance.m_LockRotationX = btn.ToggleState;
                    break;
                case TransformPanelToggleType.LockRotateY:
                    SelectionManager.m_Instance.m_LockRotationY = btn.ToggleState;
                    break;
                case TransformPanelToggleType.LockRotateZ:
                    SelectionManager.m_Instance.m_LockRotationZ = btn.ToggleState;
                    break;

                case TransformPanelToggleType.LockTranslateX:
                    SelectionManager.m_Instance.m_LockTranslationX = btn.ToggleState;
                    break;
                case TransformPanelToggleType.LockTranslateY:
                    SelectionManager.m_Instance.m_LockTranslationY = btn.ToggleState;
                    break;
                case TransformPanelToggleType.LockTranslateZ:
                    SelectionManager.m_Instance.m_LockTranslationZ = btn.ToggleState;
                    break;
            }
        }

        public void HandleAction(TransformPanelActionButton btn)
        {
            switch (btn.m_ButtonType)
            {
                case TransformPanelActionType.AlignX:
                    Align(0);
                    break;
                case TransformPanelActionType.AlignY:
                    Align(1);
                    break;
                case TransformPanelActionType.AlignZ:
                    Align(2);
                    break;
                case TransformPanelActionType.DistributeX:
                    Distribute(0);
                    break;
                case TransformPanelActionType.DistributeY:
                    Distribute(1);
                    break;
                case TransformPanelActionType.DistributeZ:
                    Distribute(2);
                    break;
            }
        }

        public void HandleAlignStateButton(int state)
        {
            m_AlignBoundsType = (BoundsTypes)state;
        }

        public void HandleDistributeStateButton(int state)
        {
            m_DistributeBoundsType = (BoundsTypes)state;
        }

        private void Align(int axis)
        {
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new AlignSelectedCommand(axis, m_AlignBoundsType)
            );
        }

        private void Distribute(int axis)
        {
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new DistributeSelectedCommand(axis, m_DistributeBoundsType)
            );
        }
    }
}
