using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace IsoMesh.Editor
{
    public static class SDFEditorUtils
    {
        public static void DrawScript(this SerializedObject obj)
        {
            GUI.enabled = false;
            EditorGUILayout.PropertyField(obj.FindProperty("m_Script"));
            GUI.enabled = true;
        }

        public static bool DrawEnumField<T>(this Object obj, string label, SerializedProperty property, out T newVal) where T : System.Enum =>
            DrawEnumField(obj, new GUIContent(label), property, out newVal);

        public static bool DrawEnumField<T>(this Object obj, GUIContent label, SerializedProperty property, out T newVal) where T : System.Enum
        {
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                newVal = (T)EditorGUILayout.EnumPopup(label, (T)(object)property.enumValueIndex);

                if (check.changed)
                {
                    Undo.RecordObject(obj, "Changed " + label.text);
                    property.enumValueIndex = (int)(object)newVal;
                    return true;
                }
            }

            return false;
        }

        public static bool DrawColourField(this Object obj, string label, SerializedProperty property, out Color newVal) =>
            DrawColourField(obj, new GUIContent(label), property, out newVal);

        public static bool DrawColourField(this Object obj, GUIContent label, SerializedProperty property, out Color newVal)
        {
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                newVal = EditorGUILayout.ColorField(label, property.colorValue);

                if (check.changed)
                {
                    Undo.RecordObject(obj, "Changed " + label.text);
                    property.colorValue = newVal;
                    return true;
                }
            }

            return false;
        }

        public static bool DrawBoolField(this Object obj, string label, SerializedProperty property, out bool newVal) =>
            DrawBoolField(obj, new GUIContent(label), property, out newVal);

        public static bool DrawBoolField(this Object obj, GUIContent label, SerializedProperty property, out bool newVal)
        {
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                newVal = EditorGUILayout.Toggle(label, property.boolValue);

                if (check.changed)
                {
                    Undo.RecordObject(obj, "Changed " + label.text);
                    property.boolValue = newVal;
                    return true;
                }
            }

            return false;
        }

        public static bool DrawObjectField<T>(this Object obj, string label, SerializedProperty property, out T newVal, bool allowSceneObjects = false) where T : UnityEngine.Object =>
            DrawObjectField<T>(obj, new GUIContent(label), property, out newVal, allowSceneObjects);

        public static bool DrawObjectField<T>(this Object obj, GUIContent label, SerializedProperty property, out T newVal, bool allowSceneObjects = false) where T : UnityEngine.Object
        {
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                newVal = (T)EditorGUILayout.ObjectField(label, property.objectReferenceValue, typeof(T), allowSceneObjects);

                if (check.changed)
                {
                    Undo.RecordObject(obj, "Changed " + label.text);
                    property.objectReferenceValue = newVal;
                    return true;
                }
            }

            return false;
        }

        public static bool DrawIntField(this Object obj, string label, SerializedProperty property, out int newVal, int? min = null, int? max = null) =>
            DrawIntField(obj, new GUIContent(label), property, out newVal, min, max);

        public static bool DrawIntField(this Object obj, GUIContent label, SerializedProperty property, out int newVal, int? min = null, int? max = null)
        {
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                newVal = EditorGUILayout.IntField(label, property.intValue);

                if (min.HasValue)
                    newVal = Mathf.Max(newVal, min.Value);

                if (max.HasValue)
                    newVal = Mathf.Min(newVal, max.Value);

                if (check.changed)
                {
                    Undo.RecordObject(obj, "Changed " + label.text);
                    property.intValue = newVal;
                    return true;
                }
            }

            return false;
        }



        public static bool DrawFloatField(this Object obj, string label, SerializedProperty property, out float newVal, float? min = null, float? max = null) =>
            DrawFloatField(obj, new GUIContent(label), property, out newVal, min, max);

        public static bool DrawFloatField(this Object obj, GUIContent label, SerializedProperty property, out float newVal, float? min = null, float? max = null)
        {
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                newVal = EditorGUILayout.FloatField(label, property.floatValue);

                if (min.HasValue)
                    newVal = Mathf.Max(newVal, min.Value);

                if (max.HasValue)
                    newVal = Mathf.Min(newVal, max.Value);

                if (check.changed)
                {
                    Undo.RecordObject(obj, "Changed " + label.text);
                    property.floatValue = newVal;
                    return true;
                }
            }

            return false;
        }

        public static bool DrawVector2Field(this Object obj, string label, SerializedProperty property, out Vector2 newVal, float? min = null, float? max = null) =>
            DrawVector2Field(obj, new GUIContent(label), property, out newVal);

        public static bool DrawVector2Field(this Object obj, GUIContent label, SerializedProperty property, out Vector2 newVal, float? min = null, float? max = null)
        {
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                newVal = EditorGUILayout.Vector2Field(label, property.GetVectorProperty());

                if (min.HasValue)
                    newVal = newVal.Max(min.Value);

                if (max.HasValue)
                    newVal = newVal.Min(max.Value);

                if (check.changed)
                {
                    Undo.RecordObject(obj, "Changed " + label.text);
                    property.SetVectorProperty(newVal);
                    return true;
                }
            }

            return false;
        }

        public static bool DrawVector3Field(this Object obj, string label, SerializedProperty property, out Vector3 newVal, float? min = null, float? max = null) =>
            DrawVector3Field(obj, new GUIContent(label), property, out newVal);

        public static bool DrawVector3Field(this Object obj, GUIContent label, SerializedProperty property, out Vector3 newVal, float? min = null, float? max = null)
        {
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                newVal = EditorGUILayout.Vector3Field(label, property.GetVectorProperty());

                if (min.HasValue)
                    newVal = newVal.Max(min.Value);

                if (max.HasValue)
                    newVal = newVal.Min(max.Value);

                if (check.changed)
                {
                    Undo.RecordObject(obj, "Changed " + label.text);

                    Vector4 vec4OldVal = GetVectorProperty(property);
                    Vector4 vec4NewVal = newVal;

                    property.SetVectorProperty(vec4NewVal.SetW(vec4OldVal.w));
                    return true;
                }
            }

            return false;
        }

        public static bool DrawVector4Field(this Object obj, string label, SerializedProperty property, out Vector4 newVal, float? min = null, float? max = null) =>
            DrawVector4Field(obj, new GUIContent(label), property, out newVal);

        public static bool DrawVector4Field(this Object obj, GUIContent label, SerializedProperty property, out Vector4 newVal, float? min = null, float? max = null)
        {
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                newVal = EditorGUILayout.Vector4Field(label, property.GetVectorProperty());

                if (min.HasValue)
                    newVal = newVal.Max(min.Value);

                if (max.HasValue)
                    newVal = newVal.Min(max.Value);

                if (check.changed)
                {
                    Undo.RecordObject(obj, "Changed " + label.text);
                    property.SetVectorProperty(newVal);
                    return true;
                }
            }

            return false;
        }

        public static bool DrawVectorFieldX(this Object obj, GUIContent label, SerializedProperty property, out float newVal, float? min = null, float? max = null)
        {
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                Vector4 vec = property.GetVectorProperty();
                newVal = EditorGUILayout.FloatField(label, vec.x);

                if (min.HasValue)
                    newVal = Mathf.Max(newVal, min.Value);

                if (max.HasValue)
                    newVal = Mathf.Min(newVal, max.Value);

                vec = new Vector4(newVal, vec.y, vec.z, vec.w);

                if (check.changed)
                {
                    Undo.RecordObject(obj, "Changed " + label.text);
                    property.SetVectorProperty(vec);
                    return true;
                }
            }

            return false;
        }

        public static bool DrawVectorFieldY(this Object obj, GUIContent label, SerializedProperty property, out float newVal, float? min = null, float? max = null)
        {
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                Vector4 vec = property.GetVectorProperty();
                newVal = EditorGUILayout.FloatField(label, vec.y);

                if (min.HasValue)
                    newVal = Mathf.Max(newVal, min.Value);

                if (max.HasValue)
                    newVal = Mathf.Min(newVal, max.Value);

                vec = new Vector4(vec.x, newVal, vec.z, vec.w);

                if (check.changed)
                {
                    Undo.RecordObject(obj, "Changed " + label.text);
                    property.SetVectorProperty(vec);
                    return true;
                }
            }

            return false;
        }

        public static bool DrawVectorFieldZ(this Object obj, GUIContent label, SerializedProperty property, out float newVal, float? min = null, float? max = null)
        {
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                Vector4 vec = property.GetVectorProperty();
                newVal = EditorGUILayout.FloatField(label, vec.z);

                if (min.HasValue)
                    newVal = Mathf.Max(newVal, min.Value);

                if (max.HasValue)
                    newVal = Mathf.Min(newVal, max.Value);

                vec = new Vector4(vec.x, vec.y, newVal, vec.w);

                if (check.changed)
                {
                    Undo.RecordObject(obj, "Changed " + label.text);
                    property.SetVectorProperty(vec);
                    return true;
                }
            }

            return false;
        }

        public static bool DrawVectorFieldW(this Object obj, GUIContent label, SerializedProperty property, out float newVal, float? min = null, float? max = null)
        {
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                Vector4 vec = property.GetVectorProperty();
                newVal = EditorGUILayout.FloatField(label, vec.w);

                if (min.HasValue)
                    newVal = Mathf.Max(newVal, min.Value);

                if (max.HasValue)
                    newVal = Mathf.Min(newVal, max.Value);

                vec = new Vector4(vec.x, vec.y, vec.z, newVal);

                if (check.changed)
                {
                    Undo.RecordObject(obj, "Changed " + label.text);
                    property.SetVectorProperty(vec);
                    return true;
                }
            }

            return false;
        }


        public static bool DrawVectorFieldXInt(this Object obj, GUIContent label, SerializedProperty property, out int newVal, int? min = null, int? max = null)
        {
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                Vector4 vec = property.GetVectorProperty();
                newVal = EditorGUILayout.IntField(label, (int)vec.x);

                if (min.HasValue)
                    newVal = Mathf.Max(newVal, min.Value);

                if (max.HasValue)
                    newVal = Mathf.Min(newVal, max.Value);

                vec = new Vector4(newVal, vec.y, vec.z, vec.w);

                if (check.changed)
                {
                    Undo.RecordObject(obj, "Changed " + label.text);
                    property.SetVectorProperty(vec);
                    return true;
                }
            }

            return false;
        }

        public static bool DrawVectorFieldYInt(this Object obj, GUIContent label, SerializedProperty property, out int newVal, int? min = null, int? max = null)
        {
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                Vector4 vec = property.GetVectorProperty();
                newVal = EditorGUILayout.IntField(label, (int)vec.y);

                if (min.HasValue)
                    newVal = Mathf.Max(newVal, min.Value);

                if (max.HasValue)
                    newVal = Mathf.Min(newVal, max.Value);

                vec = new Vector4(vec.x, newVal, vec.z, vec.w);

                if (check.changed)
                {
                    Undo.RecordObject(obj, "Changed " + label.text);
                    property.SetVectorProperty(vec);
                    return true;
                }
            }

            return false;
        }

        public static bool DrawVectorFieldZInt(this Object obj, GUIContent label, SerializedProperty property, out int newVal, int? min = null, int? max = null)
        {
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                Vector4 vec = property.GetVectorProperty();
                newVal = EditorGUILayout.IntField(label, (int)vec.z);

                if (min.HasValue)
                    newVal = Mathf.Max(newVal, min.Value);

                if (max.HasValue)
                    newVal = Mathf.Min(newVal, max.Value);

                vec = new Vector4(vec.x, vec.y, newVal, vec.w);

                if (check.changed)
                {
                    Undo.RecordObject(obj, "Changed " + label.text);
                    property.SetVectorProperty(vec);
                    return true;
                }
            }

            return false;
        }

        public static bool DrawVectorFieldWInt(this Object obj, GUIContent label, SerializedProperty property, out int newVal, int? min = null, int? max = null)
        {
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                Vector4 vec = property.GetVectorProperty();
                newVal = EditorGUILayout.IntField(label, (int)vec.w);

                if (min.HasValue)
                    newVal = Mathf.Max(newVal, min.Value);

                if (max.HasValue)
                    newVal = Mathf.Min(newVal, max.Value);

                vec = new Vector4(vec.x, vec.y, vec.z, newVal);

                if (check.changed)
                {
                    Undo.RecordObject(obj, "Changed " + label.text);
                    property.SetVectorProperty(vec);
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// This allows you get any float vector property. It will always return a vector4 but you can determine which type it is by calling serializedProperty.propertyType.
        /// </summary>
        public static Vector4 GetVectorProperty(this SerializedProperty serializedProperty)
        {
            switch (serializedProperty.propertyType)
            {
                case SerializedPropertyType.Vector2:
                    return serializedProperty.vector2Value;
                case SerializedPropertyType.Vector3:
                    return serializedProperty.vector3Value;
                case SerializedPropertyType.Vector4:
                    return serializedProperty.vector4Value;
            }

            Debug.LogError("SerializedProperty is not a float vector. Type is " + serializedProperty.propertyType);
            return Vector4.zero;
        }

        /// <summary>
        /// This allows you set any float vector property.
        /// </summary>
        public static void SetVectorProperty(this SerializedProperty serializedProperty, Vector4 val)
        {
            switch (serializedProperty.propertyType)
            {
                case SerializedPropertyType.Vector2:
                    serializedProperty.vector2Value = val;
                    return;
                case SerializedPropertyType.Vector3:
                    serializedProperty.vector3Value = val;
                    return;
                case SerializedPropertyType.Vector4:
                    serializedProperty.vector4Value = val;
                    return;
            }

            Debug.LogError("SerializedProperty is not a float vector. Type is " + serializedProperty.propertyType);
        }
    }
}