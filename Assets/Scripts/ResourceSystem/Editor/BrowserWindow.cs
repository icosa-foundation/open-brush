using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TiltBrush
{
    public class BrowserWindow : EditorWindow
    {
        private ScrollView m_Feeds;
        private ScrollView m_Items;

        [MenuItem("Tools/Resources Browser")]
        public static void ShowMyEditor()
        {
            // This method is called when the user selects the menu item in the Editor
            EditorWindow wnd = GetWindow<BrowserWindow>();
            wnd.titleContent = new GUIContent("Resources Browser");
        }

        public void CreateGUI()
        {
            rootVisualElement.Add(new Label("Testing"));
            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            rootVisualElement.Add(top);
            var main = new VisualElement();
            main.style.flexDirection = FlexDirection.Row;
            rootVisualElement.Add(main);
            m_Feeds = new ScrollView(ScrollViewMode.Vertical);
            main.Add(m_Feeds);
            m_Items = new ScrollView(ScrollViewMode.Horizontal);
            main.Add(m_Items);
        }

        public void Refresh()
        {

        }

    }
}
