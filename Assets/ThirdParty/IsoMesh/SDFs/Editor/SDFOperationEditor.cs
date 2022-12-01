using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace IsoMesh.Editor
{
    [CustomEditor(typeof(SDFOperation))]
    [CanEditMultipleObjects]
    public class SDFOperationEditor : UnityEditor.Editor
    {
        private static class Labels
        {
            public static GUIContent Type = new GUIContent("Type", "Which operation this object represents.");
            public static GUIContent Elongation = new GUIContent("Elongation", "How much this operation stretches space along each local axis.");
            public static GUIContent Rounding = new GUIContent("Rounding", "The amount of rounding this operation applies.");
            public static GUIContent Thickness = new GUIContent("Thickness", "The thickness of each layer.");
            public static GUIContent Layers = new GUIContent("Layers", "The amount of layers.");
        }

        private class SerializedProperties
        {
            public SerializedProperty Type { get; }
            public SerializedProperty Data { get; }

            public SerializedProperties(SerializedObject serializedObject)
            {
                Type = serializedObject.FindProperty("m_type");
                Data = serializedObject.FindProperty("m_data");
            }
        }
        
        private SerializedProperties m_serializedProperties;
        private SDFOperation m_sdfOperation;
        private SerializedPropertySetter m_setter;

        private void OnEnable()
        {
            m_serializedProperties = new SerializedProperties(serializedObject);
            m_sdfOperation = target as SDFOperation;
            m_setter = new SerializedPropertySetter(serializedObject);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.DrawScript();
            m_setter.Clear();

            using (EditorGUILayout.VerticalScope box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                switch (m_sdfOperation.Type)
                {
                    case SDFOperationType.Elongate:
                        m_setter.DrawVector3Setting(Labels.Elongation, m_serializedProperties.Data, min: 0f);
                        break;
                    case SDFOperationType.Onion:
                        m_setter.DrawVectorSettingX(Labels.Rounding, m_serializedProperties.Data, min: 0f);
                        m_setter.DrawVectorSettingYInt(Labels.Layers, m_serializedProperties.Data, min: 0);
                        break;
                    case SDFOperationType.Round:
                        m_setter.DrawVectorSettingX(Labels.Rounding, m_serializedProperties.Data, min: 0f);
                        break;
                }
            }

            m_setter.Update();
        }

        private void OnSceneGUI()
        {
            Color col = Color.black;
            Vector4 data = m_serializedProperties.Data.vector4Value;
            Handles.color = col;
            Handles.matrix = m_sdfOperation.transform.localToWorldMatrix;

            const float lineThickness = 2.5f;

            switch (m_sdfOperation.Type)
            {
                case SDFOperationType.Elongate:

                    float xElongation = m_sdfOperation.Data.x;
                    float yElongation = m_sdfOperation.Data.y;
                    float zElongation = m_sdfOperation.Data.z;

                    if (xElongation > 0f)
                    {
                        Handles.color = Color.red;
                        Handles.DrawAAPolyLine(lineThickness, new Vector3(-xElongation, -1f, -1f), new Vector3(-xElongation, 1f, -1f), new Vector3(-xElongation, 1f, 1f), new Vector3(-xElongation, -1f, 1f), new Vector3(-xElongation, -1f, -1f));
                        Handles.DrawAAPolyLine(lineThickness, new Vector3(xElongation, -1f, -1f), new Vector3(xElongation, 1f, -1f), new Vector3(xElongation, 1f, 1f), new Vector3(xElongation, -1f, 1f), new Vector3(xElongation, -1f, -1f));
                        Handles.DrawAAPolyLine(lineThickness, Vector3.right * -xElongation, Vector3.right * xElongation);
                    }

                    if (yElongation > 0f)
                    {
                        Handles.color = Color.green;
                        Handles.DrawAAPolyLine(lineThickness, new Vector3(-1f, -yElongation, -1f), new Vector3(1f, -yElongation, -1f), new Vector3(1f, -yElongation, 1f), new Vector3(-1f, -yElongation, 1f), new Vector3(-1f, -yElongation, -1f));
                        Handles.DrawAAPolyLine(lineThickness, new Vector3(-1f, yElongation, -1f), new Vector3(1f, yElongation, -1f), new Vector3(1f, yElongation, 1f), new Vector3(-1f, yElongation, 1f), new Vector3(-1f, yElongation, -1f));
                        Handles.DrawAAPolyLine(lineThickness, Vector3.up * -yElongation, Vector3.up * yElongation);
                    }

                    if (zElongation > 0f)
                    {
                        Handles.color = Color.blue;
                        Handles.DrawAAPolyLine(lineThickness, new Vector3(-1f, -1f, -zElongation), new Vector3(1f, -1f, -zElongation), new Vector3(1f, 1f, -zElongation), new Vector3(-1f, 1f, -zElongation), new Vector3(-1f, -1f, -zElongation));
                        Handles.DrawAAPolyLine(lineThickness, new Vector3(-1f, -1f, zElongation), new Vector3(1f, -1f, zElongation), new Vector3(1f, 1f, zElongation), new Vector3(-1f, 1f, zElongation), new Vector3(-1f, -1f, zElongation));
                        Handles.DrawAAPolyLine(lineThickness, Vector3.forward * -zElongation, Vector3.forward * zElongation);
                    }

                    break;
            }
        }
    }
}