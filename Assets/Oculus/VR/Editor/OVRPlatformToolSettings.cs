using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Assets.Oculus.VR.Editor
{
#if UNITY_EDITOR
	[UnityEditor.InitializeOnLoad]
#endif
	public sealed class OVRPlatformToolSettings : ScriptableObject
	{
		private const string DEFAULT_RELEASE_CHANNEL = "Alpha";

		public enum GamepadType
		{
			OFF,
			TWINSTICK,
			RIGHT_D_PAD,
			LEFT_D_PAD,
		};

		public static string AppID
		{
			get
			{
				return Instance.targetPlatform < OVRPlatformTool.TargetPlatform.None ? Instance.appIDs[(int)Instance.targetPlatform] : "";
			}
			set
			{
				if (Instance.targetPlatform < OVRPlatformTool.TargetPlatform.None)
				{
					Instance.appIDs[(int)Instance.targetPlatform] = value;
				}
			}
		}

		public static string AppToken
		{
			get
			{
				return Instance.targetPlatform < OVRPlatformTool.TargetPlatform.None ? Instance.appTokens[(int)Instance.targetPlatform] : "";
			}
			set
			{
				if (Instance.targetPlatform < OVRPlatformTool.TargetPlatform.None)
				{
					Instance.appTokens[(int)Instance.targetPlatform] = value;
				}
			}
		}

		public static string ReleaseNote
		{
			get
			{
				return Instance.targetPlatform < OVRPlatformTool.TargetPlatform.None ? Instance.releaseNotes[(int)Instance.targetPlatform] : "";
			}
			set
			{
				if (Instance.targetPlatform < OVRPlatformTool.TargetPlatform.None)
				{
					Instance.releaseNotes[(int)Instance.targetPlatform] = value;
				}
			}
		}

		public static string ReleaseChannel
		{
			get
			{
				return Instance.targetPlatform < OVRPlatformTool.TargetPlatform.None ? Instance.releaseChannels[(int)Instance.targetPlatform] : "";
			}
			set
			{
				if (Instance.targetPlatform < OVRPlatformTool.TargetPlatform.None)
				{
					Instance.releaseChannels[(int)Instance.targetPlatform] = value;
				}
			}
		}

		public static string ApkBuildPath
		{
			get
			{
				return Instance.targetPlatform < OVRPlatformTool.TargetPlatform.None ? Instance.apkBuildPaths[(int)Instance.targetPlatform] : "";
			}
			set
			{
				if (Instance.targetPlatform < OVRPlatformTool.TargetPlatform.None)
				{
					Instance.apkBuildPaths[(int)Instance.targetPlatform] = value;
				}
			}
		}

		public static string ObbFilePath
		{
			get
			{
				return Instance.targetPlatform < OVRPlatformTool.TargetPlatform.None ? Instance.obbFilePaths[(int)Instance.targetPlatform] : "";
			}
			set
			{
				if (Instance.targetPlatform < OVRPlatformTool.TargetPlatform.None)
				{
					Instance.obbFilePaths[(int)Instance.targetPlatform] = value;
				}
			}
		}

		public static string AssetsDirectory
		{
			get
			{
				return Instance.targetPlatform < OVRPlatformTool.TargetPlatform.None ? Instance.assetsDirectorys[(int)Instance.targetPlatform] : "";
			}
			set
			{
				if (Instance.targetPlatform < OVRPlatformTool.TargetPlatform.None)
				{
					Instance.assetsDirectorys[(int)Instance.targetPlatform] = value;
				}
			}
		}

		public static string RiftBuildDirectory
		{
			get { return Instance.riftBuildDiretory; }
			set { Instance.riftBuildDiretory = value; }
		}

		public static string RiftBuildVersion
		{
			get { return Instance.riftBuildVersion; }
			set { Instance.riftBuildVersion = value; }
		}

		public static string RiftLaunchFile
		{
			get { return Instance.riftLaunchFile; }
			set { Instance.riftLaunchFile = value; }
		}

		public static string RiftLaunchParams
		{
			get { return Instance.riftLaunchParams; }
			set { Instance.riftLaunchParams = value; }
		}

		public static string Rift2DLaunchFile
		{
			get { return Instance.rift2DLaunchFile; }
			set { Instance.rift2DLaunchFile = value; }
		}

		public static string Rift2DLaunchParams
		{
			get { return Instance.rift2DLaunchParams; }
			set { Instance.rift2DLaunchParams = value; }
		}

		public static bool RiftFirewallException
		{
			get { return Instance.riftFirewallException; }
			set { Instance.riftFirewallException = value; }
		}

		public static GamepadType RiftGamepadEmulation
		{
			get { return Instance.riftGamepadEmulation; }
			set { Instance.riftGamepadEmulation = value; }
		}

		public static List<RedistPackage> RiftRedistPackages
		{
			get { return Instance.riftRedistPackages; }
			set { Instance.riftRedistPackages = value; }
		}

		public static string LanguagePackDirectory
		{
			get { return Instance.languagePackDirectory; }
			set { Instance.languagePackDirectory = value; }
		}

		public static List<AssetConfig> AssetConfigs
		{
			get
			{
				return Instance.targetPlatform < OVRPlatformTool.TargetPlatform.None ? Instance.assetConfigs[(int)Instance.targetPlatform].configList : new List<AssetConfig>();
			}
			set
			{
				if (Instance.targetPlatform < OVRPlatformTool.TargetPlatform.None)
				{
					Instance.assetConfigs[(int)Instance.targetPlatform].configList = value;
				}
			}
		}

		public static OVRPlatformTool.TargetPlatform TargetPlatform
		{
			get { return Instance.targetPlatform; }
			set { Instance.targetPlatform = value; }
		}

		public static bool RunOvrLint
		{
			get { return Instance.runOvrLint; }
			set { Instance.runOvrLint = value; }
		}

		[SerializeField]
		private string[] appIDs = new string[(int)OVRPlatformTool.TargetPlatform.None];

		[SerializeField]
		private string[] appTokens = new string[(int)OVRPlatformTool.TargetPlatform.None];

		[SerializeField]
		private string[] releaseNotes = new string[(int)OVRPlatformTool.TargetPlatform.None];

		[SerializeField]
		private string[] releaseChannels = new string[(int)OVRPlatformTool.TargetPlatform.None];

		[SerializeField]
		private string riftBuildDiretory = "";

		[SerializeField]
		private string riftBuildVersion = "";

		[SerializeField]
		private string riftLaunchFile = "";

		[SerializeField]
		private string riftLaunchParams = "";

		[SerializeField]
		private string rift2DLaunchFile = "";

		[SerializeField]
		private string rift2DLaunchParams = "";

		[SerializeField]
		private bool riftFirewallException = false;

		[SerializeField]
		private GamepadType riftGamepadEmulation = GamepadType.OFF;

		[SerializeField]
		private List<RedistPackage> riftRedistPackages;

		[SerializeField]
		private string languagePackDirectory = "";

		[SerializeField]
		private string[] apkBuildPaths = new string[(int)OVRPlatformTool.TargetPlatform.None];

		[SerializeField]
		private string[] obbFilePaths = new string[(int)OVRPlatformTool.TargetPlatform.None];

		[SerializeField]
		private string[] assetsDirectorys = new string[(int)OVRPlatformTool.TargetPlatform.None];

		[SerializeField]
		private AssetConfigList[] assetConfigs = new AssetConfigList[(int)OVRPlatformTool.TargetPlatform.None];

		[SerializeField]
		private OVRPlatformTool.TargetPlatform targetPlatform = OVRPlatformTool.TargetPlatform.None;

		[SerializeField]
		private bool runOvrLint = true;

		private static OVRPlatformToolSettings instance;
		public static OVRPlatformToolSettings Instance
		{
			get
			{
				if (instance == null)
				{
					instance = Resources.Load<OVRPlatformToolSettings>("OVRPlatformToolSettings");

					if (instance == null)
					{
						instance = ScriptableObject.CreateInstance<OVRPlatformToolSettings>();

						string properPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "Resources");
						if (!System.IO.Directory.Exists(properPath))
						{
							UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
						}

						string fullPath = System.IO.Path.Combine(
							System.IO.Path.Combine("Assets", "Resources"),
							"OVRPlatformToolSettings.asset"
						);
						UnityEditor.AssetDatabase.CreateAsset(instance, fullPath);

						// Initialize cross platform default values for the new instance of OVRPlatformToolSettings here
						if (instance != null)
						{
							for (int i = 0; i < (int)OVRPlatformTool.TargetPlatform.None; i++)
							{
								instance.releaseChannels[i] = DEFAULT_RELEASE_CHANNEL;
								instance.assetConfigs[i] = new AssetConfigList();
							}

							instance.riftRedistPackages = new List<RedistPackage>();
						}
					}
				}
				return instance;
			}
			set
			{
				instance = value;
			}
		}
	}

	// Wrapper for asset config list so that it can be serialized properly
	[System.Serializable]
	public class AssetConfigList
	{
		public List<AssetConfig> configList;

		public AssetConfigList()
		{
			configList = new List<AssetConfig>();
		}
	}

	[System.Serializable]
	public class AssetConfig
	{
		public enum AssetType
		{
			DEFAULT,
			STORE,
			LANGUAGE_PACK,
		};

		public string name;
		public bool required;
		public AssetType type;
		public string sku;

		private bool foldout;

		public AssetConfig(string assetName)
		{
			name = assetName;
		}

		public bool GetFoldoutState()
		{
			return foldout;
		}

		public void SetFoldoutState(bool state)
		{
			foldout = state;
		}
	}

	[System.Serializable]
	public class RedistPackage
	{
		public bool include = false;
		public string name;
		public string id;

		public RedistPackage(string pkgName, string pkgId)
		{
			name = pkgName;
			id = pkgId;
		}
	}
}
