using UnityEngine;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace LIV.SDK.Unity
{
    public static class SDKBridge
    {
        #if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && UNITY_64
        #region Interop

        [DllImport("LIV_Bridge")]
        static extern IntPtr GetRenderEventFunc();

        [DllImport("LIV_Bridge", EntryPoint = "LivCaptureIsActive")]
        [return: MarshalAs(UnmanagedType.U1)]
        static extern bool GetIsCaptureActive();

        [DllImport("LIV_Bridge", EntryPoint = "LivCaptureWidth")]
        static extern int GetTextureWidth();

        [DllImport("LIV_Bridge", EntryPoint = "LivCaptureHeight")]
        static extern int GetTextureHeight();

        [DllImport("LIV_Bridge", EntryPoint = "LivCaptureSetTextureFromUnity")]
        static extern void SetTexture(IntPtr texture);

        //// Acquire a frame from the compositor, allowing atomic access to its properties - most current one by default
        [DllImport("LIV_Bridge", EntryPoint = "AcquireCompositorFrame")]
        public static extern int AcquireCompositorFrame(ulong timestamp);

        [DllImport("LIV_Bridge", EntryPoint = "ReleaseCompositorFrame")]
        public static extern int ReleaseCompositorFrame();

        // Get timestamp of SDK2 object (C# timestamp) - must be an object in the bridge, not a copy.
        [DllImport("LIV_Bridge", EntryPoint = "GetObjectTimeStamp")]
        public static extern ulong GetObjectTimeStamp(IntPtr obj);

        // Get current time in C# ticks
        [DllImport("LIV_Bridge", EntryPoint = "GetCurrentTimeTicks")]
        static extern ulong GetCurrentTimeTicks();

        // Get object tag of SDK2 object - must be an object in the bridge, not a copy.
        [DllImport("LIV_Bridge", EntryPoint = "GetObjectTag")]
        public static extern ulong GetObjectTag(IntPtr obj);

        // Get a frame object from the compositor
        [DllImport("LIV_Bridge", EntryPoint = "GetCompositorFrameObject")]
        public static extern IntPtr GetCompositorFrameObject(ulong tag);

        // Get a frame object from the compositor
        [DllImport("LIV_Bridge", EntryPoint = "GetViewportTexture")]
        public static extern IntPtr GetViewportTexture();

        // Get a channel object from the compositor
        [DllImport("LIV_Bridge", EntryPoint = "GetCompositorChannelObject")]
        public static extern IntPtr GetCompositorChannelObject(int slot, ulong tag, ulong timestamp);

        // Get a channel object from our own source channel
        [DllImport("LIV_Bridge", EntryPoint = "GetChannelObject")]
        public static extern IntPtr GetChannelObject(int slot, ulong tag, ulong timestamp);

        // Write an object to our channel
        [DllImport("LIV_Bridge", EntryPoint = "AddObjectToChannel")]
        public static extern int AddObjectToChannel(int slot, IntPtr obj, int objectsize, ulong tag);

        // Write an object to the compostor's channel
        [DllImport("LIV_Bridge", EntryPoint = "AddObjectToCompositorChannel")]
        public static extern int AddObjectToCompositorChannel(int slot, IntPtr obj, int objectsize, ulong tag);

        // Add a structure/object to the current frame / Considering if its simpler to combine with AddObjectToChannel with 0 being the frame
        [DllImport("LIV_Bridge", EntryPoint = "AddObjectToFrame")]
        public static extern int AddObjectToFrame(IntPtr obj, int objectsize, ulong tag);

        // Helper to add strings 
        [DllImport("LIV_Bridge", EntryPoint = "AddObjectToFrame")]
        public static extern int AddStringToFrame(IntPtr str, ulong tag);

        [DllImport("LIV_Bridge", EntryPoint = "AddStringToChannel")]
        public static extern int AddStringToChannel(int slot, IntPtr str, int length, ulong tag);

        // Create a new frame for rendering / native code does this already - so probably don't use
        [DllImport("LIV_Bridge", EntryPoint = "NewFrame")]
        public static extern int NewFrame();

        // Commit the frame early - not recommended - best to let the next NewFrame commit the frame to avoid pipeline stalls
        [DllImport("LIV_Bridge", EntryPoint = "CommitFrame")]
        public static extern IntPtr CommitFrame();

        // Add a copy of a unity texture to the bridge
        [DllImport("LIV_Bridge", EntryPoint = "addsharedtexture")]
        public static extern int addsharedtexture(int width, int height, int format, IntPtr sourcetexture, ulong tag);

        [DllImport("LIV_Bridge", EntryPoint = "addtexture")]
        public static extern int addtexture(IntPtr sourcetexture, ulong tag);

        [DllImport("LIV_Bridge", EntryPoint = "PublishTextures")]
        public static extern void PublishTextures();

        [DllImport("LIV_Bridge", EntryPoint = "updateinputframe")]
        public static extern IntPtr updatinputframe(IntPtr InputFrame);

        [DllImport("LIV_Bridge", EntryPoint = "setinputframe")]
        public static extern IntPtr setinputframe(float x, float y, float z, float q0, float q1, float q2, float q3, float fov, int priority);

        [DllImport("LIV_Bridge", EntryPoint = "setfeature")]
        public static extern ulong setfeature(ulong feature);

        [DllImport("LIV_Bridge", EntryPoint = "clearfeature")]
        public static extern ulong clearfeature(ulong feature);
        #endregion
        #else
        public static int AddStringToChannel(int slot, IntPtr str, int length, ulong tag) { return -2; }
        public static int addtexture(IntPtr sourcetexture, ulong tag) { return -2; }
        public static ulong GetObjectTimeStamp(IntPtr obj) { return 0; }
        public static ulong GetCurrentTimeTicks() { return 0; }
        static bool GetIsCaptureActive() { return false; }
        public static IntPtr GetRenderEventFunc() { return IntPtr.Zero; }
        public static IntPtr GetCompositorChannelObject(int slot, ulong tag, ulong timestamp) { return IntPtr.Zero; }
        public static int AddObjectToCompositorChannel(int slot, IntPtr obj, int objectsize, ulong tag) { return -2; }
        public static int AddObjectToFrame(IntPtr obj, int objectsize, ulong tag) { return -2; }
        public static IntPtr updatinputframe(IntPtr InputFrame) { return IntPtr.Zero; }
        public static IntPtr GetViewportTexture() { return IntPtr.Zero; }
        public static IntPtr GetChannelObject(int slot, ulong tag, ulong timestamp) { return IntPtr.Zero; }
        public static int AddObjectToChannel(int slot, IntPtr obj, int objectsize, ulong tag) { return -2; }
        #endif

        public struct SDKInjection<T>
        {
            public bool active;            
            public System.Action action;
            public T data;
        }

        static SDKInjection<SDKInputFrame> _injection_SDKInputFrame = new SDKInjection<SDKInputFrame>()
        {
            active = false,            
            action = null,
            data = SDKInputFrame.empty
        };

        static SDKInjection<SDKResolution> _injection_SDKResolution = new SDKInjection<SDKResolution>()
        {
            active = false,            
            action = null,
            data = SDKResolution.zero
        };

        static SDKInjection<bool> _injection_IsActive = new SDKInjection<bool>()
        {
            active = false,            
            action = null,
            data = false
        };

        static bool _injection_DisableSubmit = false;
        static bool _injection_DisableSubmitApplicationOutput = false;
        static bool _injection_DisableAddTexture = false;
        static bool _injection_DisableCreateFrame = false;
        
        // Get the tag code for a string - won't win any awards - pre-compute these and use constants.
        public static ulong Tag(string str)
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

        public static void AddString(string tag, string value, int slot)
        {
            var utf8 = Encoding.UTF8;
            byte[] utfBytes = utf8.GetBytes(value);
            GCHandle gch = GCHandle.Alloc(utfBytes, GCHandleType.Pinned);
            AddStringToChannel(slot, Marshal.UnsafeAddrOfPinnedArrayElement(utfBytes, 0), utfBytes.Length, Tag(tag));
            gch.Free();
        }

        public static void AddTexture(SDKTexture texture, ulong tag)
        {
            GCHandle gch = GCHandle.Alloc(texture, GCHandleType.Pinned);
            addtexture(gch.AddrOfPinnedObject(), tag);
            gch.Free();
        }

        public static ulong GetObjectTime(IntPtr objectptr)
        {
            return GetObjectTimeStamp(objectptr) + 621355968000000000;
        }

        public static ulong GetCurrentTime()
        {
            return GetCurrentTimeTicks() + 621355968000000000;
        }

        public static bool IsActive {
            get {
                if (_injection_IsActive.active)
                {
                    return _injection_IsActive.data;
                }
                return GetIsCaptureActive();
            }
        }

        public static void IssuePluginEvent()
        {
            if (_injection_DisableSubmit) return;
            GL.IssuePluginEvent(GetRenderEventFunc(), 2);
        }

        public static void SubmitApplicationOutput(SDKApplicationOutput applicationOutput)
        {
            if (_injection_DisableSubmitApplicationOutput) return;
            AddString("APPNAME", applicationOutput.applicationName, 5);
            AddString("APPVER", applicationOutput.applicationVersion, 5);
            AddString("ENGNAME", applicationOutput.engineName, 5);
            AddString("ENGVER", applicationOutput.engineVersion, 5);
            AddString("GFXAPI", applicationOutput.graphicsAPI, 5);
            AddString("SDKID", applicationOutput.sdkID, 5);
            AddString("SDKVER", applicationOutput.sdkVersion, 5);
            AddString("SUPPORT", applicationOutput.supportedFeatures.ToString(), 5);
            AddString("XRNAME", applicationOutput.xrDeviceName, 5);
        }

        public static bool GetStructFromGlobalChannel <T> ( ref T mystruct, int channel, ulong tag)  
        {
            IntPtr structPtr = GetCompositorChannelObject(channel, tag, UInt64.MaxValue);
            if (structPtr == IntPtr.Zero) return false;
            mystruct=  (T)Marshal.PtrToStructure(structPtr, typeof(T));
            return true;
        }

        public static int AddStructToGlobalChannel<T>(ref T mystruct, int channel, ulong tag)
        {
            GCHandle gch = GCHandle.Alloc(mystruct, GCHandleType.Pinned);
            int output = AddObjectToCompositorChannel(channel, gch.AddrOfPinnedObject(), Marshal.SizeOf(mystruct), tag);
            gch.Free();
            return output;
        }

        public static bool GetStructFromLocalChannel<T>(ref T mystruct, int channel, ulong tag)
        {
            IntPtr structPtr = GetChannelObject(channel, tag, UInt64.MaxValue);
            if (structPtr == IntPtr.Zero) return false;
            mystruct = (T)Marshal.PtrToStructure(structPtr, typeof(T));
            return true;
        }

        public static int AddStructToLocalChannel<T>(ref T mystruct, int channel, ulong tag)
        {
            GCHandle gch = GCHandle.Alloc(mystruct, GCHandleType.Pinned);
            int output = AddObjectToChannel(channel, gch.AddrOfPinnedObject(), Marshal.SizeOf(mystruct), tag);
            gch.Free();
            return output;
        }

        // Add ANY structure to the current frame
        public static void AddStructToFrame<T>(ref T mystruct, ulong tag)
        {
            GCHandle gch = GCHandle.Alloc(mystruct, GCHandleType.Pinned);
            AddObjectToFrame(gch.AddrOfPinnedObject(), Marshal.SizeOf(mystruct), tag);
            gch.Free();
        }


        /// <summary>
        /// Update the master pose sent to ALL applications. 
        /// 
        /// when called initialy, having the flags set to 0 will return the current pose (which includes resolution - which you might need)
        /// If you wish to change the pose, change the parts of the structures you need to, and set the appropriate flag to update.
        /// atm, the flags will be for Pose, Stage, Clipping Plane, and resolution.
        /// 
        /// </summary>
        /// <param name="setframe"></param>
        /// <returns>The current pose - could be yours, someone elses, or a combination</returns>

        public static bool UpdateInputFrame(ref SDKInputFrame setframe)
        {
            if (_injection_SDKInputFrame.active && _injection_SDKInputFrame.action != null)
            {
                _injection_SDKInputFrame.action.Invoke();
                setframe = _injection_SDKInputFrame.data;
            }
            else
            {
                // Pin the object briefly so we can send it to the API without it being accidentally garbage collected
                GCHandle gch = GCHandle.Alloc(setframe, GCHandleType.Pinned);
                IntPtr structPtr = updatinputframe(gch.AddrOfPinnedObject());
                gch.Free();

                if (structPtr == IntPtr.Zero)
                {
                    setframe = SDKInputFrame.empty;
                    return false;
                }

                setframe = (SDKInputFrame)Marshal.PtrToStructure(structPtr, typeof(SDKInputFrame));
                _injection_SDKInputFrame.data = setframe;
            }

            return true;
        }

        public static SDKTexture GetViewfinderTexture()
        {
            SDKTexture overlaytexture = SDKTexture.empty;
            IntPtr structPtr = GetCompositorChannelObject(11, Tag("OUTTEX"), UInt64.MaxValue);
            if (structPtr == IntPtr.Zero) return new SDKTexture();
            overlaytexture = (SDKTexture)Marshal.PtrToStructure(structPtr, typeof(SDKTexture));
            return overlaytexture;
        }

        public static void AddTexture(SDKTexture texture)
        {
            if (_injection_DisableAddTexture) return;
            string tag = "";
            switch (texture.id)
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
            AddTexture(texture, Tag(tag));
        }

        public static void CreateFrame(SDKOutputFrame frame)
        {
            if (_injection_DisableCreateFrame) return;
            GCHandle gch = GCHandle.Alloc(frame, GCHandleType.Pinned);
            AddObjectToFrame(gch.AddrOfPinnedObject(), Marshal.SizeOf(frame), Tag("OUTFRAME"));
            gch.Free();
        }

        public static void SetGroundPlane(SDKPlane groundPlane)
        {
            AddStructToGlobalChannel<SDKPlane>(ref groundPlane, 2, SDKBridge.Tag("SetGND"));
        }

        public static bool GetResolution(ref SDKResolution sdkResolution)
        {
            if(_injection_SDKResolution.active && _injection_SDKResolution.action != null)
            {
                _injection_SDKResolution.action.Invoke();
                sdkResolution = _injection_SDKResolution.data;
                return true;
            }

            bool output = GetStructFromLocalChannel<SDKResolution>(ref sdkResolution, 15, SDKBridge.Tag("SDKRes"));
            _injection_SDKResolution.data = sdkResolution;
            return output;
        }
    }
}
