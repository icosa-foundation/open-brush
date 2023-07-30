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
using System.Collections.Generic;
using TiltBrush;
using TMPro;
using UnityEngine;

public class WebcamSurface : MonoBehaviour
{
    public int deviceIndex = 0;
    public MeshRenderer Screen;
    public Transform Pivot;
    public TextMeshPro DeviceNameLabel;
    public ActionButton PreviousDeviceButton;
    public ActionButton NextDeviceButton;

    private WebCamDevice[] _Devices;
    private WebCamTexture _webcam;

    void Start()
    {
        _Devices = WebCamTexture.devices;
        UpdateButtonState();
        UpdateDeviceInCompositor();
    }

    private void UpdateDeviceInCompositor()
    {
        StartCoroutine(
            OverlayManager.m_Instance.RunInCompositor(
                OverlayType.LoadGeneric,
                UpdateDevice,
                0.25f
            )
        );
    }

    private void UpdateDevice()
    {
        if (_webcam != null)
        {
            _webcam.Stop();
            Destroy(_webcam);
        }
        var device = _Devices[deviceIndex];
        DeviceNameLabel.text = device.name;
        Application.RequestUserAuthorization(UserAuthorization.WebCam);
        _webcam = new WebCamTexture(device.name);
        Screen.material.mainTexture = _webcam;
        _webcam.Play();
        // Note that it's height / width, not width / height
        var aspectRatio = _webcam.height / (float)_webcam.width;
        Pivot.localScale = new Vector3(1, aspectRatio, 1);
    }

    public void HandleChangeDevice(int increment)
    {
        deviceIndex += increment;
        deviceIndex = Mathf.Clamp(deviceIndex, 0, _Devices.Length - 1);
        UpdateButtonState();
        UpdateDeviceInCompositor();
    }

    private void UpdateButtonState()
    {
        NextDeviceButton.gameObject.SetActive(deviceIndex < _Devices.Length - 1);
        PreviousDeviceButton.gameObject.SetActive(deviceIndex > 0);
    }

    private void OnDestroy()
    {
        _webcam.Stop();
        Destroy(_webcam);
    }
}
