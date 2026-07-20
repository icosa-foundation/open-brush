using System;
using System.Collections;
using System.Collections.Generic;
using LIV.SDK.Unity;
using UnityEngine;

public class CaptureProtocolMock : CaptureProtocolInterface
{
    public delegate SDKBridge.ErrorCode ErrorDelegate();
    public static event ErrorDelegate _Create;
    public static event ErrorDelegate _Destroy;

    public delegate bool IsConnectedDelegate(out SDKBridge.ErrorCode errorCode);
    public static event IsConnectedDelegate _IsConnected;

    public delegate SDKBridge.ErrorCode SubmitApplicationDelegate(SDKApplicationOutput sdkApplicationOutput);
    public static event SubmitApplicationDelegate _SubmitApplicationOutput;

    public delegate SDKBridge.ErrorCode UpdateInputFrameDelegate(ref SDKInputFrame setframe);
    public static event UpdateInputFrameDelegate _UpdateInputFrame;

    public delegate SDKBridge.ErrorCode AddTextureDelegate(RenderTexture texture, TEXTURE_ID id);
    public static event AddTextureDelegate _AddTexture;

    public delegate SDKBridge.ErrorCode CreateFrameDelegate(SDKOutputFrame frame);
    public static event CreateFrameDelegate _CreateFrame;

    public delegate SDKBridge.ErrorCode SetGroundPlaneDelegate(SDKPlane groundPlane);
    public static event SetGroundPlaneDelegate _SetGroundPlane;

    public delegate SDKBridge.ErrorCode GetResolutionDelegate(ref SDKResolution sdkResolution);
    public static event GetResolutionDelegate _GetResolution;

    public static event ErrorDelegate _IssuePluginEvent;

    private bool _inited = false;
    public SDKBridge.ErrorCode Create()
    {
        if (_inited)
            return SDKBridge.ErrorCode.ERR_CAPTURE_PROTOCOL_ALREADY_EXISTS;

        _inited = true;
        if (_Create != null)
        {
            return _Create.Invoke();
        }

        return SDKBridge.ErrorCode.OK;
    }

    public SDKBridge.ErrorCode Destroy()
    {
        if (!_inited)
            return SDKBridge.ErrorCode.ERR_CAPTURE_PROTOCOL_MOCK_NOT_LOADED;

        if (_Destroy != null)
        {
            return _Destroy.Invoke();
        }

        return SDKBridge.ErrorCode.OK;
    }

    public bool IsConnected(out SDKBridge.ErrorCode errorCode)
    {
        if (_IsConnected != null)
        {
            return _IsConnected.Invoke(out errorCode);
        }

        errorCode = SDKBridge.ErrorCode.OK;
        return true;
    }
    
    public SDKBridge.ErrorCode SubmitApplicationOutput(SDKApplicationOutput applicationOutput)
    {
        if (_SubmitApplicationOutput != null)
        {
            return _SubmitApplicationOutput(applicationOutput);
        }

        return SDKBridge.ErrorCode.OK;
    }

    public SDKBridge.ErrorCode UpdateInputFrame(ref SDKInputFrame setframe)
    {
        if (_UpdateInputFrame != null)
        {
            return _UpdateInputFrame.Invoke(ref setframe);
        }
        return SDKBridge.ErrorCode.OK;
    }

    public SDKBridge.ErrorCode AddTexture(RenderTexture texture, TEXTURE_ID id)
    {
        if (_AddTexture != null)
        {
            return _AddTexture(texture, id);
        }
        return SDKBridge.ErrorCode.OK;
    }

    public SDKBridge.ErrorCode CreateFrame(SDKOutputFrame frame)
    {
        if (_CreateFrame != null)
        {
            return _CreateFrame.Invoke(frame);
        }
        return SDKBridge.ErrorCode.OK;
    }

    public SDKBridge.ErrorCode SetGroundPlane(SDKPlane groundPlane)
    {
        if (_SetGroundPlane != null)
        {
            return _SetGroundPlane.Invoke(groundPlane);
        }

        return SDKBridge.ErrorCode.OK;
    }

    public SDKBridge.ErrorCode GetResolution(ref SDKResolution sdkResolution)
    {
        if (_GetResolution != null)
        {
            return _GetResolution.Invoke(ref sdkResolution);
        }

        return SDKBridge.ErrorCode.OK;
    }

    public SDKBridge.ErrorCode IssuePluginEvent()
    {
        if (_IssuePluginEvent != null)
        {
            return _IssuePluginEvent.Invoke();
        }

        return SDKBridge.ErrorCode.OK;
    }
}
