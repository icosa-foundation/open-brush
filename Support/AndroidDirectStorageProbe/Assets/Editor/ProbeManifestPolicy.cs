using System;
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

public sealed class ProbeManifestPolicy : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 1000;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
        var manifest = new XmlDocument();
        manifest.Load(manifestPath);

        var legacy = (XmlDocument)manifest.CloneNode(true);
        ((XmlElement)legacy.SelectSingleNode("manifest/application")).SetAttribute(
            "requestLegacyExternalStorage", AndroidStoreManifest.AndroidNamespace, "true");
        AndroidStoreManifest.Configure(legacy, false, false, false);
        if (legacy.SelectSingleNode("manifest/application").Attributes["requestLegacyExternalStorage",
            AndroidStoreManifest.AndroidNamespace]?.Value != "true")
            throw new Exception("OBDS_MANIFEST non-scoped policy changed legacy access");

        bool rejectedMetaCombination = false;
        try { AndroidStoreManifest.Configure((XmlDocument)manifest.CloneNode(true), true, false, true); }
        catch (InvalidOperationException) { rejectedMetaCombination = true; }
        if (!rejectedMetaCombination) throw new Exception("OBDS_MANIFEST scoped Meta combination was accepted");

        AndroidStoreManifest.Configure(manifest, false, false, true);
        // Unity can reuse the generated manifest on an incremental build.
        AndroidStoreManifest.Configure(manifest, false, false, true);
        manifest.Save(manifestPath);
        File.WriteAllText("Build/manifest-policy.txt", "OBDS_MANIFEST production policy callback completed");
        Debug.Log("OBDS_MANIFEST production scoped policy applied; inspect actual merged APK permissions");
    }
}
