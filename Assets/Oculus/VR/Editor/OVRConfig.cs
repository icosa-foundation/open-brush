using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;

#if UNITY_EDITOR
[UnityEditor.InitializeOnLoad]
#endif
public class OVRConfig : ScriptableObject
{
	[SerializeField]
	private string androidSDKPath = "";

	[SerializeField]
	private string gradlePath = "";

	[SerializeField]
	private string jdkPath = "";

	private static OVRConfig instance;

	public static OVRConfig Instance
	{
		get
		{
			if (instance == null)
			{
				instance = Resources.Load<OVRConfig>("OVRConfig");
				if (instance == null)
				{
					instance = ScriptableObject.CreateInstance<OVRConfig>();
					string resourcePath = Path.Combine(UnityEngine.Application.dataPath, "Resources");
					if (!Directory.Exists(resourcePath))
					{
						UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
					}

					string fullPath = Path.Combine(Path.Combine("Assets", "Resources"), "OVRBuildConfig.asset");
					UnityEditor.AssetDatabase.CreateAsset(instance, fullPath);
				}
			}
			return instance;
		}
		set
		{
			instance = value;
		}
	}

	// Returns the path to the base directory of the Android SDK
	public string GetAndroidSDKPath(bool throwError = true)
	{
#if UNITY_2019_1_OR_NEWER
		// Check for use of embedded path or user defined 
		bool useEmbedded = EditorPrefs.GetBool("SdkUseEmbedded");
		if (useEmbedded)
		{
			androidSDKPath = Path.Combine(BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None), "SDK");
		}
		else
#endif
		{
			androidSDKPath = EditorPrefs.GetString("AndroidSdkRoot");
		}

		androidSDKPath = androidSDKPath.Replace("/", "\\");
		// Validate sdk path and notify user if path does not exist.
		if (!Directory.Exists(androidSDKPath))
		{
			androidSDKPath = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
			if (!string.IsNullOrEmpty(androidSDKPath))
			{
				return androidSDKPath;
			}

			if (throwError)
			{
				EditorUtility.DisplayDialog("Android SDK not Found",
						"Android SDK not found. Please ensure that the path is set correctly in (Edit -> Preferences -> External Tools) or that the Untiy Android module is installed correctly.",
						"Ok");
			}
			return string.Empty;
		}

		EditorUtility.SetDirty(Instance);
		return androidSDKPath;
	}

	// Returns the path to the gradle-launcher-*.jar
	public string GetGradlePath(bool throwError = true)
	{
		string libPath = "";
#if UNITY_2019_1_OR_NEWER
		// Check for use of embedded path or user defined 
		bool useEmbedded = EditorPrefs.GetBool("GradleUseEmbedded");

		if (useEmbedded)
		{
			libPath = Path.Combine(BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None), "Tools\\gradle\\lib");
		}
		else
		{
			libPath = Path.Combine(EditorPrefs.GetString("GradlePath"), "lib");
		}
#else
		libPath = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines\\AndroidPlayer\\Tools\\gradle\\lib");
#endif

		libPath = libPath.Replace("/", "\\");
		if (!string.IsNullOrEmpty(libPath) && Directory.Exists(libPath))
		{
			string[] gradleJar = Directory.GetFiles(libPath, "gradle-launcher-*.jar");
			if (gradleJar.Length == 1)
			{
				gradlePath = gradleJar[0];
			}
		}

		// Validate gradle path and notify user if path does not exist.
		if (!File.Exists(gradlePath))
		{
			if (throwError)
			{
				EditorUtility.DisplayDialog("Gradle not Found",
						"Gradle not found. Please ensure that the path is set correctly in (Edit -> Preferences -> External Tools) or that the Untiy Android module is installed correctly.",
						"Ok");
			}
			return string.Empty;
		}

		EditorUtility.SetDirty(Instance);
		return gradlePath;
	}

	// Returns path to the Java executable in the JDK
	public string GetJDKPath(bool throwError = true)
	{
#if UNITY_EDITOR_WIN
		// Check for use of embedded path or user defined 
		bool useEmbedded = EditorPrefs.GetBool("JdkUseEmbedded");

		string exePath = "";
		if (useEmbedded)
		{
#if UNITY_2019_1_OR_NEWER
			exePath = Path.Combine(BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None), "Tools\\OpenJDK\\Windows\\bin");
#else
			exePath = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines\\AndroidPlayer\\Tools\\OpenJDK\\Windows\\bin");
#endif
		}
		else
		{
			exePath = Path.Combine(EditorPrefs.GetString("JdkPath"), "bin");
		}

		jdkPath = Path.Combine(exePath, "java.exe");
		jdkPath = jdkPath.Replace("/", "\\");

		// Validate gradle path and notify user if path does not exist.
		if (!File.Exists(jdkPath))
		{
			// Check the enviornment variable as a backup to see if the JDK is there.
			string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
			if(!string.IsNullOrEmpty(javaHome))
			{
				jdkPath = Path.Combine(javaHome, "bin\\java.exe");
				if(File.Exists(jdkPath))
				{
					EditorUtility.SetDirty(Instance);
					return jdkPath;
				}
			}

			if (throwError)
			{
				EditorUtility.DisplayDialog("JDK not Found",
					"JDK not found. Please ensure that the path is set correctly in (Edit -> Preferences -> External Tools) or that the Untiy Android module is installed correctly.",
					"Ok");
			}
			return string.Empty;
		}
#endif
		EditorUtility.SetDirty(Instance);
		return jdkPath;
	}
}
