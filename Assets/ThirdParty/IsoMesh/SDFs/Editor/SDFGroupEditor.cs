using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace IsoMesh.Editor
{
    [CustomEditor(typeof(SDFGroup))]
    [CanEditMultipleObjects]
    public class SDFGroupEditor : UnityEditor.Editor
    {
        private static class Labels
        {
            public static GUIContent IsRunning = new GUIContent("Is Running", "Whether this group is actively updating.");
            public static GUIContent Settings = new GUIContent("Settings", "Additional controls for the entire group.");
            public static GUIContent NormalSmoothing = new GUIContent("Normal Smoothing", "The sample size for determining the normals of the resulting combined SDF. Higher values produce smoother normals.");
            public static GUIContent ThicknessMaxDistance = new GUIContent("Thickness Max Distance", "The max distance of the raycast samples used to determine how thick the sdf is at any given point.");
            public static GUIContent ThicknessFalloff = new GUIContent("Thickness Falloff", "The strength of the thickness effect.");
        }

        private class SerializedProperties
        {
            public SerializedProperty IsRunning { get; }
            public SerializedProperty Smoothing { get; }
            public SerializedProperty NormalSmoothing { get; }
            public SerializedProperty ThicknessMaxDistance { get; }
            public SerializedProperty ThicknessFalloff { get; }

            public SerializedProperties(SerializedObject serializedObject)
            {
                IsRunning = serializedObject.FindProperty("m_isRunning");
                NormalSmoothing = serializedObject.FindProperty("m_normalSmoothing");
                ThicknessMaxDistance = serializedObject.FindProperty("m_thicknessMaxDistance");
                ThicknessFalloff = serializedObject.FindProperty("m_thicknessFalloff");
            }
        }

        private SerializedProperties m_serializedProperties;
        private SDFGroup m_sdfGroup;
        private SerializedPropertySetter m_setter;
        private bool m_isSettingsOpen = true;

        private void OnEnable()
        {
            m_serializedProperties = new SerializedProperties(serializedObject);
            m_sdfGroup = target as SDFGroup;
            m_setter = new SerializedPropertySetter(serializedObject);
        }

        public override void OnInspectorGUI()
        {
            m_setter.Clear();

            serializedObject.DrawScript();

            EditorGUILayout.PropertyField(m_serializedProperties.IsRunning, Labels.IsRunning);

            if (m_isSettingsOpen = EditorGUILayout.Foldout(m_isSettingsOpen, Labels.Settings, true))
            {
                using (EditorGUILayout.VerticalScope box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope())
                    {
                        m_setter.DrawFloatSetting(Labels.NormalSmoothing, m_serializedProperties.NormalSmoothing, min: SDFGroup.MIN_SMOOTHING, onValueChangedCallback: m_sdfGroup.OnSettingsChanged);
                        m_setter.DrawFloatSetting(Labels.ThicknessMaxDistance, m_serializedProperties.ThicknessMaxDistance, min: 0f, onValueChangedCallback: m_sdfGroup.OnSettingsChanged);
                        m_setter.DrawFloatSetting(Labels.ThicknessFalloff, m_serializedProperties.ThicknessFalloff, min: 0f, onValueChangedCallback: m_sdfGroup.OnSettingsChanged);
                    }
                }
            }

            m_setter.Update();
        }
    }
}