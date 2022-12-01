using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace IsoMesh.Editor
{
    /// <summary>
    /// This class is used by custom editors to draw serialized properties correctly, so that they undo.
    /// In your OnGUI() method, clear this at the start and then call update at the end.
    /// </summary>
    public class SerializedPropertySetter
    {
        private bool m_changed = false;
        private readonly Queue<System.Action> m_actionQueue = new Queue<System.Action>();
        private SerializedObject m_serializedObject;
        private Object m_object;

        public SerializedPropertySetter(SerializedObject serializedObject)
        {
            m_serializedObject = serializedObject;
            m_object = m_serializedObject.targetObject;
        }

        public void DrawProperty(GUIContent label, SerializedProperty property, System.Action onValueChangedCallback = null)
        {
            using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(property, label);

                if (check.changed)
                {
                    m_changed = true;

                    if (onValueChangedCallback != null)
                        m_actionQueue.Enqueue(onValueChangedCallback);
                }
            }
        }

        public void DrawVector2Setting(GUIContent label, SerializedProperty property, float? min = null, float? max = null, System.Action onValueChangedCallback = null)
        {
            if (m_object.DrawVector2Field(label, property, out _, min, max))
            {
                m_changed = true;

                if (onValueChangedCallback != null)
                    m_actionQueue.Enqueue(onValueChangedCallback);
            }
        }

        public void DrawVector3Setting(GUIContent label, SerializedProperty property, float? min = null, float? max = null, System.Action onValueChangedCallback = null)
        {
            if (m_object.DrawVector3Field(label, property, out _, min, max))
            {
                m_changed = true;

                if (onValueChangedCallback != null)
                    m_actionQueue.Enqueue(onValueChangedCallback);
            }
        }

        public void DrawVector4Setting(GUIContent label, SerializedProperty property, float? min = null, float? max = null, System.Action onValueChangedCallback = null)
        {
            if (m_object.DrawVector4Field(label, property, out _, min, max))
            {
                m_changed = true;

                if (onValueChangedCallback != null)
                    m_actionQueue.Enqueue(onValueChangedCallback);
            }
        }

        public void DrawVectorSettingX(GUIContent label, SerializedProperty property, float? min = null, float? max = null, System.Action onValueChangedCallback = null)
        {
            if (m_object.DrawVectorFieldX(label, property, out _, min, max))
            {
                m_changed = true;

                if (onValueChangedCallback != null)
                    m_actionQueue.Enqueue(onValueChangedCallback);
            }
        }

        public void DrawVectorSettingY(GUIContent label, SerializedProperty property, float? min = null, float? max = null, System.Action onValueChangedCallback = null)
        {
            if (m_object.DrawVectorFieldY(label, property, out _, min, max))
            {
                m_changed = true;

                if (onValueChangedCallback != null)
                    m_actionQueue.Enqueue(onValueChangedCallback);
            }
        }

        public void DrawVectorSettingZ(GUIContent label, SerializedProperty property, float? min = null, float? max = null, System.Action onValueChangedCallback = null)
        {
            if (m_object.DrawVectorFieldZ(label, property, out _, min, max))
            {
                m_changed = true;

                if (onValueChangedCallback != null)
                    m_actionQueue.Enqueue(onValueChangedCallback);
            }
        }

        public void DrawVectorSettingW(GUIContent label, SerializedProperty property, float? min = null, float? max = null, System.Action onValueChangedCallback = null)
        {
            if (m_object.DrawVectorFieldW(label, property, out _, min, max))
            {
                m_changed = true;

                if (onValueChangedCallback != null)
                    m_actionQueue.Enqueue(onValueChangedCallback);
            }
        }


        public void DrawVectorSettingXInt(GUIContent label, SerializedProperty property, int? min = null, int? max = null, System.Action onValueChangedCallback = null)
        {
            if (m_object.DrawVectorFieldXInt(label, property, out _, min, max))
            {
                m_changed = true;

                if (onValueChangedCallback != null)
                    m_actionQueue.Enqueue(onValueChangedCallback);
            }
        }

        public void DrawVectorSettingYInt(GUIContent label, SerializedProperty property, int? min = null, int? max = null, System.Action onValueChangedCallback = null)
        {
            if (m_object.DrawVectorFieldYInt(label, property, out _, min, max))
            {
                m_changed = true;

                if (onValueChangedCallback != null)
                    m_actionQueue.Enqueue(onValueChangedCallback);
            }
        }

        public void DrawVectorSettingZInt(GUIContent label, SerializedProperty property, int? min = null, int? max = null, System.Action onValueChangedCallback = null)
        {
            if (m_object.DrawVectorFieldZInt(label, property, out _, min, max))
            {
                m_changed = true;

                if (onValueChangedCallback != null)
                    m_actionQueue.Enqueue(onValueChangedCallback);
            }
        }

        public void DrawVectorSettingWInt(GUIContent label, SerializedProperty property, int? min = null, int? max = null, System.Action onValueChangedCallback = null)
        {
            if (m_object.DrawVectorFieldWInt(label, property, out _, min, max))
            {
                m_changed = true;

                if (onValueChangedCallback != null)
                    m_actionQueue.Enqueue(onValueChangedCallback);
            }
        }


        public void DrawColourSetting(GUIContent label, SerializedProperty property, System.Action onValueChangedCallback = null)
        {
            if (m_object.DrawColourField(label, property, out _))
            {
                m_changed = true;

                if (onValueChangedCallback != null)
                    m_actionQueue.Enqueue(onValueChangedCallback);
            }
        }

        public void DrawFloatSetting(GUIContent label, SerializedProperty property, float? min = null, float? max = null, System.Action onValueChangedCallback = null)
        {
            if (m_object.DrawFloatField(label, property, out _, min, max))
            {
                m_changed = true;

                if (onValueChangedCallback != null)
                    m_actionQueue.Enqueue(onValueChangedCallback);
            }
        }

        public void DrawIntSetting(GUIContent label, SerializedProperty property, int? min = null, int? max = null, System.Action onValueChangedCallback = null)
        {
            if (m_object.DrawIntField(label, property, out _, min, max))
            {
                m_changed = true;

                if (onValueChangedCallback != null)
                    m_actionQueue.Enqueue(onValueChangedCallback);
            }
        }

        public void DrawBoolSetting(GUIContent label, SerializedProperty property, System.Action onValueChangedCallback = null)
        {
            if (m_object.DrawBoolField(label, property, out _))
            {
                m_changed = true;

                if (onValueChangedCallback != null)
                    m_actionQueue.Enqueue(onValueChangedCallback);
            }
        }

        public void DrawEnumSetting<T>(GUIContent label, SerializedProperty property, System.Action onValueChangedCallback = null) where T : System.Enum
        {
            if (m_object.DrawEnumField<T>(label, property, out _))
            {
                m_changed = true;

                if (onValueChangedCallback != null)
                    m_actionQueue.Enqueue(onValueChangedCallback);
            }
        }

        public void SetChanged()
        {
            m_changed = true;
        }

        public void Clear()
        {
            m_changed = false;
            m_actionQueue.Clear();
        }

        public void Update()
        {
            if (!m_changed)
                return;

            m_serializedObject.ApplyModifiedProperties();

            while (m_actionQueue.Count > 0)
            {
                System.Action action = m_actionQueue.Dequeue();
                action?.Invoke();
            }

            Clear();
        }
    }
}