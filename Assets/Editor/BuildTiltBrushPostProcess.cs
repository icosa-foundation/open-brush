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

            XmlElement element = (XmlElement)doc.SelectSingleNode("/manifest");
            var androidNamespaceURI = element.GetAttribute("xmlns:android");


            if (BuildTiltBrush.IsGooglePlayBuildActive)
            {
                UnityEngine.Debug.Log("Apply Google Play Android storage manifest profile");
                AddOrRemoveTag(doc,
                    androidNamespaceURI,
                    "/manifest/application",
                    "meta-data",
                    "unityplayer.SkipPermissionsDialog",
                    true,
                    true,
                    "value", "true"
                );

                foreach (string permission in new[]
                {
                    "android.permission.MANAGE_EXTERNAL_STORAGE",
                    "android.permission.WRITE_EXTERNAL_STORAGE",
                    "android.permission.READ_EXTERNAL_STORAGE",
                    "android.permission.READ_MEDIA_AUDIO",
                    "android.permission.READ_MEDIA_IMAGES",
                    "android.permission.READ_MEDIA_VIDEO",
                    "android.permission.READ_MEDIA_VISUAL_USER_SELECTED",
                })
                {
                    RemovePermissionTags(doc, androidNamespaceURI, permission);
                }

                var application = (XmlElement)doc.SelectSingleNode("/manifest/application");
                application?.RemoveAttribute("requestLegacyExternalStorage", androidNamespaceURI);
            }

            ConfigureGameActivityLauncher(doc);

#if USE_QUEST_PACKAGE_NAME
            const bool metaStore = true;
#else
            const bool metaStore = false;
#endif
            AndroidStoreManifest.Configure(doc, metaStore,
                BuildTiltBrush.CurrentBuildXrSdk == TiltBrush.XrSdkMode.AndroidXR);

            doc.Save(file);
            UnityEngine.Debug.Log($"[OB-STORE-MANIFEST] Applied Android manifest settings: " +
                $"MetaStore={metaStore}, XR={BuildTiltBrush.CurrentBuildXrSdk}.");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogException(e);
            throw;
        }
    }

    private static void RemovePermissionTags(XmlDocument doc, string @namespace, string permission)
    {
        RemoveTags(doc, @namespace, "/manifest", "uses-permission", permission);
        RemoveTags(doc, @namespace, "/manifest", "uses-permission-sdk-23", permission);
    }

    private static void RemoveTags(XmlDocument doc, string @namespace, string path, string elementName, string name)
    {
        var nodes = doc.SelectNodes(path + "/" + elementName);
        for (int i = nodes.Count - 1; i >= 0; --i)
        {
            XmlElement element = nodes[i] as XmlElement;
            if (element != null && (name == null || name == element.GetAttribute("name", @namespace)))
            {
                element.ParentNode?.RemoveChild(element);
            }
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
    /// XR library manifests are merged into the final application by Gradle.
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
