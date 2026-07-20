using UnityEngine;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Runtime.InteropServices;
using System.Text;

namespace LIV.SDK.Unity
{
    //#define UNITY_64;
    public static class SDKBridge
    {
        public enum ErrorCode : UInt32
        {
            OK = 0,
            GROUP_CAPTURE_PROTOCOL = 0x00010000,
            GROUP_CAPTURE_PROTOCOL_BRIDGE = 0x00020000,
            GROUP_CAPTURE_PROTOCOL_MOCK = 0x00030000,

            ERR_CAPTURE_PROTOCOL_NOT_CREATED = GROUP_CAPTURE_PROTOCOL | 0x00000001,
            ERR_CAPTURE_PROTOCOL_ALREADY_EXISTS = GROUP_CAPTURE_PROTOCOL | 0x00000002,
            ERR_CAPTURE_PROTOCOL_FACTORY_INVALID_VALUE = GROUP_CAPTURE_PROTOCOL | 0x00000003,

            ERR_CAPTURE_PROTOCOL_BRIDGE_LOADING_FAILED = GROUP_CAPTURE_PROTOCOL_BRIDGE | 0x00000001,
            ERR_CAPTURE_PROTOCOL_BRIDGE_NOT_LOADED = GROUP_CAPTURE_PROTOCOL_BRIDGE | 0x00000002,
            ERR_CAPTURE_PROTOCOL_BRIDGE_ERROR = GROUP_CAPTURE_PROTOCOL_BRIDGE | 0x00000003,

            ERR_CAPTURE_PROTOCOL_MOCK_NOT_LOADED = GROUP_CAPTURE_PROTOCOL_MOCK | 0x00000001,
        }

        public enum CaptureProtocolType : UInt32
        {
            BRIDGE = 0,
            MOCK = 1,
        }

        private static CaptureProtocolInterface _captureProtocolInterface = null;

        public static ErrorCode CreateCaptureProtocol(CaptureProtocolType captureProtocolType)
        {
            switch (captureProtocolType)
            {
                case CaptureProtocolType.BRIDGE:
                    _captureProtocolInterface = new CaptureProtocolBridge();
                    return _captureProtocolInterface.Create();
                case CaptureProtocolType.MOCK:
                    _captureProtocolInterface = new CaptureProtocolMock();
                    return _captureProtocolInterface.Create();
            }
            return ErrorCode.ERR_CAPTURE_PROTOCOL_NOT_CREATED;
        }

        public static bool IsConnected(out SDKBridge.ErrorCode errorCode) {
            return _captureProtocolInterface.IsConnected(out errorCode);
        }

        public static SDKBridge.ErrorCode IssuePluginEvent()
        {
            return _captureProtocolInterface.IssuePluginEvent();
        }

        public static SDKBridge.ErrorCode SubmitApplicationOutput(SDKApplicationOutput applicationOutput)
        {
            return _captureProtocolInterface.SubmitApplicationOutput(applicationOutput);
        }

        /// <summary>
        /// Update the master pose sent to ALL applications.
        ///
        /// when called initialy, having the flags set to 0 will return the current pose (which includes resolution - which you might need)
        /// If you wish to change the pose, change the parts of the structures you need to, and set the appropriate flag to update.
        /// atm, the flags will be for Pose, Stage, Clipping Plane, and resolution.
        ///
        /// </summary>
        /// <param name="setFrame"></param>
        /// <returns>The current pose - could be yours, someone elses, or a combination</returns>

        public static SDKBridge.ErrorCode UpdateInputFrame(ref SDKInputFrame setFrame)
        {
            return _captureProtocolInterface.UpdateInputFrame(ref setFrame);
        }

        public static SDKBridge.ErrorCode AddTexture(RenderTexture texture, TEXTURE_ID id)
        {
            return _captureProtocolInterface.AddTexture(texture, id);
        }

        public static SDKBridge.ErrorCode CreateFrame(SDKOutputFrame frame)
        {
            return _captureProtocolInterface.CreateFrame(frame);
        }

        public static SDKBridge.ErrorCode SetGroundPlane(SDKPlane groundPlane)
        {
            return _captureProtocolInterface.SetGroundPlane(groundPlane);
        }

        public static SDKBridge.ErrorCode GetResolution(ref SDKResolution sdkResolution)
        {
            return _captureProtocolInterface.GetResolution(ref sdkResolution);
        }

        public static SDKBridge.ErrorCode DestroyCaptureProtocol()
        {
            return _captureProtocolInterface.Destroy();
        }

    }
}
