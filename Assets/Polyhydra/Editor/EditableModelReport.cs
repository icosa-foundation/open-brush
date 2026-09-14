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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TiltBrush
{

    class EditableModelReport : EditorWindow
    {
        [SerializeField] private int m_SelectedIndex = -1;
        private ListView m_LeftPane;
        private VisualElement m_RightPane;
        private EditableModelWidget[] m_AllWidgets;

        [MenuItem("Open Brush/Editable Model Report")]
        public static void ShowEditor()
        {
            // This method is called when the user selects the menu item in the Editor
            EditorWindow wnd = GetWindow<EditableModelReport>();
            wnd.titleContent = new GUIContent("Editable Model Report");

            // Limit size of the window
            wnd.minSize = new Vector2(450, 200);
            wnd.maxSize = new Vector2(1920, 720);
        }

        public void Update()
        {
            DoUpdate();
        }

        public void CreateGUI()
        {
            TwoPaneSplitView splitView = new TwoPaneSplitView(0, 150, TwoPaneSplitViewOrientation.Horizontal);

            // var btn = new Button(DoUpdate);
            // btn.Add(new Label("Refresh"));
            // rootVisualElement.Add(btn);

            rootVisualElement.Add(splitView);
            m_LeftPane = new ListView();
            splitView.Add(m_LeftPane);
            m_RightPane = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            splitView.Add(m_RightPane);
            m_LeftPane.onSelectionChange += OnWidgetSelectionChange;
            // Initialize the list view
            m_LeftPane.makeItem = () => new Label();
            m_LeftPane.bindItem = (item, index) =>
            {
                var widget = m_AllWidgets[index];
                ((Label)item).text = $"{index}: {widget.m_PolyRecipe.GeneratorType} + {widget.m_PolyRecipe.Operators.Count} ops"; ;
            };
            m_SelectedIndex = 0;
            DoUpdate();
        }

        public void DoUpdate()
        {
            if (!Application.isPlaying) return;
            m_LeftPane.selectedIndex = m_SelectedIndex;
            m_LeftPane.onSelectionChange += (items) => { m_SelectedIndex = m_LeftPane.selectedIndex; };
            m_AllWidgets = FindObjectsOfType<EditableModelWidget>();
            m_LeftPane.itemsSource = m_AllWidgets;
            m_LeftPane.RefreshItems();
        }

        private void OnWidgetSelectionChange(IEnumerable<object> selectedItems)
        {
            m_RightPane.Clear();
            var widget = selectedItems.First() as EditableModelWidget;
            if (widget == null) return;

            EditableModelDefinition emDef = new EditableModelDefinition(widget.m_PolyRecipe);
            var jsonSerializer = new JsonSerializer { ContractResolver = new CustomJsonContractResolver() };
            using var stringWriter = new StringWriter();
            using var jsonWriter = new CustomJsonWriter(stringWriter);
            jsonSerializer.Serialize(jsonWriter, emDef);
            m_RightPane.Add(new Label($"{m_SelectedIndex} {widget.m_PolyRecipe.GeneratorType} + {widget.m_PolyRecipe.Operators.Count} ops"));
            m_RightPane.Add(new Label(""));
            m_RightPane.Add(new Label($"{stringWriter}"));
        }
    }
}
