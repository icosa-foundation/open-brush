using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LIV.SDK.Unity
{
    public interface CaptureProtocolInterface
    {
        SDKBridge.ErrorCode Create();
        SDKBridge.ErrorCode Destroy();
        bool IsConnected(out SDKBridge.ErrorCode errorCode);
        SDKBridge.ErrorCode SubmitApplicationOutput(SDKApplicationOutput applicationOutput);
        SDKBridge.ErrorCode UpdateInputFrame(ref SDKInputFrame setframe);
        SDKBridge.ErrorCode AddTexture(RenderTexture texture, TEXTURE_ID id);
        SDKBridge.ErrorCode CreateFrame(SDKOutputFrame frame);
        SDKBridge.ErrorCode SetGroundPlane(SDKPlane groundPlane);
        SDKBridge.ErrorCode GetResolution(ref SDKResolution sdkResolution);
        SDKBridge.ErrorCode IssuePluginEvent();
    }
}