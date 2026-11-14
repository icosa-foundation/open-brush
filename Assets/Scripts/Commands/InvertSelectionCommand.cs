// Copyright 2020 The Tilt Brush Authors
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

namespace TiltBrush
{

    /// A command to toggle selection on a set of strokes and widgets.
    ///
    /// Selection children retain the same peer movement and undo behavior as ordinary selection.
    public class InvertSelectionCommand : BaseCommand
    {
        public InvertSelectionCommand(
            ICollection<Stroke> strokesOn,
            ICollection<Stroke> strokesOff,
            ICollection<GrabWidget> widgetsOn,
            ICollection<GrabWidget> widgetsOff,
            BaseCommand parent = null) : base(parent)
        {
            var transform = SelectionManager.m_Instance.SelectionTransform;
            // Capture the incoming selection while the outgoing linked groups still have drivers,
            // so inversion excludes their peers instead of selecting another copy of the same group.
            new SelectCommand(strokesOn, widgetsOn, transform, parent: this);
            // Ordinary deselection captures and commits peer movement, with matching undo endpoints.
            new SelectCommand(strokesOff, widgetsOff, transform, deselect: true, parent: this);
        }

        public override bool Merge(BaseCommand other)
        {
            return false;
        }
    }
} // namespace TiltBrush
