using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace LIV.SDK.Unity
{
	[CustomEditor(typeof(SDKSettings))]
	public class SDKSettingsEditor : Editor
	{
		static GUIContent TRACKING_ID_GUICONTENT = new GUIContent("Tracking ID");
		public static GUIContent TRACKING_ID_INFO_GUICONTENT = new GUIContent(
			"Tracking ID that identifies your game to the LIV backend, allowing you to get usage analytics."
		);

		const string TRACKING_ID_PROPERTY = "_trackingID";

		private SerializedProperty trackingIDProperty;
		
		
		public static Color darkBGColor {
			get {
				if (EditorGUIUtility.isProSkin)
				{
					return new Color(0f, 0f, 0f, 0.5f);
				} else
				{
					return new Color(1f, 1f, 1f, 0.5f);
				}
			}
		}
		public static Color lightRedBGColor {
			get {
				return new Color(1f, 0.5f, 0.5f, 1f);
			}
		}
		public static Color lightBGColor {
			get {
				return new Color(1f, 1, 1, 1f);
			}
		}
		
		void OnEnable()
		{

			
			trackingIDProperty = serializedObject.FindProperty(TRACKING_ID_PROPERTY);
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			RenderTrackingID();
			RenderRenderPipelineSelector();
			serializedObject.ApplyModifiedProperties();
		}

		void RenderTrackingID()
		{
			Color color = lightBGColor;
			GUIContent content = new GUIContent(TRACKING_ID_INFO_GUICONTENT);
			string trackingID = trackingIDProperty.stringValue;
			var isTrackingIDMissing = string.IsNullOrEmpty(trackingID);
            
			Color lastAccentColor = GUI.color;
			GUI.color = darkBGColor;
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			GUI.color = lastAccentColor;
			GUILayout.Space(2);
            
			if (isTrackingIDMissing)
			{
				color = lightRedBGColor;
				content.text += "\nThe tracking ID has to be set! Click the button bellow to get the tracking ID for your game.";
				content.image = EditorGUIUtility.IconContent("console.erroricon").image;
			}
            
			EditorGUILayout.PropertyField(trackingIDProperty, TRACKING_ID_GUICONTENT);
			Color lastBackgroundColor = GUI.backgroundColor;
			GUI.backgroundColor = color;
			EditorGUILayout.LabelField(content, EditorStyles.helpBox);
			GUI.backgroundColor = lastBackgroundColor;
            
			GUIContent authButtonContent = new GUIContent(EditorGUIUtility.IconContent("CloudConnect"));
			authButtonContent.text = "Get tracking ID";
			if (GUILayout.Button(authButtonContent))
			{
				Application.OpenURL("https://dev.liv.tv");
			}
            
			EditorGUILayout.EndVertical();
		}

		private const string LIV_UNIVERSAL_RENDER = "LIV_UNIVERSAL_RENDER";
		private const string URP_COMPATIBILITY_MODE = "URP_COMPATIBILITY_MODE";

		void RenderRenderPipelineSelector()
		{
			bool isLivURPDefined = GetScriptingDefineSymbols().Contains(LIV_UNIVERSAL_RENDER);

			Color lastAccentColor = GUI.color;
			GUI.color = darkBGColor;
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			GUI.color = lastAccentColor;
			
			if (isLivURPDefined)
			{
                GUILayout.Label("Liv is using: Universal render pipeline");
				if (GUILayout.Button("Switch to Legacy render pipeline"))
					SetRenderPipelineLegacy();
			}
			else
			{
                GUILayout.Label("Liv is using: Legacy render pipeline");
				if (GUILayout.Button("Switch to Universal render pipeline"))
					SetRenderPipelineUniversal();
			}

			EditorGUILayout.EndVertical();
		}

		static List<string> GetScriptingDefineSymbols()
		{
			string scriptingDefineSymbolsString = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
			return new List<string>(scriptingDefineSymbolsString.Split(';'));	
		}
		
		static void SetRenderPipelineLegacy()
		{
			List<string> scriptingDefineSymbolsString = GetScriptingDefineSymbols();
			scriptingDefineSymbolsString.Remove(LIV_UNIVERSAL_RENDER);
#if UNITY_6000_3_OR_NEWER
			scriptingDefineSymbolsString.Remove(URP_COMPATIBILITY_MODE);
#endif
			PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone,
				string.Join(";", scriptingDefineSymbolsString.ToArray()));
			Debug.Log("Render pipeline has been set to legacy");
		}

		static void SetRenderPipelineUniversal()
		{
			List<string> scriptingDefineSymbolsString = GetScriptingDefineSymbols();
			scriptingDefineSymbolsString.Add(LIV_UNIVERSAL_RENDER);
#if UNITY_6000_3_OR_NEWER
			scriptingDefineSymbolsString.Add(URP_COMPATIBILITY_MODE);
#endif
			PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone,
				string.Join(";", scriptingDefineSymbolsString.ToArray()));
			Debug.Log("Render pipeline has been set to URP");
		}
	}
}