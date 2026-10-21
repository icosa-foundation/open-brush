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

using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.NativeTypes;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

namespace TiltBrush
{
    /// Passthrough through the core OpenXR XR_ENVIRONMENT_BLEND_MODE_ALPHA_BLEND, for runtimes
    /// that composite the camera feed behind the app's alpha instead of exposing a passthrough
    /// extension: Steam Frame, Android XR, Varjo and others. Opaque stays the default; alpha
    /// blend is only requested while the passthrough environment is active.
#if UNITY_EDITOR
    [OpenXRFeature(UiName = "Open Brush: Environment Blend Passthrough",
        BuildTargetGroups = new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android },
        Company = "Open Brush",
        Desc = "Passthrough via the core OpenXR alpha-blend environment blend mode.",
        Version = "0.1.0",
        FeatureId = FeatureId)]
#endif
    public class EnvironmentBlendPassthroughFeature : OpenXRFeature
    {
        public const string FeatureId = "org.openbrush.openxr.environmentblendpassthrough";

        // XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO
        private const int kPrimaryStereo = 2;

        /// True once the runtime has listed alpha blend among the system's blend modes.
        public static bool AlphaBlendSupported { get; private set; }

        private static ulong s_XrInstance;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate XrResult GetInstanceProcAddrDelegate(
            ulong instance, [MarshalAs(UnmanagedType.LPStr)] string name, out IntPtr function);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate XrResult EnumerateEnvironmentBlendModesDelegate(
            ulong instance, ulong systemId, int viewConfigurationType,
            uint capacityInput, out uint countOutput, [Out] int[] blendModes);

        protected override bool OnInstanceCreate(ulong xrInstance)
        {
            s_XrInstance = xrInstance;
            AlphaBlendSupported = false;
            return true;
        }

        protected override void OnInstanceDestroy(ulong xrInstance)
        {
            s_XrInstance = 0;
            AlphaBlendSupported = false;
        }

        protected override void OnSystemChange(ulong xrSystem)
        {
            AlphaBlendSupported = xrSystem != 0 && RuntimeSupportsAlphaBlend(xrSystem);
            Debug.Log($"[Passthrough] Runtime alpha blend support: {AlphaBlendSupported}");
        }

        /// Requests alpha blend (passthrough) or opaque composition. The runtime applies it on a
        /// later frame, so callers must not read the blend mode back immediately.
        public static void SetPassthrough(bool active)
        {
            if (!AlphaBlendSupported)
            {
                return;
            }
            SetEnvironmentBlendMode(active ? XrEnvironmentBlendMode.AlphaBlend : XrEnvironmentBlendMode.Opaque);
        }

        private static bool RuntimeSupportsAlphaBlend(ulong xrSystem)
        {
            if (s_XrInstance == 0 || xrGetInstanceProcAddr == IntPtr.Zero)
            {
                return false;
            }

            var getInstanceProcAddr = Marshal.GetDelegateForFunctionPointer<GetInstanceProcAddrDelegate>(
                xrGetInstanceProcAddr);
            if (getInstanceProcAddr(s_XrInstance, "xrEnumerateEnvironmentBlendModes",
                    out IntPtr enumeratePtr) != XrResult.Success || enumeratePtr == IntPtr.Zero)
            {
                return false;
            }

            var enumerate = Marshal.GetDelegateForFunctionPointer<EnumerateEnvironmentBlendModesDelegate>(
                enumeratePtr);
            if (enumerate(s_XrInstance, xrSystem, kPrimaryStereo, 0, out uint count, null) !=
                XrResult.Success || count == 0)
            {
                return false;
            }

            var modes = new int[count];
            if (enumerate(s_XrInstance, xrSystem, kPrimaryStereo, count, out count, modes) !=
                XrResult.Success)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                if (modes[i] == (int)XrEnvironmentBlendMode.AlphaBlend)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
