using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace IsoMesh.Editor
{
    [CustomEditor(typeof(SDFMeshAsset))]
    [CanEditMultipleObjects]
    public class SDFMeshAssetEditor : UnityEditor.Editor
    {
        private static class Labels
        {
            public static GUIContent SourceMesh = new GUIContent("Source Mesh", "The mesh asset which was used to generate this SDF.");
            public static GUIContent TessellationLevel = new GUIContent("Tessellation Level", "How many times each polygon of the source mesh was split. The resulting tesselated vertices are positioned according to the positions -and normals- of the original geometry, which produces smoother geometry.");
            public static GUIContent HasUVs = new GUIContent("Has UVs", "Whether this asset includes UV information.");
            public static GUIContent Size = new GUIContent("Size", "The number of samples along each side of the cubic sample space.");
            public static GUIContent Padding = new GUIContent("Padding", "The amount of extra space around the outer bounds of the mesh.");
            public static GUIContent MinBounds = new GUIContent("Min Bounds", "The near bottom left corner of the mesh's bounding box.");
            public static GUIContent MaxBounds = new GUIContent("Max Bounds", "The far top right corner of the mesh's bounding box.");
        }

        private class SerializedProperties
        {
            public SerializedProperty TessellationLevel { get; }
            public SerializedProperty Size { get; }
            public SerializedProperty Padding { get; }
            public SerializedProperty MinBounds { get; }
            public SerializedProperty MaxBounds { get; }

            public SerializedProperties(SerializedObject serializedObject)
            {
                TessellationLevel = serializedObject.FindProperty("m_tessellationLevel");
                Size = serializedObject.FindProperty("m_size");
                Padding = serializedObject.FindProperty("m_padding");
                MinBounds = serializedObject.FindProperty("m_minBounds");
                MaxBounds = serializedObject.FindProperty("m_maxBounds");
            }
        }

        private UnityEditor.Editor m_sourceMeshPreview;
        private SDFMeshAsset m_asset;
        private SerializedProperties m_properties;

        private void OnEnable()
        {
            m_asset = (SDFMeshAsset)target;

            m_sourceMeshPreview = UnityEditor.Editor.CreateEditor(m_asset.SourceMesh);
            m_properties = new SerializedProperties(serializedObject);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.DrawScript();

            try
            {
                m_sourceMeshPreview.DrawPreview(GUILayoutUtility.GetRect(200, 200));
            }
            catch (Exception e)
            {
                Debug.LogWarning(e);
            }

            GUI.enabled = false;

            if (m_asset.IsTessellated)
                EditorGUILayout.PropertyField(m_properties.TessellationLevel, Labels.TessellationLevel);

            EditorGUILayout.Toggle(Labels.HasUVs, m_asset.HasUVs);
            EditorGUILayout.PropertyField(m_properties.Size, Labels.Size);
            EditorGUILayout.PropertyField(m_properties.Padding, Labels.Padding);
            EditorGUILayout.PropertyField(m_properties.MinBounds, Labels.MinBounds);
            EditorGUILayout.PropertyField(m_properties.MaxBounds, Labels.MaxBounds);

            GUI.enabled = true;
        }
    }
}