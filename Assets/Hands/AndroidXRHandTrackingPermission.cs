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

#if UNITY_ANDROID && OPEN_BRUSH_ANDROID_XR
using UnityEngine.Android;
#endif

namespace TiltBrush
{
    /// <summary>
    /// Requests the runtime permission required by the Android XR hand subsystem.
    /// </summary>
    /// <remarks>
    /// Enabling XR Hands adds the permission to the Android manifest, but Android XR still
    /// requires the user to grant it at runtime. Keep this separate from the prototype bridge:
    /// permission policy is platform startup behavior, not hand-to-controller translation.
    ///
    /// The build-only symbol prevents ordinary Quest and other Android OpenXR builds from asking
    /// for an Android XR permission merely because they share the XRHands prefab.
    /// </remarks>
    public class AndroidXRHandTrackingPermission : MonoBehaviour
    {
        const string kPermission = "android.permission.HAND_TRACKING";

#if UNITY_ANDROID && OPEN_BRUSH_ANDROID_XR
        void Start()
        {
            if (Permission.HasUserAuthorizedPermission(kPermission))
            {
                return;
            }

            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += permission =>
                Debug.Log($"Android XR hand tracking permission granted: {permission}");
            callbacks.PermissionDenied += permission =>
                Debug.LogWarning($"Android XR hand tracking permission denied: {permission}");
            callbacks.PermissionDeniedAndDontAskAgain += permission =>
                Debug.LogWarning(
                    $"Android XR hand tracking permission permanently denied: {permission}");
            Permission.RequestUserPermission(kPermission, callbacks);
        }
#endif
    }
}
