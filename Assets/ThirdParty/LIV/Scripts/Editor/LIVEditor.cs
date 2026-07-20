using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace LIV.SDK.Unity
{
    [CustomEditor(typeof(LIV))]
    public class LIVEditor : Editor
    {
        #region UI NAME REFERENCES

        private const string STAGE_PROPERTY = "_stage";
        private const string HMD_CAMERA_PROPERTY = "_HMDCamera";
        private const string MR_CAMERA_PREFAB_PROPERTY = "_MRCameraPrefab";
        private const string SPECTATOR_LAYER_MASK_PROPERTY = "_spectatorLayerMask";
        private const string PASSTHROUGH_LAYER_MASK_PROPERTY = "_passthroughLayerMask";
        private const string FIX_POST_EFFECTS_ALPHA_PROPERTY = "_fixPostEffectsAlpha";
        private const string EXCLUDE_BEHAVIOURS_PROPERTY = "_excludeBehaviours";
        private const string OVERRIDE_ALPHA_FROM_DEPTH_BUFFER = "_overrideAlphaFromDepthBuffer";

        #endregion

        #region CONTENT DATA

        private static GUIContent TRACKING_ID_CONTENT = new GUIContent(
            "Tracking ID",
            "Tracking ID that identifies your game to the LIV backend, allowing you to get usage analytics.");

        private static GUIContent TRACKING_SPACE_CONTENT = new GUIContent(
            "XR Origin",
            "The origin of tracked space.\n" +
            "This is the object that you use to move the player around.");

        private static GUIContent HMD_CAMERA_CONTENT = new GUIContent(
            "HMD Camera",
            "Set this to the camera used to render the HMD's point of view.");

        private static GUIContent MR_CAMERA_PREFAB_CONTENT = new GUIContent(
            "MR Camera Prefab",
            "Set custom camera prefab.");

        private static GUIContent SPECTATOR_LAYER_MASK_CONTENT = new GUIContent(
            "Spectator Layer Mask",
            "By default, we'll show everything on the spectator camera.\n" +
            "If you want to disable certain objects from showing, update this mask property.");

        private static GUIContent PASSTHROUGH_LAYER_MASK_CONTENT = new GUIContent(
            "Passthrough Layer Mask",
            "Render only certain layers when passthrough rendering is turned on.");

        private static GUIContent FIX_POST_EFFECTS_ALPHA_CONTENT = new GUIContent(
            "Fix Post Effect Alpha Channel",
            "Some post-effects corrupt the alpha channel.\n" +
            "This fix tries to recover it.");

        private static GUIContent OVERRIDE_ALPHA_FROM_DEPTH_BUFFER_CONTENT = new GUIContent(
            "Override Alpha From Depth Buffer",
            "Overrides alpha using depth buffer from opaque render pass, " +
            "enable only when opaque render pass has corrupted alpha channel.");

        #endregion

        #region URLS

        private const string TUTORIAL_URL = "https://www.youtube.com/watch?v=FSXfNXu0mT0&list=PL_411laEMx3IGPBf548ZpOoJyiVPLEE6u";
        private const string DOCUMENTATION_URL = "https://mrc-docs.liv.tv/intro";
        private const string DEVELOPER_PORTAL_URL = "https://dev.liv.tv/";
        private const string DISCORD_URL = "https://discord.gg/liv";
        private const string HELP_URL = "https://help.liv.tv/hc/en-us";

        #endregion

        #region STYLES

        private static GUIStyle SECTION_TITLE_STYLE
        {
            get
            {
                var style = new GUIStyle(EditorStyles.boldLabel);
                return style;
            }
        }

        private static GUIStyle ERROR_LABEL_STYLE
        {
            get
            {
                var style = new GUIStyle(EditorStyles.label);
                style.normal.textColor = ERROR_COLOR;
                return style;
            }
        }

        private static GUIStyle ERROR_DESC_STYLE
        {
            get
            {
                var style = new GUIStyle(EditorStyles.miniLabel);
                return style;
            }
        }

        private static GUIStyle LINK_STYLE
        {
            get
            {
                var style = new GUIStyle(EditorStyles.label);
                style.alignment = TextAnchor.MiddleLeft;
                style.normal.textColor = Color.white;
                style.wordWrap = false;
                return style;
            }
        }

        private static GUIStyle VERSION_STYLE {
            get {
                GUIStyle style = new GUIStyle(EditorStyles.label);
                style.alignment = TextAnchor.MiddleLeft;
                style.normal.textColor = VERSION_COLOR;
                return style;
            }
        }

        #endregion

        #region COLORS

        static Color ERROR_COLOR = new Color(1f, 0.3058824f, 0.3058824f, 1f);
        static Color SEPARATOR_COLOR = new Color(0.0f, 0.0f, 0.0f, 0.2f);
        static Color VERSION_COLOR
        {
            get
            {
                return EditorStyles.label.normal.textColor;
            }
        }

        #endregion

        #region MARGINS AND SIZES

        private const float BASE_MARGIN = 2f;

        #endregion

        #region REORDERABLE LIST

        private ReorderableList _excludeBehavioursList;
        private const string EXCLUDE_BEHAVIOURS_FOLDOUT_KEY = "liv_excludeBehavioursfoldout";
        private static bool excludeBehavioursFoldoutValue {
            get {
                if (!EditorPrefs.HasKey(EXCLUDE_BEHAVIOURS_FOLDOUT_KEY)) return false;
                return EditorPrefs.GetBool(EXCLUDE_BEHAVIOURS_FOLDOUT_KEY);
            }
            set {
                EditorPrefs.SetBool(EXCLUDE_BEHAVIOURS_FOLDOUT_KEY, value);
            }
        }

        #endregion

        #region PROPERTIES

        private SerializedProperty _stageProperty;
        private SerializedProperty _hmdCameraProperty;
        private SerializedProperty _mrCameraPrefabProperty;
        private SerializedProperty _spectatorLayerMaskProperty;
        private SerializedProperty _passthroughLayerMaskProperty;
        private SerializedProperty _excludeBehavioursProperty;
        private SerializedProperty _fixPostEffectsAlphaProperty;
        private SerializedProperty _overrideAlphaFromDepthBuffer;

        #endregion

        #region ICONS

        private Texture2D _logo;
        private Texture2D _tutorial;
        private Texture2D _documentation;
        private Texture2D _discord;
        private Texture2D _info;
        private Texture2D _devPortal;
        private Texture2D _componentLogo;

        #endregion

        #region UNITY METHODS

        private void OnEnable()
        {
            GetTexturesForIcons();

            #if UNITY_2021_2_OR_NEWER
            if (_componentLogo != null)
            {
                MonoScript script = MonoScript.FromMonoBehaviour((MonoBehaviour)target);
                EditorGUIUtility.SetIconForObject(script, _componentLogo);
            }
            #endif

            _stageProperty = serializedObject.FindProperty(STAGE_PROPERTY);
            _hmdCameraProperty = serializedObject.FindProperty(HMD_CAMERA_PROPERTY);
            _mrCameraPrefabProperty = serializedObject.FindProperty(MR_CAMERA_PREFAB_PROPERTY);
            _spectatorLayerMaskProperty = serializedObject.FindProperty(SPECTATOR_LAYER_MASK_PROPERTY);
            _passthroughLayerMaskProperty = serializedObject.FindProperty(PASSTHROUGH_LAYER_MASK_PROPERTY);
            _excludeBehavioursProperty = serializedObject.FindProperty(EXCLUDE_BEHAVIOURS_PROPERTY);
            _fixPostEffectsAlphaProperty = serializedObject.FindProperty(FIX_POST_EFFECTS_ALPHA_PROPERTY);
            _overrideAlphaFromDepthBuffer = serializedObject.FindProperty(OVERRIDE_ALPHA_FROM_DEPTH_BUFFER);

            _excludeBehavioursList = new ReorderableList(serializedObject, _excludeBehavioursProperty, true, false, true, true);
            _excludeBehavioursList.drawElementCallback = DrawListItems;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawUI();
            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #region DRAWING UI METHODS

        private void DrawUI()
        {
            DrawSectionTitle("Required");
            DrawGap();
            DrawTrackingID();
            DrawGap(4);
            DrawTrackedSpace();
            DrawHMDField();
            DrawGap();
            DrawLineSeparator();
            DrawSectionTitle("Optional");
            DrawGap();
            DrawProperty(_mrCameraPrefabProperty, MR_CAMERA_PREFAB_CONTENT, EditorStyles.label);
            DrawSpectatorLayerMaskField();
            DrawPassthroughLayerMaskField();
            DrawProperty(_fixPostEffectsAlphaProperty, FIX_POST_EFFECTS_ALPHA_CONTENT, EditorStyles.label);
            DrawProperty(_overrideAlphaFromDepthBuffer, OVERRIDE_ALPHA_FROM_DEPTH_BUFFER_CONTENT, EditorStyles.label);
            DrawGap();
            DrawLineSeparator();
            DrawExcludeBehaviours();
            DrawGap();
            DrawLineSeparator();
            DrawFooter();
        }

        private void DrawSectionTitle(string titleName)
        {
             EditorGUILayout.LabelField(titleName, SECTION_TITLE_STYLE, GUILayout.ExpandWidth(false));
        }

        private void DrawGap(int factor = 1)
        {
            GUILayout.Space(BASE_MARGIN * factor);
        }

        void DrawTrackingID()
        {
            string trackingID = SDKSettings.instance.trackingID;
            bool isTrackingIDMissing = string.IsNullOrEmpty(trackingID);

            EditorGUILayout.BeginHorizontal();

            string valueText = isTrackingIDMissing ? "None" : trackingID;
            GUIStyle fieldStyle = isTrackingIDMissing ? ERROR_LABEL_STYLE : EditorStyles.label;
            EditorGUILayout.LabelField(TRACKING_ID_CONTENT, fieldStyle, GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField(valueText, EditorStyles.label);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("", GUILayout.ExpandWidth(false));

            if (GUILayout.Button("Open Settings", GUILayout.ExpandWidth(true)))
            {
                Selection.activeObject = SDKSettings.instance;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTrackedSpace()
        {
            GUIStyle labelStyle = _stageProperty.objectReferenceValue ? EditorStyles.label : ERROR_LABEL_STYLE;
            DrawProperty(_stageProperty, TRACKING_SPACE_CONTENT, labelStyle);
        }

        private void DrawHMDField()
        {
            GUIStyle labelStyle = _hmdCameraProperty.objectReferenceValue ? EditorStyles.label : ERROR_LABEL_STYLE;
            DrawProperty(_hmdCameraProperty, HMD_CAMERA_CONTENT, labelStyle);
        }

        private void DrawSpectatorLayerMaskField()
        {
            bool isEmpty = _spectatorLayerMaskProperty.intValue == 0;
            DrawProperty(_spectatorLayerMaskProperty, SPECTATOR_LAYER_MASK_CONTENT, isEmpty ? ERROR_LABEL_STYLE : EditorStyles.label);
            if (isEmpty)
                RenderFieldError("Nothing will be rendered.");
        }

        private void DrawPassthroughLayerMaskField()
        {
            bool isEmpty = _passthroughLayerMaskProperty.intValue == 0;
            DrawProperty(_passthroughLayerMaskProperty, PASSTHROUGH_LAYER_MASK_CONTENT, isEmpty ? ERROR_LABEL_STYLE : EditorStyles.label);
            if (isEmpty)
                RenderFieldError("Nothing will be rendered.");
        }

        private void DrawExcludeBehaviours()
        {
            EditorGUI.indentLevel++;
            excludeBehavioursFoldoutValue = EditorGUILayout.Foldout(excludeBehavioursFoldoutValue,
                "Exclude Behaviours (" + _excludeBehavioursList.count + ")");
            EditorGUI.indentLevel--;

            if (excludeBehavioursFoldoutValue)
            {
                DrawGap(2);
                _excludeBehavioursList.DoLayoutList();
            }
        }

        private void DrawFooter()
        {
            DrawLogo();
            GUILayout.Space(-14); // Offset rest UI to align with logo
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            CreateButtonLinkWithIcon("Tutorials", _tutorial, TUTORIAL_URL);
            CreateButtonLinkWithIcon("Discord", _discord, DISCORD_URL);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            CreateButtonLinkWithIcon("Documentation", _documentation, DOCUMENTATION_URL);
            CreateButtonLinkWithIcon("Developer's Portal", _devPortal, DEVELOPER_PORTAL_URL);
            EditorGUILayout.EndHorizontal();
            CreateButtonLinkWithIcon("Help Desk", _info, HELP_URL);
            EditorGUILayout.LabelField("v" + LivApi.GetVersion(), VERSION_STYLE);
            EditorGUILayout.EndVertical();
        }

        private void DrawLogo()
        {
            if (_logo == null) return;

            GUI.color = EditorStyles.label.normal.textColor;
            float logoWidth = 56f;
            float logoHeight = 32f;
            Rect originalRect = EditorGUILayout.GetControlRect();
            Rect logoRect = originalRect;
            logoRect.height = logoHeight;
            logoRect.width = logoWidth;
            logoRect.x = (originalRect.width - logoWidth / 2 -14);
            GUI.DrawTexture(logoRect, _logo);
        }

        #endregion

        #region UTILS

        private void DrawListItems(Rect rect, int index, bool isActive, bool isFocused)
        {
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(rect, _excludeBehavioursList.serializedProperty.GetArrayElementAtIndex(index), GUIContent.none);
        }

        private void GetTexturesForIcons()
        {
            _logo = GetIconTexture("liv_logo");
            _tutorial = GetIconTexture("liv_tutorial");
            _documentation = GetIconTexture("liv_doc");
            _discord = GetIconTexture("liv_discord");
            _info = GetIconTexture("liv_info");
            _devPortal = GetIconTexture("liv_dev");
            _componentLogo = GetIconTexture("liv_component_logo");
        }

        private static Texture2D GetIconTexture(string iconName)
        {
            string[] iconGuid = AssetDatabase.FindAssets(iconName + " t:texture2D");

            if (iconGuid.Length > 0)
                return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(iconGuid[0]));

            return null;
        }

        private void RenderFieldError(string message)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField(message, ERROR_DESC_STYLE, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawProperty(SerializedProperty property, GUIContent label, GUIStyle labelStyle)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, labelStyle, GUILayout.ExpandWidth(false));
            EditorGUILayout.PropertyField(property, GUIContent.none);
            EditorGUILayout.EndHorizontal();
        }
        private void DrawLineSeparator()
        {
            GUILayout.Space(BASE_MARGIN * 2);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            rect.width = EditorGUIUtility.currentViewWidth;
            rect.x = 0;
            EditorGUI.DrawRect(rect, SEPARATOR_COLOR);
            GUILayout.Space(BASE_MARGIN * 2);
        }

        private void CreateButtonLinkWithIcon(string text, Texture2D icon, string url)
        {
            GUI.color = EditorStyles.label.normal.textColor;
            GUIContent content = icon != null ? new GUIContent(" " + text, icon) : new GUIContent(text);

            if(GUILayout.Button(content, LINK_STYLE, GUILayout.Width(160), GUILayout.Height(16)))
            {
                Application.OpenURL(url);
            }
        }

        #endregion
    }
}
