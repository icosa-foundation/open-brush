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

using System;
using System.Collections;
using TiltBrush;
using TMPro;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public class WebcamSurface : MonoBehaviour
{
    public int deviceIndex = 0;
    public MeshRenderer Screen;
    public Transform Pivot;
    public TextMeshPro DeviceNameLabel;
    public ActionButton PreviousDeviceButton;
    public ActionButton NextDeviceButton;

    private WebCamDevice[] _devices = Array.Empty<WebCamDevice>();
    private WebCamTexture _webcam;
    private Material _screenMaterial;
    private Texture _originalTexture;
    private Vector2 _originalTextureScale;
    private Vector2 _originalTextureOffset;
    private Quaternion _screenRotation;
    private bool _started;
    private bool _initializing;
    private bool _paused;
    private bool _resumeCamera;

    private void Awake()
    {
        _screenMaterial = Screen.material;
        _originalTexture = _screenMaterial.mainTexture;
        _originalTextureScale = _screenMaterial.mainTextureScale;
        _originalTextureOffset = _screenMaterial.mainTextureOffset;
        _screenRotation = Screen.transform.localRotation;
    }

    private void Start()
    {
        _started = true;
        StartCamera();
    }

    private void OnEnable()
    {
        if (_started)
        {
            StartCamera();
        }
    }

    private void StartCamera()
    {
        StopCamera();
        _resumeCamera = false;
#if UNITY_IOS && !UNITY_EDITOR
        DeviceNameLabel.text = "Camera unavailable on this platform";
#else
        _initializing = true;
        DeviceNameLabel.text = "Starting camera...";
        UpdateButtonState();
        StartCoroutine(InitializeCamera());
#endif
    }

    private IEnumerator InitializeCamera()
    {
        try
        {
            // Allow the previous texture to be destroyed before opening another camera.
            yield return null;
            while (_paused)
            {
                yield return null;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            yield return RequestAndroidPermission(Permission.Camera);
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                DeviceNameLabel.text = "Camera permission denied";
                yield break;
            }
            if (RequiresHeadsetCameraPermission())
            {
                const string headsetPermission = "horizonos.permission.HEADSET_CAMERA";
                yield return RequestAndroidPermission(headsetPermission);
                if (!Permission.HasUserAuthorizedPermission(headsetPermission))
                {
                    DeviceNameLabel.text = "Headset camera permission denied";
                    yield break;
                }
            }
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                DeviceNameLabel.text = "Camera permission denied";
                yield break;
            }
#endif
            // Permission dialogs can suspend the app. Open the camera only after returning.
            while (_paused)
            {
                yield return null;
            }
            if (!TryCreateCamera())
            {
                yield break;
            }
            // Quest's WebCamTexture path needs a frame between construction and Play.
            yield return null;
            if (!TryStartCamera())
            {
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + 10f;
            while (!_webcam.didUpdateThisFrame || _webcam.width <= 16 || _webcam.height <= 16)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Debug.LogWarning($"[OB_WEBCAM] No frames from camera {_webcam.deviceName}");
                    ReleaseCamera();
                    DeviceNameLabel.text = "Camera did not start";
                    yield break;
                }
                yield return null;
            }
            DeviceNameLabel.text = _webcam.deviceName;
            Debug.Log($"[OB_WEBCAM] Started {_webcam.deviceName}: {_webcam.width}x{_webcam.height}, rotation={_webcam.videoRotationAngle}, mirrored={_webcam.videoVerticallyMirrored}");
            UpdateDisplay();
        }
        finally
        {
            _initializing = false;
            UpdateButtonState();
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private IEnumerator RequestAndroidPermission(string permission)
    {
        if (Permission.HasUserAuthorizedPermission(permission))
        {
            yield break;
        }
        bool completed = false;
        var callbacks = new PermissionCallbacks();
        callbacks.PermissionGranted += _ => completed = true;
        callbacks.PermissionDenied += _ => completed = true;
        Permission.RequestUserPermission(permission, callbacks);
        while (!completed)
        {
            yield return null;
        }
        // Unity requires a frame after granting permission before camera initialization.
        yield return null;
        while (_paused || !Application.isFocused)
        {
            yield return null;
        }
    }

    private static bool RequiresHeadsetCameraPermission()
    {
        using var build = new AndroidJavaClass("android.os.Build");
        string manufacturer = build.GetStatic<string>("MANUFACTURER");
        return string.Equals(manufacturer, "Oculus", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(manufacturer, "Meta", StringComparison.OrdinalIgnoreCase);
    }
#endif

    private bool TryCreateCamera()
    {
        try
        {
            _devices = WebCamTexture.devices;
            if (_devices.Length == 0)
            {
                DeviceNameLabel.text = "No cameras available";
                return false;
            }
            deviceIndex = Mathf.Clamp(deviceIndex, 0, _devices.Length - 1);
#if UNITY_ANDROID && !UNITY_EDITOR
            // A panel needs a preview, not the camera's highest capture resolution.
            _webcam = new WebCamTexture(_devices[deviceIndex].name, 640, 480, 30);
#else
            _webcam = new WebCamTexture(_devices[deviceIndex].name);
#endif
            _screenMaterial.mainTexture = _webcam;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[OB_WEBCAM] Could not open camera: {exception.Message}");
            ReleaseCamera();
            DeviceNameLabel.text = "Camera unavailable";
            return false;
        }
    }

    private bool TryStartCamera()
    {
        try
        {
            _webcam.Play();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[OB_WEBCAM] Could not start camera: {exception.Message}");
            ReleaseCamera();
            DeviceNameLabel.text = "Camera unavailable";
            return false;
        }
    }

    private void Update()
    {
        if (_webcam != null && _webcam.didUpdateThisFrame && _webcam.width > 16 && _webcam.height > 16)
        {
            UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        int rotation = _webcam.videoRotationAngle;
        float aspectRatio = _webcam.height / (float)_webcam.width;
        Pivot.localScale = new Vector3(1, rotation % 180 == 0 ? aspectRatio : 1 / aspectRatio, 1);
        Screen.transform.localRotation = _screenRotation * Quaternion.Euler(0, 0, -rotation);
        bool mirrored = _webcam.videoVerticallyMirrored;
        _screenMaterial.mainTextureScale = new Vector2(_originalTextureScale.x,
            mirrored ? -_originalTextureScale.y : _originalTextureScale.y);
        _screenMaterial.mainTextureOffset = _originalTextureOffset +
            (mirrored ? new Vector2(0, _originalTextureScale.y) : Vector2.zero);
    }

    private void UpdateButtonState()
    {
        if (NextDeviceButton != null)
        {
            NextDeviceButton.gameObject.SetActive(!_initializing && deviceIndex < _devices.Length - 1);
        }
        if (PreviousDeviceButton != null)
        {
            PreviousDeviceButton.gameObject.SetActive(!_initializing && deviceIndex > 0 && _devices.Length > 0);
        }
    }

    private void OnApplicationPause(bool paused)
    {
        _paused = paused;
        if (paused && _webcam != null)
        {
            StopCamera();
            _resumeCamera = true;
        }
        else if (!paused && _resumeCamera && isActiveAndEnabled)
        {
            StartCamera();
        }
    }

    private void OnDisable()
    {
        StopCamera();
        _resumeCamera = false;
    }

    private void OnDestroy()
    {
        ReleaseCamera();
        if (_screenMaterial != null)
        {
            Destroy(_screenMaterial);
        }
    }

    private void StopCamera()
    {
        StopAllCoroutines();
        _initializing = false;
        ReleaseCamera();
        UpdateButtonState();
    }

    private void ReleaseCamera()
    {
        if (_webcam != null)
        {
            _webcam.Stop();
            Destroy(_webcam);
            _webcam = null;
        }
        if (_screenMaterial != null)
        {
            _screenMaterial.mainTexture = _originalTexture;
            _screenMaterial.mainTextureScale = _originalTextureScale;
            _screenMaterial.mainTextureOffset = _originalTextureOffset;
        }
    }

    public void HandleChangeDevice(int increment)
    {
        if (_initializing || _devices.Length == 0 || !isActiveAndEnabled)
        {
            return;
        }
        deviceIndex = Mathf.Clamp(deviceIndex + increment, 0, _devices.Length - 1);
        StartCamera();
    }
}
