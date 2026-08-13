// Copyright 2023 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.IO;
using System.Xml;
using UnityEditor;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif

[InitializeOnLoad]
public class BuildTiltBrushPostProcess
#if UNITY_ANDROID
    : IPostGenerateGradleAndroidProject
#endif
{
    private const string kAndroidNamespace = "http://schemas.android.com/apk/res/android";
    private const string kPlayerActivity = "com.unity3d.player.UnityPlayerActivity";
    private const string kGameActivity = "com.unity3d.player.UnityPlayerGameActivity";

    // OVRGradleGeneration is 99999, so we'll just go to the extreme.
    public int callbackOrder => 1000000;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifestFolder = Path.Combine(path, "src/main");
        string file = manifestFolder + "/AndroidManifest.xml";

        try
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(file);

            ConfigureGameActivityLauncher(doc);

            doc.Save(file);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogException(e);
            throw;
        }
    }

    /// <summary>
    /// Makes the generated launcher agree with Unity's selected Android application entry point.
    /// </summary>
    /// <remarks>
    /// Our custom source manifest declares PlayerActivity so established Android targets retain
    /// their existing launcher. For an AndroidXR build, BuildTiltBrush temporarily selects
    /// GameActivity as required by Unity's Android XR package. Unity disables PlayerActivity but
    /// leaves the custom MAIN/LAUNCHER intent on it, producing an application with no usable
    /// launcher.
    ///
    /// Change only the generated Gradle manifest. This avoids modifying and reimporting a shared
    /// project asset during a build, and leaves every build that selects PlayerActivity untouched.
    /// Unity's XR manifest processor will merge its Android XR properties into this activity later.
    /// </remarks>
    private static void ConfigureGameActivityLauncher(XmlDocument doc)
    {
        if (PlayerSettings.Android.applicationEntry != AndroidApplicationEntry.GameActivity)
        {
            return;
        }

        var namespaceManager = new XmlNamespaceManager(doc.NameTable);
        namespaceManager.AddNamespace("android", kAndroidNamespace);
        var launcherActivity = doc.SelectSingleNode(
            "/manifest/application/activity[@android:name='" + kPlayerActivity + "']" +
            "[intent-filter/action[@android:name='android.intent.action.MAIN']]" +
            "[intent-filter/category[@android:name='android.intent.category.LAUNCHER']]",
            namespaceManager) as XmlElement;
        if (launcherActivity == null)
        {
            throw new BuildTiltBrush.BuildFailedException(
                "The generated Android manifest has no PlayerActivity launcher to convert " +
                "for the selected GameActivity entry point.");
        }

        // Preserve Unity's generated launch mode, configuration changes, orientation, and other
        // project-specific attributes. Only the GameActivity-specific identity and bootstrap
        // values need to differ.
        launcherActivity.SetAttribute("name", kAndroidNamespace, kGameActivity);
        launcherActivity.SetAttribute("theme", kAndroidNamespace,
            "@style/BaseUnityGameActivityTheme");
        launcherActivity.SetAttribute("enabled", kAndroidNamespace, "true");

        SetMetadata(doc, launcherActivity, namespaceManager,
            "unityplayer.UnityActivity", "true");
        SetMetadata(doc, launcherActivity, namespaceManager,
            "android.app.lib_name", "game");

        UnityEngine.Debug.Log(
            "Configured the generated Android manifest to launch GameActivity.");
    }

    private static void SetMetadata(
        XmlDocument doc,
        XmlElement activity,
        XmlNamespaceManager namespaceManager,
        string name,
        string value)
    {
        var metadata = activity.SelectSingleNode(
            "meta-data[@android:name='" + name + "']", namespaceManager) as XmlElement;
        if (metadata == null)
        {
            metadata = doc.CreateElement("meta-data");
            metadata.SetAttribute("name", kAndroidNamespace, name);
            activity.AppendChild(metadata);
        }

        metadata.SetAttribute("value", kAndroidNamespace, value);
    }

    private static void AddOrRemoveTag(XmlDocument doc, string @namespace, string path, string elementName, string name,
        bool required, bool modifyIfFound, params string[] attrs) // name, value pairs
    {
        var nodes = doc.SelectNodes(path + "/" + elementName);
        XmlElement element = null;
        foreach (XmlElement e in nodes)
        {
            if (name == null || name == e.GetAttribute("name", @namespace))
            {
                element = e;
                break;
            }
        }

        if (required)
        {
            if (element == null)
            {
                var parent = doc.SelectSingleNode(path);
                element = doc.CreateElement(elementName);
                element.SetAttribute("name", @namespace, name);
                parent.AppendChild(element);
            }

            for (int i = 0; i < attrs.Length; i += 2)
            {
                if (modifyIfFound || string.IsNullOrEmpty(element.GetAttribute(attrs[i], @namespace)))
                {
                    if (attrs[i + 1] != null)
                    {
                        element.SetAttribute(attrs[i], @namespace, attrs[i + 1]);
                    }
                    else
                    {
                        element.RemoveAttribute(attrs[i], @namespace);
                    }
                }
            }
        }
        else
        {
            if (element != null && modifyIfFound)
            {
                element.ParentNode.RemoveChild(element);
            }
        }
    }

}
