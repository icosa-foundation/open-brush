using UnityEngine;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Runtime.InteropServices;
using System.Text;

namespace LIV.SDK.Unity
{
    public class CaptureProtocolBridge : CaptureProtocolInterface
    {
#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && UNITY_64

        #region Interop

        //Manually import this function (not publicly declared in NativeSDK)
        [DllImport("LIV_Native", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr LIV_GetBridgeProcAddr(string procName);

        #endregion

#endif
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private delegate IntPtr GetRenderEventFunc_Delegate();
        private GetRenderEventFunc_Delegate GetRenderEventFunc;

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private delegate bool GetIsCaptureActive_Delegate();
        private GetIsCaptureActive_Delegate GetIsCaptureActive;

        // Get a channel object from the compositor
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private delegate IntPtr GetCompositorChannelObject_Delegate(int slot, ulong tag, ulong timestamp);
        private GetCompositorChannelObject_Delegate GetCompositorChannelObject;

        // Get a channel object from our own source channel
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private delegate IntPtr GetChannelObject_Delegate(int slot, ulong tag, ulong timestamp);
        private GetChannelObject_Delegate GetChannelObject;

        // Write an object to our channel
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate int AddObjectToChannel_Delegate(int slot, IntPtr obj, int objectsize, ulong tag);
        public AddObjectToChannel_Delegate AddObjectToChannel;

        // Write an object to the compostor's channel
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private delegate int AddObjectToCompositorChannel_Delegate(int slot, IntPtr obj, int objectsize, ulong tag);
        private AddObjectToCompositorChannel_Delegate AddObjectToCompositorChannel;

        // Add a structure/object to the current frame / Considering if its simpler to combine with AddObjectToChannel with 0 being the frame
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private delegate int AddObjectToFrame_Delegate(IntPtr obj, int objectsize, ulong tag);
        private AddObjectToFrame_Delegate AddObjectToFrame;

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private delegate int AddStringToChannel_Delegate(int slot, IntPtr str, int length, ulong tag);
        private AddStringToChannel_Delegate AddStringToChannel;

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private delegate int addtexture_Delegate(IntPtr sourcetexture, ulong tag);
        private addtexture_Delegate addTexture;

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private delegate IntPtr updateinputframe_Delegate(IntPtr InputFrame);
        private updateinputframe_Delegate updateInputframe;

        private bool _dynamicAlreadyInitialized = false;

        public SDKBridge.ErrorCode Create()
        {
            if (_dynamicAlreadyInitialized)
                return SDKBridge.ErrorCode.ERR_CAPTURE_PROTOCOL_ALREADY_EXISTS;

#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && UNITY_64
            if (!LIV_Native.LIV_Load())
            {
                Debug.Log("LIV Application is either not installed, or installed but has never been ran by user. LIV functionality disabled for this session. Please install LIV from Steam then start the program to continue integration. https://store.steampowered.com/app/755540/LIV/");
                return SDKBridge.ErrorCode.ERR_CAPTURE_PROTOCOL_BRIDGE_LOADING_FAILED;
            }

            try
            {
                GetRenderEventFunc =
                    (GetRenderEventFunc_Delegate)Marshal.GetDelegateForFunctionPointer(
                        LIV_GetBridgeProcAddr("GetRenderEventFunc"), typeof(GetRenderEventFunc_Delegate));
                GetIsCaptureActive =
                    (GetIsCaptureActive_Delegate)Marshal.GetDelegateForFunctionPointer(
                        LIV_GetBridgeProcAddr("LivCaptureIsActive"), typeof(GetIsCaptureActive_Delegate));
                GetCompositorChannelObject =
                    (GetCompositorChannelObject_Delegate)Marshal.GetDelegateForFunctionPointer(
                        LIV_GetBridgeProcAddr("GetCompositorChannelObject"),
                        typeof(GetCompositorChannelObject_Delegate));
                GetChannelObject =
                    (GetChannelObject_Delegate)Marshal.GetDelegateForFunctionPointer(
                        LIV_GetBridgeProcAddr("GetChannelObject"), typeof(GetChannelObject_Delegate));
                AddObjectToChannel =
                    (AddObjectToChannel_Delegate)Marshal.GetDelegateForFunctionPointer(
                        LIV_GetBridgeProcAddr("AddObjectToChannel"), typeof(AddObjectToChannel_Delegate));
                AddObjectToCompositorChannel =
                    (AddObjectToCompositorChannel_Delegate)Marshal.GetDelegateForFunctionPointer(
                        LIV_GetBridgeProcAddr("AddObjectToCompositorChannel"),
                        typeof(AddObjectToCompositorChannel_Delegate));
                AddObjectToFrame =
                    (AddObjectToFrame_Delegate)Marshal.GetDelegateForFunctionPointer(
                        LIV_GetBridgeProcAddr("AddObjectToFrame"), typeof(AddObjectToFrame_Delegate));
                AddStringToChannel =
                    (AddStringToChannel_Delegate)Marshal.GetDelegateForFunctionPointer(
                        LIV_GetBridgeProcAddr("AddStringToChannel"), typeof(AddStringToChannel_Delegate));
                addTexture =
                    (addtexture_Delegate)Marshal.GetDelegateForFunctionPointer(
                        LIV_GetBridgeProcAddr("addtexture"), typeof(addtexture_Delegate));
                updateInputframe = //typo was there before me
                    (updateinputframe_Delegate)Marshal.GetDelegateForFunctionPointer(
                        LIV_GetBridgeProcAddr("updateinputframe"), typeof(updateinputframe_Delegate));
            }
            catch (Exception e)
            {
                Debug.Log(e);
                return SDKBridge.ErrorCode.ERR_CAPTURE_PROTOCOL_BRIDGE_LOADING_FAILED;
            }

            Debug.Log("LIV: Created capture protocol: Bridge");
            _dynamicAlreadyInitialized = true;
            return SDKBridge.ErrorCode.OK;
#else
            _dynamicAlreadyInitialized = false;
            return SDKBridge.ErrorCode.ERR_CAPTURE_PROTOCOL_NOT_CREATED;
#endif
        }

        public SDKBridge.ErrorCode Destroy()
        {
#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && UNITY_64
            GetRenderEventFunc = null;
            GetIsCaptureActive = null;
            GetCompositorChannelObject = null;
            GetChannelObject = null;
            AddObjectToChannel = null;
            AddObjectToCompositorChannel = null;
            AddObjectToFrame = null;
            AddStringToChannel = null;
            addTexture = null;
            updateInputframe = null; //typo was there before me
#endif
            _dynamicAlreadyInitialized = false;
            return SDKBridge.ErrorCode.OK;
        }

        public bool isLoaded {
            get {
                if (!_dynamicAlreadyInitialized)
                {
                    Debug.LogError("LIV: Bridge was not loaded!");
                    return false;
                }

                return true;
            }
        }

        public bool IsConnected(out SDKBridge.ErrorCode errorCode)
        {
            if (!isLoaded)
            {
                errorCode = SDKBridge.ErrorCode.ERR_CAPTURE_PROTOCOL_BRIDGE_NOT_LOADED;
                return false;
            }

            errorCode = SDKBridge.ErrorCode.OK;
            return GetIsCaptureActive();
        }

        public SDKBridge.ErrorCode SubmitApplicationOutput(SDKApplicationOutput applicationOutput)
        {
            if (!isLoaded)
                return SDKBridge.ErrorCode.ERR_CAPTURE_PROTOCOL_BRIDGE_NOT_LOADED;

            AddString("APPNAME", applicationOutput.applicationName, 5);
            AddString("APPVER", applicationOutput.applicationVersion, 5);
            AddString("ENGNAME", applicationOutput.engineName, 5);
            AddString("ENGVER", applicationOutput.engineVersion, 5);
            AddString("GFXAPI", applicationOutput.graphicsAPI, 5);
            AddString("SDKID", applicationOutput.sdkID, 5);
            AddString("SDKVER", applicationOutput.sdkVersion, 5);
            AddString("SUPPORT", applicationOutput.supportedFeatures.ToString(), 5);
            AddString("XRNAME", applicationOutput.xrDeviceName, 5);

            return SDKBridge.ErrorCode.OK;
        }

        public SDKBridge.ErrorCode UpdateInputFrame(ref SDKInputFrame setframe)
        {
            if (!isLoaded)
            {
                Debug.LogError("LIV: unable to update input frame, Bridge was not loaded.");
                return SDKBridge.ErrorCode.ERR_CAPTURE_PROTOCOL_BRIDGE_NOT_LOADED;
            }

            // Pin the object briefly so we can send it to the API without it being accidentally garbage collected
            GCHandle gch = GCHandle.Alloc(setframe, GCHandleType.Pinned);
            IntPtr structPtr = updateInputframe(gch.AddrOfPinnedObject());
            gch.Free();

            if (structPtr == IntPtr.Zero)
            {
                setframe = SDKInputFrame.empty;
                Debug.LogError("LIV: unable to update input frame, bridge object is null.");
                return SDKBridge.ErrorCode.ERR_CAPTURE_PROTOCOL_BRIDGE_ERROR;
            }

            setframe = (SDKInputFrame)Marshal.PtrToStructure(structPtr, typeof(SDKInputFrame));
            return SDKBridge.ErrorCode.OK;
        }

        public SDKBridge.ErrorCode AddTexture(RenderTexture texture, TEXTURE_ID id)
        {
            SDKTexture sdkTexture = new SDKTexture()
            {
                id = id,
                texturePtr = texture.GetNativeTexturePtr(),
                SharedHandle = System.IntPtr.Zero,
                device = SDKUtils.GetDevice(),
                dummy = 0,
                type = TEXTURE_TYPE.COLOR_BUFFER,
                format = TEXTURE_FORMAT.ARGB32,
                colorSpace = SDKUtils.GetColorSpace(texture),
                width = texture.width,
                height = texture.height
            };

            if (!isLoaded)
            {
                Debug.LogError("LIV: unable to add texture, Bridge was not loaded!");
                return SDKBridge.ErrorCode.ERR_CAPTURE_PROTOCOL_BRIDGE_NOT_LOADED;
            }

            string tag = "";
            switch (sdkTexture.id)
            {
                case TEXTURE_ID.BACKGROUND_COLOR_BUFFER_ID:
                    tag = "BGCTEX";
                    break;
                case TEXTURE_ID.FOREGROUND_COLOR_BUFFER_ID:
                    tag = "FGCTEX";
                    break;
                case TEXTURE_ID.OPTIMIZED_COLOR_BUFFER_ID:
                    tag = "OPTTEX";
                    break;
            }

            return AddTexture(sdkTexture, Tag(tag));
        }

        public SDKBridge.ErrorCode CreateFrame(SDKOutputFrame frame)
        {
            if (!isLoaded)
            {
                Debug.LogError("LIV: unable to create frame, Bridge is not loaded.");
                return SDKBridge.ErrorCode.ERR_CAPTURE_PROTOCOL_BRIDGE_NOT_LOADED;
            }

            GCHandle gch = GCHandle.Alloc(frame, GCHandleType.Pinned);
            AddObjectToFrame(gch.AddrOfPinnedObject(), Marshal.SizeOf(frame), Tag("OUTFRAME"));
            gch.Free();

            return SDKBridge.ErrorCode.OK;
        }

        public SDKBridge.ErrorCode SetGroundPlane(SDKPlane groundPlane)
        {
            if (!isLoaded)
            {
                Debug.LogError("LIV: unable to set ground plane, Bridge is not loaded.");
                return SDKBridge.ErrorCode.ERR_CAPTURE_PROTOCOL_BRIDGE_NOT_LOADED;
            }

            int result = AddStructToGlobalChannel<SDKPlane>(ref groundPlane, 2, Tag("SetGND"));
            if (result == 0)
            {
                Debug.LogError($"LIV: Unable to set ground plane, error: {result}");
                return SDKBridge.ErrorCode.ERR_CAPTURE_PROTOCOL_BRIDGE_ERROR;
            }

            return SDKBridge.ErrorCode.OK;
        }

        public SDKBridge.ErrorCode GetResolution(ref SDKResolution sdkResolution)
        {
            if (!isLoaded)
            {
                Debug.LogError($"LIV: unable to get resolution. Bridge is not loaded.");
                return SDKBridge.ErrorCode.ERR_CAPTURE_PROTOCOL_BRIDGE_NOT_LOADED;
            }

            if (!GetStructFromLocalChannel<SDKResolution>(ref sdkResolution, 15, Tag("SDKRes")))
            {
                Debug.LogError($"LIV: unable to get resolution. Bridge error.");
                return SDKBridge.ErrorCode.ERR_CAPTURE_PROTOCOL_BRIDGE_ERROR;
            }

            return SDKBridge.ErrorCode.OK;
        }

        public SDKBridge.ErrorCode IssuePluginEvent()
        {
            if (!isLoaded)
            {
                Debug.LogError($"LIV: unable to IssuePluginEvent. Bridge is not loaded.");
                return SDKBridge.ErrorCode.ERR_CAPTURE_PROTOCOL_BRIDGE_NOT_LOADED;
            }

            GL.IssuePluginEvent(GetRenderEventFunc(), 2);
            return SDKBridge.ErrorCode.OK;
        }

        private void AddString(string tag, string value, int slot)
        {
            if (!isLoaded)
                return;

            var utf8 = Encoding.UTF8;
            byte[] utfBytes = utf8.GetBytes(value);
            GCHandle gch = GCHandle.Alloc(utfBytes, GCHandleType.Pinned);
            AddStringToChannel(slot, Marshal.UnsafeAddrOfPinnedArrayElement(utfBytes, 0), utfBytes.Length, Tag(tag));
            gch.Free();
        }

        private SDKBridge.ErrorCode AddTexture(SDKTexture texture, ulong tag)
        {
            if (!isLoaded)
                return SDKBridge.ErrorCode.ERR_CAPTURE_PROTOCOL_BRIDGE_NOT_LOADED;

            GCHandle gch = GCHandle.Alloc(texture, GCHandleType.Pinned);
            addTexture(gch.AddrOfPinnedObject(), tag);
            gch.Free();
            return SDKBridge.ErrorCode.OK;
        }

        private SDKBridge.ErrorCode GetStructFromGlobalChannel<T>(ref T mystruct, int channel, ulong tag)
        {
            if (!isLoaded)
                return SDKBridge.ErrorCode.ERR_CAPTURE_PROTOCOL_BRIDGE_NOT_LOADED;

            IntPtr structPtr = GetCompositorChannelObject(channel, tag, UInt64.MaxValue);
            if (structPtr == IntPtr.Zero) 
                return SDKBridge.ErrorCode.ERR_CAPTURE_PROTOCOL_BRIDGE_ERROR;

            mystruct = (T)Marshal.PtrToStructure(structPtr, typeof(T));
            return SDKBridge.ErrorCode.OK;
        }

        private int AddStructToGlobalChannel<T>(ref T mystruct, int channel, ulong tag)
        {
            if (!isLoaded)
                return 0;

            GCHandle gch = GCHandle.Alloc(mystruct, GCHandleType.Pinned);
            int output = AddObjectToCompositorChannel(channel, gch.AddrOfPinnedObject(), Marshal.SizeOf(mystruct), tag);
            gch.Free();
            return output;
        }

        private bool GetStructFromLocalChannel<T>(ref T mystruct, int channel, ulong tag)
        {
            if (!isLoaded)
                return false;

            IntPtr structPtr = GetChannelObject(channel, tag, UInt64.MaxValue);
            if (structPtr == IntPtr.Zero) return false;
            mystruct = (T)Marshal.PtrToStructure(structPtr, typeof(T));
            return true;
        }

        private int AddStructToLocalChannel<T>(ref T mystruct, int channel, ulong tag)
        {
            if (!isLoaded)
                return 0;

            GCHandle gch = GCHandle.Alloc(mystruct, GCHandleType.Pinned);
            int output = AddObjectToChannel(channel, gch.AddrOfPinnedObject(), Marshal.SizeOf(mystruct), tag);
            gch.Free();
            return output;
        }

        // Add ANY structure to the current frame
        private void AddStructToFrame<T>(ref T mystruct, ulong tag)
        {
            if (!isLoaded)
                return;

            GCHandle gch = GCHandle.Alloc(mystruct, GCHandleType.Pinned);
            AddObjectToFrame(gch.AddrOfPinnedObject(), Marshal.SizeOf(mystruct), tag);
            gch.Free();
        }

        // Get the tag code for a string - won't win any awards - pre-compute these and use constants.
        private static ulong Tag(string str)
        {
            ulong tag = 0;
            for (int i = 0; i < str.Length; i++)
            {
                if (i == 8) break;
                char c = str[i];
                tag |= (((ulong)(c & 255)) << (i * 8));
            }

            return tag;
        }

    }
}