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
    public class ModifyTextCommand : BaseCommand
    {
        private readonly TextWidget m_Widget;
        private readonly string m_StartText;
        private readonly string m_EndText;

        public ModifyTextCommand(TextWidget widget, string endText, BaseCommand parent = null)
            : base(parent)
        {
            m_Widget = widget;
            m_StartText = widget.Text;
            m_EndText = endText;
        }

        public override bool NeedsSave => true;
        public override bool IsAvailable => m_Widget != null;

        protected override void OnUndo()
        {
            m_Widget.Text = m_StartText;
        }

        protected override void OnRedo()
        {
            m_Widget.Text = m_EndText;
        }
    }
} // namespace TiltBrush
