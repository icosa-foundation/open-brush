using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LIV.SDK.Unity
{
    public class SDKSettings : ScriptableObject
    {
        private const string FILE_NAME = "LIVSDKSettings";
        private const string LIV_ROOT_DIR = "LIV";
        private const string RESOURCE_DIR = "Resources";
#pragma warning disable 0649
        [SerializeField] private string _trackingID = "testing_tracking_id";
        [SerializeField] private SDKBridge.CaptureProtocolType _captureProtocolType = SDKBridge.CaptureProtocolType.BRIDGE;
#pragma warning restore 0649
        public string trackingID
        {
            get { return _trackingID; }
        }

        public SDKBridge.CaptureProtocolType captureProtocolType
        {
            get { return _captureProtocolType; }
        }

        private static SDKSettings _instance;

        public static SDKSettings instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<SDKSettings>(FILE_NAME);
                
#if UNITY_EDITOR
                if (_instance == null)
                {
                    //Get path to SDKSettings script
                    SDKSettings scriptableObject = ScriptableObject.CreateInstance<SDKSettings>();
                    UnityEditor.MonoScript monoScript = UnityEditor.MonoScript.FromScriptableObject(scriptableObject);
                    var assetPath = UnityEditor.AssetDatabase.GetAssetPath(monoScript);
                    //Get last index of last occurence of LIV in path
                    var livIndex = assetPath.LastIndexOf(LIV_ROOT_DIR);
                    var livDirectory = assetPath.Substring(0,livIndex+LIV_ROOT_DIR.Length);
                    //Create path to Resources
                    var resourcesPath = System.IO.Path.Combine(livDirectory, RESOURCE_DIR);
                    //Check if Resources present
                    if (!System.IO.Directory.Exists(resourcesPath))
                    {
                        System.IO.Directory.CreateDirectory(resourcesPath);
                    }
                    //Create filepath with target path and asset name.
                    string filePath = System.IO.Path.Combine(resourcesPath, FILE_NAME + ".asset");
                    //Save to DB
                    UnityEditor.AssetDatabase.CreateAsset(scriptableObject, filePath);
                    UnityEditor.AssetDatabase.SaveAssets();
                    _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<SDKSettings>(filePath);
                }
#endif
                if (_instance == null)
                    Debug.LogError("LIV: Unable to find LIV SDK Settings!");
                
                return _instance;
            }
        }
    }
}