using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using System.IO;

namespace TiltBrush
{
    public class WriteFullUserConfig
    {
        [MenuItem("Open Brush/Write Full User Config")]
        public static void DoWriteFullUserConfig()
        {
            // Quit if we're not in play mode
            if (!Application.isPlaying)
            {
                Debug.LogError("Enter Play Mode and try again.");
                return;
            }
            string path = $"{App.UserPath()}/Full Open Brush.cfg";
            string json = JsonConvert.SerializeObject(App.UserConfig, Formatting.Indented);
            File.WriteAllText(path, json);
            Debug.Log($"User data written to {path}");
        }
    }
}
