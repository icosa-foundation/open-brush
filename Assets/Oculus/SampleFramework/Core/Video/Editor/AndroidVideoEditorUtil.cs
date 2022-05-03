using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class AndroidVideoEditorUtil
{
    private static readonly string videoPlayerFileName = "Assets/Oculus/SampleFramework/Core/Video/Plugins/Android/java/com/oculus/videoplayer/NativeVideoPlayer.java";
    private static readonly string disabledPlayerFileName = videoPlayerFileName + ".DISABLED";

    private static readonly string gradleSourceSetPath = "$projectDir/../../Assets/Oculus/SampleFramework/Core/Video/Plugins/Android/java";

    private static readonly string audio360PluginPath = "Assets/Oculus/SampleFramework/Core/Video/Plugins/Android/Audio360/audio360.aar";
    private static readonly string audio360Exo28PluginPath = "Assets/Oculus/SampleFramework/Core/Video/Plugins/Android/Audio360/audio360-exo28.aar";

    private static readonly string gradleTemplatePath = "Assets/Plugins/Android/mainTemplate.gradle";
    private static readonly string disabledGradleTemplatePath = gradleTemplatePath + ".DISABLED";
    private static readonly string internalGradleTemplatePath = Path.Combine(Path.Combine(GetBuildToolsDirectory(BuildTarget.Android), "GradleTemplates"), "mainTemplate.gradle");

    private static string GetBuildToolsDirectory(BuildTarget bt)
    {
        return (string)(typeof(BuildPipeline).GetMethod("GetBuildToolsDirectory", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { bt }));
    }

    [MenuItem("Oculus/Video/Enable Native Android Video Player")]
    public static void EnableNativeVideoPlayer()
    {
        // rename NativeJavaPlayer.java.DISABLED to NativeJavaPlayer.java
        if (File.Exists(disabledPlayerFileName))
        {
            File.Move(disabledPlayerFileName, videoPlayerFileName);
            File.Move(disabledPlayerFileName + ".meta", videoPlayerFileName + ".meta");
        }

        AssetDatabase.ImportAsset(videoPlayerFileName);
        AssetDatabase.DeleteAsset(disabledPlayerFileName);

        // Enable audio plugins
        PluginImporter audio360 = (PluginImporter)AssetImporter.GetAtPath(audio360PluginPath);
        PluginImporter audio360exo28 = (PluginImporter)AssetImporter.GetAtPath(audio360Exo28PluginPath);

        audio360.SetCompatibleWithPlatform(BuildTarget.Android, true);
        audio360exo28.SetCompatibleWithPlatform(BuildTarget.Android, true);

        audio360exo28.SaveAndReimport();

        // Enable gradle build with exoplayer
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

        if (!File.Exists(gradleTemplatePath))
        {
            if (File.Exists(gradleTemplatePath + ".DISABLED"))
            {
                File.Move(disabledGradleTemplatePath, gradleTemplatePath);
                File.Move(disabledGradleTemplatePath + ".meta", gradleTemplatePath+".meta");
            }
            else
            {
                File.Copy(internalGradleTemplatePath, gradleTemplatePath);
            }
            AssetDatabase.ImportAsset(gradleTemplatePath);
        }

        // parse the gradle file to check the current version:
        string currentFile = File.ReadAllText(gradleTemplatePath);

        List<string> lines = new List<string>(currentFile.Split('\n'));

        var gradleVersion = new System.Text.RegularExpressions.Regex("com.android.tools.build:gradle:([0-9]+\\.[0-9]+\\.[0-9]+)").Match(currentFile).Groups[1].Value;

        if (gradleVersion == "2.3.0")
        {
            // add google() to buildscript/repositories
            int buildscriptRepositories = GoToSection("buildscript.repositories", lines);

            if (FindInScope("google\\(\\)", buildscriptRepositories + 1, lines) == -1)
            {
                lines.Insert(GetScopeEnd(buildscriptRepositories + 1, lines), "\t\tgoogle()");
            }

            // add google() and jcenter() to allprojects/repositories
            int allprojectsRepositories = GoToSection("allprojects.repositories", lines);

            if (FindInScope("google\\(\\)", allprojectsRepositories + 1, lines) == -1)
            {
                lines.Insert(GetScopeEnd(allprojectsRepositories + 1, lines), "\t\tgoogle()");
            }
            if (FindInScope("jcenter\\(\\)", allprojectsRepositories + 1, lines) == -1)
            {
                lines.Insert(GetScopeEnd(allprojectsRepositories + 1, lines), "\t\tjcenter()");
            }
        }

        // add "compile 'com.google.android.exoplayer:exoplayer:2.8.4'" to dependencies
        int dependencies = GoToSection("dependencies", lines);
        if (FindInScope("com\\.google\\.android\\.exoplayer:exoplayer", dependencies + 1, lines) == -1)
        {
            lines.Insert(GetScopeEnd(dependencies + 1, lines), "\tcompile 'com.google.android.exoplayer:exoplayer:2.8.4'");
        }

        // add sourceSets if Version < 2018.2
#if !UNITY_2018_2_OR_NEWER
        int android = GoToSection("android", lines);
    
        if (FindInScope("sourceSets\\.main\\.java\\.srcDir", android + 1, lines) == -1)
        {
            lines.Insert(GetScopeEnd(android + 1, lines), "\tsourceSets.main.java.srcDir \"" + gradleSourceSetPath + "\"");
        }
#endif

        File.WriteAllText(gradleTemplatePath, string.Join("\n", lines.ToArray()));
    }

    [MenuItem("Oculus/Video/Disable Native Android Video Player")]
    public static void DisableNativeVideoPlayer()
    {
        if (File.Exists(videoPlayerFileName))
        {
            File.Move(videoPlayerFileName, disabledPlayerFileName);
            File.Move(videoPlayerFileName + ".meta", disabledPlayerFileName + ".meta");
        }

        AssetDatabase.ImportAsset(disabledPlayerFileName);
        AssetDatabase.DeleteAsset(videoPlayerFileName);

        // Disable audio plugins
        PluginImporter audio360 = (PluginImporter)AssetImporter.GetAtPath(audio360PluginPath);
        PluginImporter audio360exo28 = (PluginImporter)AssetImporter.GetAtPath(audio360Exo28PluginPath);

        audio360.SetCompatibleWithPlatform(BuildTarget.Android, false);
        audio360exo28.SetCompatibleWithPlatform(BuildTarget.Android, false);

        audio360exo28.SaveAndReimport();

        // remove exoplayer and sourcesets from gradle file (leave other parts since they are harmless).
        if (File.Exists(gradleTemplatePath))
        {
            // parse the gradle file to check the current version:
            string currentFile = File.ReadAllText(gradleTemplatePath);

            List<string> lines = new List<string>(currentFile.Split('\n'));

            int dependencies = GoToSection("dependencies", lines);
            int exoplayer = FindInScope("com\\.google\\.android\\.exoplayer:exoplayer", dependencies + 1, lines);
            if (exoplayer != -1)
            {
                lines.RemoveAt(exoplayer);
            }

            int android = GoToSection("android", lines);
            int sourceSets = FindInScope("sourceSets\\.main\\.java\\.srcDir", android + 1, lines);
            if (sourceSets != -1)
            {
                lines.RemoveAt(sourceSets);
            }

            File.WriteAllText(gradleTemplatePath, string.Join("\n", lines.ToArray()));
        }
    }

    private static int GoToSection(string section, List<string> lines)
    {
        return GoToSection(section, 0, lines);
    }

    private static int GoToSection(string section, int start, List<string> lines)
    {
        var sections = section.Split('.');

        int p = start - 1;
        for (int i = 0; i < sections.Length; i++)
        {
            p = FindInScope("\\s*" + sections[i] + "\\s*\\{\\s*", p + 1, lines);
        }

        return p;
    }

    private static int FindInScope(string search, int start, List<string> lines)
    {
        var regex = new System.Text.RegularExpressions.Regex(search);

        int depth = 0;

        for (int i = start; i < lines.Count; i++)
        {
            if (depth == 0 && regex.IsMatch(lines[i]))
            {
                return i;
            }

            // count the number of open and close braces. If we leave the current scope, break
            if (lines[i].Contains("{"))
            {
                depth++;
            }
            if (lines[i].Contains("}"))
            {
                depth--;
            }
            if (depth < 0)
            {
                break;
            }
        }
        return -1;
    }

    private static int GetScopeEnd(int start, List<string> lines)
    {
        int depth = 0;
        for (int i = start; i < lines.Count; i++)
        {
            // count the number of open and close braces. If we leave the current scope, break
            if (lines[i].Contains("{"))
            {
                depth++;
            }
            if (lines[i].Contains("}"))
            {
                depth--;
            }
            if (depth < 0)
            {
                return i;
            }
        }

        return -1;
    }

}
