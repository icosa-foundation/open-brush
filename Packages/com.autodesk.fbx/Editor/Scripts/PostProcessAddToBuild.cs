#if FBXSDK_RUNTIME
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System.IO;

namespace Autodesk.Fbx
{
    /// <summary>
    /// Add UnityFbxSdkNative plugin to build after build is complete.
    /// </summary>
    public class PostProcessAddToBuild
    {
        private const string fbxsdkNativePlugin = "UnityFbxSdkNative";
        private const string fbxsdkNativePluginPath = "Packages/com.autodesk.fbx/Editor/Plugins";
        
        private const string fbxsdkNativePluginExtWin = ".dll";
        private const string fbxsdkNativePluginExtOSX = ".bundle";
        private const string fbxsdkNativePluginExtLinux = ".so";

        private const string buildPluginPathWin = "{0}_Data/Plugins";
        private const string buildPluginPathOSX = "{0}.app/Contents/Plugins";
        private const string buildPluginPathLinux = "{0}_Data/Plugins/x86_64";

        [PostProcessBuild(1)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            var buildPathWithoutExt = Path.ChangeExtension(pathToBuiltProject, null);

            string destPath = null;
            string sourcePath = null;
            string sourcePathExt = null;
            switch (target)
            {
                case BuildTarget.StandaloneWindows64:
                    destPath = string.Format(buildPluginPathWin, buildPathWithoutExt);
                    sourcePathExt = fbxsdkNativePluginExtWin;
                    break;
                case BuildTarget.StandaloneOSX:
                    destPath = string.Format(buildPluginPathOSX, buildPathWithoutExt);
                    // Since the bundle is technically a folder and not a file, need to copy the contents of the bundle
                    destPath = Path.Combine(destPath, fbxsdkNativePlugin + fbxsdkNativePluginExtOSX);
                    sourcePathExt = fbxsdkNativePluginExtOSX;
                    break;
                case BuildTarget.StandaloneLinux64:
                    destPath = string.Format(buildPluginPathLinux, buildPathWithoutExt);
                    sourcePathExt = fbxsdkNativePluginExtLinux;
                    break;
                default:
                    throw new System.PlatformNotSupportedException("FBX SDK not supported on Build Target: " + target);
            }

            if (!string.IsNullOrEmpty(sourcePathExt))
            {
                sourcePath = Path.Combine(fbxsdkNativePluginPath, fbxsdkNativePlugin + sourcePathExt);
            }

            if (string.IsNullOrEmpty(destPath) || string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogWarningFormat("Failed to copy plugin {0} to build folder", fbxsdkNativePlugin);
                return;
            }

            if (!Directory.Exists(destPath))
            {
                Directory.CreateDirectory(destPath);
            }

            if (target == BuildTarget.StandaloneOSX)
            {
                // bundle is technically a folder and gives an error
                // when you try to copy it as a file.
                DirectoryCopy(sourcePath, destPath, true);
            }
            else {
                destPath = Path.Combine(destPath, fbxsdkNativePlugin + sourcePathExt);
                File.Copy(sourcePath, destPath);
            }
        }

        // Taken from: https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }
    }
}
#endif // FBXSDK_RUNTIME