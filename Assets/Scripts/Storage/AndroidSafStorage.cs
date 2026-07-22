// Copyright 2026 The Open Brush Authors
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

using UnityEngine;

namespace TiltBrush
{
    public static class AndroidSafStorage
    {
        private const string kBridgeClass = "foundation.icosa.openbrush.storage.OpenBrushStorageBridge";

        public static bool IsAvailable
        {
            get
            {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
                return Application.platform == RuntimePlatform.Android;
#else
                return false;
#endif
            }
        }

        public static void RequestOpenBrushFolder()
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            bridge.CallStatic("requestOpenBrushFolder", GetActivity());
#endif
        }

        public static bool HasOpenBrushFolder()
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<bool>("hasOpenBrushFolder", GetActivity());
#else
            return true;
#endif
        }

        public static string GetOpenBrushFolderDisplayName()
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<string>("getOpenBrushFolderDisplayName", GetActivity());
#else
            return App.kAppFolderName;
#endif
        }

        public static void ClearOpenBrushFolder()
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            bridge.CallStatic("clearOpenBrushFolder", GetActivity());
#endif
        }

        public static bool EnsureDirectory(string relativePath)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<bool>("ensureDirectory", GetActivity(), relativePath);
#else
            return true;
#endif
        }

        public static bool WriteFileFromPath(string relativePath, string sourcePath, string mimeType)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<bool>(
                "writeFileFromPath", GetActivity(), relativePath, sourcePath, mimeType);
#else
            return true;
#endif
        }

        public static bool CopyDirectoryFromPath(string relativeDestinationPath, string sourceDirectoryPath)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<bool>(
                "copyDirectoryFromPath", GetActivity(), relativeDestinationPath, sourceDirectoryPath);
#else
            return true;
#endif
        }

        public static int StartWriteFileFromPath(string relativePath, string sourcePath, string mimeType)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<int>(
                "startWriteFileFromPath", GetActivity(), relativePath, sourcePath, mimeType);
#else
            return 0;
#endif
        }

        public static int StartCopyDirectoryFromPath(
            string relativeDestinationPath, string sourceDirectoryPath)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<int>(
                "startCopyDirectoryFromPath",
                GetActivity(),
                relativeDestinationPath,
                sourceDirectoryPath);
#else
            return 0;
#endif
        }

        public static int StartCopyDirectoryToPath(
            string relativePath, string destinationDirectoryPath, string[] preservedPaths)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<int>(
                "startCopyDirectoryToPath",
                GetActivity(),
                relativePath,
                destinationDirectoryPath,
                preservedPaths);
#else
            return 0;
#endif
        }
        public static bool IsTransferJobDone(int jobId)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<bool>("isTransferJobDone", jobId);
#else
            return true;
#endif
        }

        public static bool DidTransferJobSucceed(int jobId)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<bool>("didTransferJobSucceed", jobId);
#else
            return true;
#endif
        }

        public static long GetTransferJobBytesDone(int jobId)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<long>("getTransferJobBytesDone", jobId);
#else
            return 0;
#endif
        }

        public static long GetTransferJobBytesTotal(int jobId)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<long>("getTransferJobBytesTotal", jobId);
#else
            return 0;
#endif
        }

        public static string GetTransferJobError(int jobId)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<string>("getTransferJobError", jobId);
#else
            return null;
#endif
        }

        public static void ClearTransferJob(int jobId)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            bridge.CallStatic("clearTransferJob", jobId);
#endif
        }

        public static bool DeleteTreeChild(string relativePath)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<bool>("deleteTreeChild", GetActivity(), relativePath);
#else
            return true;
#endif
        }

        public static string[] ListFiles(string relativePath)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<string[]>("listFiles", GetActivity(), relativePath);
#else
            return new string[0];
#endif
        }

        public static bool CopyFileToPath(string relativePath, string destinationPath)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<bool>(
                "copyFileToPath", GetActivity(), relativePath, destinationPath);
#else
            return true;
#endif
        }

        public static bool CopyDirectoryToPath(string relativePath, string destinationDirectoryPath)
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            using var bridge = new AndroidJavaClass(kBridgeClass);
            return bridge.CallStatic<bool>(
                "copyDirectoryToPath", GetActivity(), relativePath, destinationDirectoryPath);
#else
            return true;
#endif
        }

#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
        private static AndroidJavaObject GetActivity()
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }
#endif
    }
}
