// Copyright 2023 The Open Brush Authors
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
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    enum ItemType
    {
        Image,
        Model,
        Video,
        CameraPath,
        Stencil
    }

    static class Utils
    {
        private static int _NegativeIndexing<T>(int index, IEnumerable<T> enumerable)
        {
            // Python style: negative numbers count from the end
            int count = enumerable.Count();
            if (index < 0) index = count - Mathf.Abs(index);
            return index;
        }

        public static void _Transform(ItemType type, int index, TrTransform tr)
        {
            void _Action(GrabWidget widget)
            {
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                    new MoveWidgetCommand(
                        widget,
                        tr,
                        widget.CustomDimension,
                        true
                    )
                );
            }

            switch (type)
            {
                case ItemType.Image:
                    {
                        var widgets = WidgetManager.m_Instance.ActiveImageWidgets;
                        _Action(widgets[_NegativeIndexing(index, widgets)].WidgetScript);
                        break;
                    }
                case ItemType.Model:
                    {
                        var widgets = WidgetManager.m_Instance.ActiveImageWidgets;
                        _Action(widgets[_NegativeIndexing(index, widgets)].WidgetScript);
                        break;
                    }
                case ItemType.Video:
                    {
                        var widgets = WidgetManager.m_Instance.ActiveImageWidgets;
                        _Action(widgets[_NegativeIndexing(index, widgets)].WidgetScript);
                        break;
                    }
                case ItemType.CameraPath:
                    {
                        var widgets = WidgetManager.m_Instance.ActiveImageWidgets;
                        _Action(widgets[_NegativeIndexing(index, widgets)].WidgetScript);
                        break;
                    }
                case ItemType.Stencil:
                    {
                        var widgets = WidgetManager.m_Instance.ActiveImageWidgets;
                        _Action(widgets[_NegativeIndexing(index, widgets)].WidgetScript);
                        break;
                    }
            }
        }
    }

}
