// Copyright 2020 The Tilt Brush Authors
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

using System;

namespace OpenBrush.TiltFile
{
    /// <summary>
    /// Abstraction for logging to decouple from Unity's Debug class
    /// </summary>
    public interface ITiltFileLogger
    {
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message);
        void LogFormat(string format, params object[] args);
        void LogWarningFormat(string format, params object[] args);
        void LogErrorFormat(string format, params object[] args);
    }

    /// <summary>
    /// Default logger that does nothing (silent)
    /// </summary>
    public class NullLogger : ITiltFileLogger
    {
        public static readonly NullLogger Instance = new NullLogger();

        public void Log(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message) { }
        public void LogFormat(string format, params object[] args) { }
        public void LogWarningFormat(string format, params object[] args) { }
        public void LogErrorFormat(string format, params object[] args) { }
    }

#if UNITY_5_3_OR_NEWER
    /// <summary>
    /// Unity-specific logger implementation
    /// </summary>
    public class UnityLogger : ITiltFileLogger
    {
        public static readonly UnityLogger Instance = new UnityLogger();

        public void Log(string message) => UnityEngine.Debug.Log(message);
        public void LogWarning(string message) => UnityEngine.Debug.LogWarning(message);
        public void LogError(string message) => UnityEngine.Debug.LogError(message);
        public void LogFormat(string format, params object[] args) => UnityEngine.Debug.LogFormat(format, args);
        public void LogWarningFormat(string format, params object[] args) => UnityEngine.Debug.LogWarningFormat(format, args);
        public void LogErrorFormat(string format, params object[] args) => UnityEngine.Debug.LogErrorFormat(format, args);
    }
#endif
}
