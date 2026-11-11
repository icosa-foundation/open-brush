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

using System.Collections.Generic;

namespace TiltBrush.FrameAnimation
{
    /// Pure helper used by differential playback and its editor tests.
    public static class AnimationSetDiff
    {
        public static bool ShouldApplyFrame(int previouslyAppliedFrame, int nextFrame)
        {
            return previouslyAppliedFrame != nextFrame;
        }

        public static void GetChanges<T>(
            ISet<T> previousItems, ISet<T> nextItems, ICollection<T> itemsToHide,
            ICollection<T> itemsToShow)
        {
            foreach (T item in previousItems)
            {
                if (!nextItems.Contains(item)) itemsToHide.Add(item);
            }
            foreach (T item in nextItems)
            {
                if (!previousItems.Contains(item)) itemsToShow.Add(item);
            }
        }
    }
}
