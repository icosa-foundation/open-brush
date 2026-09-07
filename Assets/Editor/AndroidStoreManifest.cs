// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Xml;

/// <summary>Store requirements applied to the generated manifest, without editing project assets.</summary>
internal static class AndroidStoreManifest
{
    internal const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
    private const string ToolsNamespace = "http://schemas.android.com/tools";

    internal static void Configure(XmlDocument doc, bool metaStore, bool androidXr)
    {
        var root = doc.DocumentElement ?? throw new InvalidOperationException("Missing Android manifest.");
        root.SetAttribute("xmlns:tools", ToolsNamespace);
        var app = root.SelectSingleNode("application") as XmlElement
            ?? throw new InvalidOperationException("Missing Android application element.");

        // AndroidXR derives hardware requirements from the foveation feature's extension list,
        // including eye-tracked foveation. Open Brush also works with fixed foveation and no
        // eye tracker. Override the lower-priority XR library manifest's required=true value.
        // Meta's feature processor can also add a separate required software feature.
        foreach (string feature in new[] { "android.hardware.xr.input.eye_tracking", "oculus.software.eye_tracking" })
        {
            var eyes = GetOrCreate(doc, root, "uses-feature", feature);
            SetAndroid(eyes, "required", "false");
            eyes.SetAttribute("replace", ToolsNamespace, "android:required");
        }

        if (!androidXr)
        {
            // These describe Google's AndroidXR platform, not the ability to load an OpenXR
            // runtime. Other stores/devices must not be filtered out by Google's requirements.
            foreach (string feature in new[] { "android.software.xr.api.openxr", "android.software.xr.api.spatial" })
            {
                var node = GetOrCreate(doc, root, "uses-feature", feature);
                node.SetAttribute("node", ToolsNamespace, "remove");
            }
        }

        if (!metaStore)
        {
            return;
        }

        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("android", AndroidNamespace);
        var launcher = app.SelectSingleNode(
            "activity[intent-filter/action[@android:name='android.intent.action.MAIN']]" +
            "[intent-filter/category[@android:name='android.intent.category.LAUNCHER']]", ns) as XmlElement
            ?? throw new InvalidOperationException("Meta store build has no launcher activity.");
        var intent = launcher.SelectSingleNode(
            "intent-filter[action[@android:name='android.intent.action.MAIN']]", ns) as XmlElement;

        SetAndroid(root, "installLocation", "auto");
        SetAndroid(launcher, "excludeFromRecents", "true");
        GetOrCreate(doc, intent, "category", "com.oculus.intent.category.VR");
        SetMetadata(doc, launcher, "com.oculus.vr.focusaware", "true");
        // Use current store identifiers instead of the older package's cambria/eureka aliases.
        SetMetadata(doc, app, "com.oculus.supportedDevices", "quest2|questpro|quest3|quest3s");
        SetMetadata(doc, app, "com.samsung.android.vr.application.mode", "vr_only");
        var headTracking = GetOrCreate(doc, root, "uses-feature", "android.hardware.vr.headtracking");
        SetAndroid(headTracking, "required", "true");
        SetAndroid(headTracking, "version", "1");
    }

    private static void SetMetadata(XmlDocument doc, XmlElement parent, string name, string value)
    {
        var node = GetOrCreate(doc, parent, "meta-data", name);
        SetAndroid(node, "value", value);
        node.SetAttribute("replace", ToolsNamespace, "android:value");
    }

    private static void SetAndroid(XmlElement element, string name, string value) =>
        element.SetAttribute(name, AndroidNamespace, value);

    private static XmlElement GetOrCreate(XmlDocument doc, XmlElement parent, string tag, string name)
    {
        foreach (XmlNode child in parent.ChildNodes)
        {
            if (child is XmlElement element && element.Name == tag &&
                element.GetAttribute("name", AndroidNamespace) == name)
            {
                return element;
            }
        }
        var created = doc.CreateElement(tag);
        SetAndroid(created, "name", name);
        parent.AppendChild(created);
        return created;
    }
}
