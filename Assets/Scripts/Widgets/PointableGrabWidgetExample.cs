// Copyright 2024 The Open Brush Authors
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

using UnityEngine;

namespace TiltBrush
{
    /// Example subclass showing how to extend PointableGrabWidget.
    /// Delete or replace this with your actual widget implementation.
    public class PointableGrabWidgetExample : PointableGrabWidget
    {
        override protected void Awake()
        {
            base.Awake();
            // Widget-specific init here.
        }

        override protected void OnShow()
        {
            base.OnShow();
            // Called when the widget transitions to visible.
        }

        override protected void OnHide()
        {
            base.OnHide();
            // Called when the widget transitions to invisible.
        }

        override protected void OnUpdate()
        {
            base.OnUpdate(); // Must call — drives pointing and scale animation.
            // Per-frame widget logic here.
        }

        override protected void OnUserBeginInteracting()
        {
            base.OnUserBeginInteracting(); // Must call — suppresses pointing during grab.
            // Grab-start logic here.
        }

        override protected void OnUserEndInteracting()
        {
            base.OnUserEndInteracting(); // Must call — re-enables pointing after grab.
            // Grab-end logic here.
        }
    }
} // namespace TiltBrush
