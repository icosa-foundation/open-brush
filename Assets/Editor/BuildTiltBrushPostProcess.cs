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



#if FORCE_QUEST_SUPPORT_DEVICE
            UnityEngine.Debug.Log("Add quest as a supported devices");
            AddOrRemoveTag(doc,
                androidNamespaceURI,
                "/manifest/application",
                "meta-data",
                "com.oculus.supportedDevices",
                true,
                true,
                "value", "quest"
            );
#endif

#if FORCE_FOCUSAWARE
            UnityEngine.Debug.Log("Add com.oculus.vr.focusaware");
            AddOrRemoveTag(doc,
                androidNamespaceURI,
                "/manifest/application/activity",
                "meta-data",
                "com.oculus.vr.focusaware",
                true,
                true,
                "value", "true"
            );
#endif

#if FORCE_HEADTRACKING
            UnityEngine.Debug.Log("Add android.hardware.vr.headtracking");
            AddOrRemoveTag(doc,
                    androidNamespaceURI,
                    "/manifest",
                    "uses-feature",
                    "android.hardware.vr.headtracking",
                    true,
                    true,
                    "version", "1",
                    "required", "true"
            );
#endif

            doc.Save(file);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogException(e);
        }
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
