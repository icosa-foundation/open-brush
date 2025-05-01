// Copyright 2024 The Open Brush Authors
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
using System;

namespace TiltBrush
{

    public class RamLogger : MonoBehaviour
    {
        [SerializeField] private TMPro.TextMeshPro m_Text;

        void Update()
        {
            if (App.Instance.RamLoggingActive)
            {
                ClearRamLog();
                if (Application.platform == RuntimePlatform.Android)
                {
                    using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var activityManager = activity.Call<AndroidJavaObject>("getSystemService", "activity"))
                    using (var memoryInfo = new AndroidJavaObject("android.app.ActivityManager$MemoryInfo"))
                    {
                        activityManager.Call("getMemoryInfo", memoryInfo);

                        long availMem = memoryInfo.Get<long>("availMem");
                        long totalMem = memoryInfo.Get<long>("totalMem");
                        long threshold = memoryInfo.Get<long>("threshold");

                        long usedMem = totalMem - availMem;

                        AddRamLogMessage($"Total Memory: {totalMem / (1024.0 * 1024.0):F2} MB");
                        AddRamLogMessage($"Available Memory: {availMem / (1024.0 * 1024.0):F2} MB");
                        AddRamLogMessage($"Used Memory: {usedMem / (1024.0 * 1024.0):F2} MB");
                        AddRamLogMessage($"Low Memory Threshold: {threshold / (1024.0 * 1024.0):F2} MB");

                        // Compare app memory usage
                        long appMemoryUsage = System.GC.GetTotalMemory(false);
                        AddRamLogMessage($"App Memory Usage: {appMemoryUsage / (1024.0 * 1024.0):F2} MB");

                        if (availMem < threshold)
                        {
                            AddRamLogMessage("Device is running low on memory. App may be terminated soon.");
                        }
                    }
                }
                else
                {
                    AddRamLogMessage("Ram logging is Android only.");
                }
            }
        }
        private void AddRamLogMessage(string text)
        {
            m_Text.text += $"{text}\n";
        }

        private void ClearRamLog()
        {
            m_Text.text = "";
        }
    }
} // namespace TiltBrush
