/************************************************************************************
Copyright : Copyright (c) Facebook Technologies, LLC and its affiliates. All rights reserved.

Licensed under the Oculus Utilities SDK License Version 1.31 (the "License"); you may not use
the Utilities SDK except in compliance with the License, which is provided at the time of installation
or download, or which otherwise accompanies this software in either electronic or hard copy form.

You may obtain a copy of the License at
https://developer.oculus.com/licenses/utilities-1.31

Unless required by applicable law or agreed to in writing, the Utilities SDK distributed
under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
ANY KIND, either express or implied. See the License for the specific language governing
permissions and limitations under the License.
************************************************************************************/

#if !(UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || (UNITY_ANDROID && !UNITY_EDITOR))
#define OVRPLUGIN_UNSUPPORTED_PLATFORM
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
#define OVRPLUGIN_INCLUDE_MRC_ANDROID
#endif

using System;
using System.Runtime.InteropServices;
using UnityEngine;

// Internal C# wrapper for OVRPlugin.

public static class OVRPlugin
{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
	public const bool isSupportedPlatform = false;
#else
	public const bool isSupportedPlatform = true;
#endif

#if OVRPLUGIN_UNSUPPORTED_PLATFORM
	public static readonly System.Version wrapperVersion = _versionZero;
#else
	public static readonly System.Version wrapperVersion = OVRP_1_41_0.version;
#endif

#if !OVRPLUGIN_UNSUPPORTED_PLATFORM
	private static System.Version _version;
#endif
	public static System.Version version
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			Debug.LogWarning("Platform is not currently supported by OVRPlugin");
			return _versionZero;
#else
			if (_version == null)
			{
				try
				{
					string pluginVersion = OVRP_1_1_0.ovrp_GetVersion();

					if (pluginVersion != null)
					{
						// Truncate unsupported trailing version info for System.Version. Original string is returned if not present.
						pluginVersion = pluginVersion.Split('-')[0];
						_version = new System.Version(pluginVersion);
					}
					else
					{
						_version = _versionZero;
					}
				}
				catch
				{
					_version = _versionZero;
				}

				// Unity 5.1.1f3-p3 have OVRPlugin version "0.5.0", which isn't accurate.
				if (_version == OVRP_0_5_0.version)
					_version = OVRP_0_1_0.version;

				if (_version > _versionZero && _version < OVRP_1_3_0.version)
					throw new PlatformNotSupportedException("Oculus Utilities version " + wrapperVersion + " is too new for OVRPlugin version " + _version.ToString() + ". Update to the latest version of Unity.");
			}

			return _version;
#endif
		}
	}

#if !OVRPLUGIN_UNSUPPORTED_PLATFORM
	private static System.Version _nativeSDKVersion;
#endif
	public static System.Version nativeSDKVersion
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return _versionZero;
#else
			if (_nativeSDKVersion == null)
			{
				try
				{
					string sdkVersion = string.Empty;

					if (version >= OVRP_1_1_0.version)
						sdkVersion = OVRP_1_1_0.ovrp_GetNativeSDKVersion();
					else
						sdkVersion = _versionZero.ToString();

					if (sdkVersion != null)
					{
						// Truncate unsupported trailing version info for System.Version. Original string is returned if not present.
						sdkVersion = sdkVersion.Split('-')[0];
						_nativeSDKVersion = new System.Version(sdkVersion);
					}
					else
					{
						_nativeSDKVersion = _versionZero;
					}
				}
				catch
				{
					_nativeSDKVersion = _versionZero;
				}
			}

			return _nativeSDKVersion;
#endif
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	private class GUID
	{
		public int a;
		public short b;
		public short c;
		public byte d0;
		public byte d1;
		public byte d2;
		public byte d3;
		public byte d4;
		public byte d5;
		public byte d6;
		public byte d7;
	}

	public enum Bool
	{
		False = 0,
		True
	}

	public enum Result
	{
		/// Success
		Success = 0,

		/// Failure
		Failure = -1000,
		Failure_InvalidParameter = -1001,
		Failure_NotInitialized = -1002,
		Failure_InvalidOperation = -1003,
		Failure_Unsupported = -1004,
		Failure_NotYetImplemented = -1005,
		Failure_OperationFailed = -1006,
		Failure_InsufficientSize = -1007,
	}

	public enum CameraStatus
	{
		CameraStatus_None,
		CameraStatus_Connected,
		CameraStatus_Calibrating,
		CameraStatus_CalibrationFailed,
		CameraStatus_Calibrated,
		CameraStatus_EnumSize = 0x7fffffff
	}

	public enum Eye
	{
		None = -1,
		Left = 0,
		Right = 1,
		Count = 2
	}

	public enum Tracker
	{
		None = -1,
		Zero = 0,
		One = 1,
		Two = 2,
		Three = 3,
		Count,
	}

	public enum Node
	{
		None = -1,
		EyeLeft = 0,
		EyeRight = 1,
		EyeCenter = 2,
		HandLeft = 3,
		HandRight = 4,
		TrackerZero = 5,
		TrackerOne = 6,
		TrackerTwo = 7,
		TrackerThree = 8,
		Head = 9,
		DeviceObjectZero = 10,
		Count,
	}

	public enum Controller
	{
		None = 0,
		LTouch = 0x00000001,
		RTouch = 0x00000002,
		Touch = LTouch | RTouch,
		Remote = 0x00000004,
		Gamepad = 0x00000010,
		Touchpad = 0x08000000,
		LTrackedRemote = 0x01000000,
		RTrackedRemote = 0x02000000,
		Active = unchecked((int)0x80000000),
		All = ~None,
	}

	public enum Handedness
	{
		Unsupported = 0,
		LeftHanded = 1,
		RightHanded = 2,
	}

	public enum TrackingOrigin
	{
		EyeLevel = 0,
		FloorLevel = 1,
		Stage = 2,
		Count,
	}

	public enum RecenterFlags
	{
		Default = 0,
		Controllers = 0x40000000,
		IgnoreAll = unchecked((int)0x80000000),
		Count,
	}

	public enum BatteryStatus
	{
		Charging = 0,
		Discharging,
		Full,
		NotCharging,
		Unknown,
	}

	public enum EyeTextureFormat
	{
		Default = 0,
		R8G8B8A8_sRGB = 0,
		R8G8B8A8 = 1,
		R16G16B16A16_FP = 2,
		R11G11B10_FP = 3,
		B8G8R8A8_sRGB = 4,
		B8G8R8A8 = 5,
		R5G6B5 = 11,
		EnumSize = 0x7fffffff
	}

	public enum PlatformUI
	{
		None = -1,
		ConfirmQuit = 1,
		GlobalMenuTutorial, // Deprecated
	}

	public enum SystemRegion
	{
		Unspecified = 0,
		Japan,
		China,
	}

	public enum SystemHeadset
	{
		None = 0,
		GearVR_R320, // Note4 Innovator
		GearVR_R321, // S6 Innovator
		GearVR_R322, // Commercial 1
		GearVR_R323, // Commercial 2 (USB Type C)
		GearVR_R324, // Commercial 3 (USB Type C)
		GearVR_R325, // Commercial 4 (USB Type C)
		Oculus_Go,
		Oculus_Quest,

		Rift_DK1 = 0x1000,
		Rift_DK2,
		Rift_CV1,
		Rift_CB,
		Rift_S,
	}

	public enum OverlayShape
	{
		Quad = 0,
		Cylinder = 1,
		Cubemap = 2,
		OffcenterCubemap = 4,
		Equirect = 5,
	}

	public enum Step
	{
		Render = -1,
		Physics = 0,
	}

	public enum CameraDevice
	{
		None = 0,
		WebCamera0 = 100,
		WebCamera1 = 101,
		ZEDCamera = 300,
	}

	public enum CameraDeviceDepthSensingMode
	{
		Standard = 0,
		Fill = 1,
	}

	public enum CameraDeviceDepthQuality
	{
		Low = 0,
		Medium = 1,
		High = 2,
	}

	public enum FixedFoveatedRenderingLevel
	{
		Off = 0,
		Low = 1,
		Medium = 2,
		High = 3,
		// High foveation setting with more detail toward the bottom of the view and more foveation near the top (Same as High on Oculus Go)
		HighTop = 4,
		EnumSize = 0x7FFFFFFF
	}

	[Obsolete("Please use FixedFoveatedRenderingLevel instead", false)]
	public enum TiledMultiResLevel
	{
		Off = 0,
		LMSLow = FixedFoveatedRenderingLevel.Low,
		LMSMedium = FixedFoveatedRenderingLevel.Medium,
		LMSHigh = FixedFoveatedRenderingLevel.High,
		// High foveation setting with more detail toward the bottom of the view and more foveation near the top (Same as High on Oculus Go)
		LMSHighTop = FixedFoveatedRenderingLevel.HighTop,
		EnumSize = 0x7FFFFFFF
	}

	public enum PerfMetrics
	{
		App_CpuTime_Float = 0,
		App_GpuTime_Float = 1,

		Compositor_CpuTime_Float = 3,
		Compositor_GpuTime_Float = 4,
		Compositor_DroppedFrameCount_Int = 5,

		System_GpuUtilPercentage_Float = 7,
		System_CpuUtilAveragePercentage_Float = 8,
		System_CpuUtilWorstPercentage_Float = 9,

		// 1.32.0
		Device_CpuClockFrequencyInMHz_Float = 10,
		Device_GpuClockFrequencyInMHz_Float = 11,
		Device_CpuClockLevel_Int = 12,
		Device_GpuClockLevel_Int = 13,

		Count,
		EnumSize = 0x7FFFFFFF
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct CameraDeviceIntrinsicsParameters
	{
		float fx; /* Focal length in pixels along x axis. */
		float fy; /* Focal length in pixels along y axis. */
		float cx; /* Optical center along x axis, defined in pixels (usually close to width/2). */
		float cy; /* Optical center along y axis, defined in pixels (usually close to height/2). */
		double disto0; /* Distortion factor : [ k1, k2, p1, p2, k3 ]. Radial (k1,k2,k3) and Tangential (p1,p2) distortion.*/
		double disto1;
		double disto2;
		double disto3;
		double disto4;
		float v_fov; /* Vertical field of view after stereo rectification, in degrees. */
		float h_fov; /* Horizontal field of view after stereo rectification, in degrees.*/
		float d_fov; /* Diagonal field of view after stereo rectification, in degrees.*/
		int w; /* Resolution width */
		int h; /* Resolution height */
	}

	private const int OverlayShapeFlagShift = 4;
	private enum OverlayFlag
	{
		None = unchecked((int)0x00000000),
		OnTop = unchecked((int)0x00000001),
		HeadLocked = unchecked((int)0x00000002),
		NoDepth = unchecked((int)0x00000004),
		ExpensiveSuperSample = unchecked((int)0x00000008),

		// Using the 5-8 bits for shapes, total 16 potential shapes can be supported 0x000000[0]0 ->  0x000000[F]0
		ShapeFlag_Quad = unchecked((int)OverlayShape.Quad << OverlayShapeFlagShift),
		ShapeFlag_Cylinder = unchecked((int)OverlayShape.Cylinder << OverlayShapeFlagShift),
		ShapeFlag_Cubemap = unchecked((int)OverlayShape.Cubemap << OverlayShapeFlagShift),
		ShapeFlag_OffcenterCubemap = unchecked((int)OverlayShape.OffcenterCubemap << OverlayShapeFlagShift),
		ShapeFlagRangeMask = unchecked((int)0xF << OverlayShapeFlagShift),
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Vector2f
	{
		public float x;
		public float y;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Vector3f
	{
		public float x;
		public float y;
		public float z;
		public static readonly Vector3f zero = new Vector3f { x = 0.0f, y = 0.0f, z = 0.0f };
		public override string ToString()
		{
			return string.Format("{0}, {1}, {2}", x, y, z);
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Quatf
	{
		public float x;
		public float y;
		public float z;
		public float w;
		public static readonly Quatf identity = new Quatf { x = 0.0f, y = 0.0f, z = 0.0f, w = 1.0f };
		public override string ToString()
		{
			return string.Format("{0}, {1}, {2}, {3}", x, y, z, w);
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Posef
	{
		public Quatf Orientation;
		public Vector3f Position;
		public static readonly Posef identity = new Posef { Orientation = Quatf.identity, Position = Vector3f.zero };
		public override string ToString()
		{
			return string.Format("Position ({0}), Orientation({1})", Position, Orientation);
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct TextureRectMatrixf
	{
		public Rect leftRect;
		public Rect rightRect;
		public Vector4 leftScaleBias;
		public Vector4 rightScaleBias;
		public static readonly TextureRectMatrixf zero = new TextureRectMatrixf { leftRect = new Rect(0, 0, 1, 1), rightRect = new Rect(0, 0, 1, 1), leftScaleBias = new Vector4(1, 1, 0, 0), rightScaleBias = new Vector4(1, 1, 0, 0) };

		public override string ToString()
		{
			return string.Format("Rect Left ({0}), Rect Right({1}), Scale Bias Left ({2}), Scale Bias Right({3})", leftRect, rightRect, leftScaleBias, rightScaleBias);
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct PoseStatef
	{
		public Posef Pose;
		public Vector3f Velocity;
		public Vector3f Acceleration;
		public Vector3f AngularVelocity;
		public Vector3f AngularAcceleration;
		public double Time;

		public static readonly PoseStatef identity = new PoseStatef
		{
			Pose = Posef.identity,
			Velocity = Vector3f.zero,
			Acceleration = Vector3f.zero,
			AngularVelocity = Vector3f.zero,
			AngularAcceleration = Vector3f.zero
		};
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct ControllerState4
	{
		public uint ConnectedControllers;
		public uint Buttons;
		public uint Touches;
		public uint NearTouches;
		public float LIndexTrigger;
		public float RIndexTrigger;
		public float LHandTrigger;
		public float RHandTrigger;
		public Vector2f LThumbstick;
		public Vector2f RThumbstick;
		public Vector2f LTouchpad;
		public Vector2f RTouchpad;
		public byte LBatteryPercentRemaining;
		public byte RBatteryPercentRemaining;
		public byte LRecenterCount;
		public byte RRecenterCount;
		public byte Reserved_27;
		public byte Reserved_26;
		public byte Reserved_25;
		public byte Reserved_24;
		public byte Reserved_23;
		public byte Reserved_22;
		public byte Reserved_21;
		public byte Reserved_20;
		public byte Reserved_19;
		public byte Reserved_18;
		public byte Reserved_17;
		public byte Reserved_16;
		public byte Reserved_15;
		public byte Reserved_14;
		public byte Reserved_13;
		public byte Reserved_12;
		public byte Reserved_11;
		public byte Reserved_10;
		public byte Reserved_09;
		public byte Reserved_08;
		public byte Reserved_07;
		public byte Reserved_06;
		public byte Reserved_05;
		public byte Reserved_04;
		public byte Reserved_03;
		public byte Reserved_02;
		public byte Reserved_01;
		public byte Reserved_00;

		public ControllerState4(ControllerState2 cs)
		{
			ConnectedControllers = cs.ConnectedControllers;
			Buttons = cs.Buttons;
			Touches = cs.Touches;
			NearTouches = cs.NearTouches;
			LIndexTrigger = cs.LIndexTrigger;
			RIndexTrigger = cs.RIndexTrigger;
			LHandTrigger = cs.LHandTrigger;
			RHandTrigger = cs.RHandTrigger;
			LThumbstick = cs.LThumbstick;
			RThumbstick = cs.RThumbstick;
			LTouchpad = cs.LTouchpad;
			RTouchpad = cs.RTouchpad;
			LBatteryPercentRemaining = 0;
			RBatteryPercentRemaining = 0;
			LRecenterCount = 0;
			RRecenterCount = 0;
			Reserved_27 = 0;
			Reserved_26 = 0;
			Reserved_25 = 0;
			Reserved_24 = 0;
			Reserved_23 = 0;
			Reserved_22 = 0;
			Reserved_21 = 0;
			Reserved_20 = 0;
			Reserved_19 = 0;
			Reserved_18 = 0;
			Reserved_17 = 0;
			Reserved_16 = 0;
			Reserved_15 = 0;
			Reserved_14 = 0;
			Reserved_13 = 0;
			Reserved_12 = 0;
			Reserved_11 = 0;
			Reserved_10 = 0;
			Reserved_09 = 0;
			Reserved_08 = 0;
			Reserved_07 = 0;
			Reserved_06 = 0;
			Reserved_05 = 0;
			Reserved_04 = 0;
			Reserved_03 = 0;
			Reserved_02 = 0;
			Reserved_01 = 0;
			Reserved_00 = 0;
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct ControllerState2
	{
		public uint ConnectedControllers;
		public uint Buttons;
		public uint Touches;
		public uint NearTouches;
		public float LIndexTrigger;
		public float RIndexTrigger;
		public float LHandTrigger;
		public float RHandTrigger;
		public Vector2f LThumbstick;
		public Vector2f RThumbstick;
		public Vector2f LTouchpad;
		public Vector2f RTouchpad;

		public ControllerState2(ControllerState cs)
		{
			ConnectedControllers = cs.ConnectedControllers;
			Buttons = cs.Buttons;
			Touches = cs.Touches;
			NearTouches = cs.NearTouches;
			LIndexTrigger = cs.LIndexTrigger;
			RIndexTrigger = cs.RIndexTrigger;
			LHandTrigger = cs.LHandTrigger;
			RHandTrigger = cs.RHandTrigger;
			LThumbstick = cs.LThumbstick;
			RThumbstick = cs.RThumbstick;
			LTouchpad = new Vector2f() { x = 0.0f, y = 0.0f };
			RTouchpad = new Vector2f() { x = 0.0f, y = 0.0f };
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct ControllerState
	{
		public uint ConnectedControllers;
		public uint Buttons;
		public uint Touches;
		public uint NearTouches;
		public float LIndexTrigger;
		public float RIndexTrigger;
		public float LHandTrigger;
		public float RHandTrigger;
		public Vector2f LThumbstick;
		public Vector2f RThumbstick;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct HapticsBuffer
	{
		public IntPtr Samples;
		public int SamplesCount;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct HapticsState
	{
		public int SamplesAvailable;
		public int SamplesQueued;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct HapticsDesc
	{
		public int SampleRateHz;
		public int SampleSizeInBytes;
		public int MinimumSafeSamplesQueued;
		public int MinimumBufferSamplesCount;
		public int OptimalBufferSamplesCount;
		public int MaximumBufferSamplesCount;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct AppPerfFrameStats
	{
		public int HmdVsyncIndex;
		public int AppFrameIndex;
		public int AppDroppedFrameCount;
		public float AppMotionToPhotonLatency;
		public float AppQueueAheadTime;
		public float AppCpuElapsedTime;
		public float AppGpuElapsedTime;
		public int CompositorFrameIndex;
		public int CompositorDroppedFrameCount;
		public float CompositorLatency;
		public float CompositorCpuElapsedTime;
		public float CompositorGpuElapsedTime;
		public float CompositorCpuStartToGpuEndElapsedTime;
		public float CompositorGpuEndToVsyncElapsedTime;
	}

	public const int AppPerfFrameStatsMaxCount = 5;

	[StructLayout(LayoutKind.Sequential)]
	public struct AppPerfStats
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = AppPerfFrameStatsMaxCount)]
		public AppPerfFrameStats[] FrameStats;
		public int FrameStatsCount;
		public Bool AnyFrameStatsDropped;
		public float AdaptiveGpuPerformanceScale;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Sizei
	{
		public int w;
		public int h;

		public static readonly Sizei zero = new Sizei { w = 0, h = 0 };
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Sizef
	{
		public float w;
		public float h;

		public static readonly Sizef zero = new Sizef { w = 0, h = 0 };
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Vector2i
	{
		public int x;
		public int y;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Recti {
		Vector2i Pos;
		Sizei Size;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Rectf {
		Vector2f Pos;
		Sizef Size;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Frustumf
	{
		public float zNear;
		public float zFar;
		public float fovX;
		public float fovY;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Frustumf2
	{
		public float zNear;
		public float zFar;
		public Fovf Fov;
	}

	public enum BoundaryType
	{
		OuterBoundary = 0x0001,
		PlayArea = 0x0100,
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct BoundaryTestResult
	{
		public Bool IsTriggering;
		public float ClosestDistance;
		public Vector3f ClosestPoint;
		public Vector3f ClosestPointNormal;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct BoundaryGeometry
	{
		public BoundaryType BoundaryType;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public Vector3f[] Points;
		public int PointsCount;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Colorf
	{
		public float r;
		public float g;
		public float b;
		public float a;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Fovf
	{
		public float UpTan;
		public float DownTan;
		public float LeftTan;
		public float RightTan;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct CameraIntrinsics
	{
		public bool IsValid;
		public double LastChangedTimeSeconds;
		public Fovf FOVPort;
		public float VirtualNearPlaneDistanceMeters;
		public float VirtualFarPlaneDistanceMeters;
		public Sizei ImageSensorPixelResolution;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct CameraExtrinsics
	{
		public bool IsValid;
		public double LastChangedTimeSeconds;
		public CameraStatus CameraStatusData;
		public Node AttachedToNode;
		public Posef RelativePose;
	}

	public enum LayerLayout
	{
		Stereo = 0,
		Mono = 1,
		DoubleWide = 2,
		Array = 3,
		EnumSize = 0xF
	}

	public enum LayerFlags
	{
		Static = (1 << 0),
		LoadingScreen = (1 << 1),
		SymmetricFov = (1 << 2),
		TextureOriginAtBottomLeft = (1 << 3),
		ChromaticAberrationCorrection = (1 << 4),
		NoAllocation = (1 << 5),
		ProtectedContent = (1 << 6),
		AndroidSurfaceSwapChain = (1 << 7),
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct LayerDesc
	{
		public OverlayShape Shape;
		public LayerLayout Layout;
		public Sizei TextureSize;
		public int MipLevels;
		public int SampleCount;
		public EyeTextureFormat Format;
		public int LayerFlags;

		//Eye FOV-only members.
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
		public Fovf[] Fov;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
		public Rectf[] VisibleRect;
		public Sizei MaxViewportSize;
		EyeTextureFormat DepthFormat;

		public override string ToString()
		{
			string delim = ", ";
			return Shape.ToString()
				+ delim + Layout.ToString()
				+ delim + TextureSize.w.ToString() + "x" + TextureSize.h.ToString()
				+ delim + MipLevels.ToString()
				+ delim + SampleCount.ToString()
				+ delim + Format.ToString()
				+ delim + LayerFlags.ToString();
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct LayerSubmit
	{
		int LayerId;
		int TextureStage;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
		Recti[] ViewportRect;
		Posef Pose;
		int LayerSubmitFlags;
	}

	public static bool initialized
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			return OVRP_1_1_0.ovrp_GetInitialized() == OVRPlugin.Bool.True;
#endif
		}
	}

	public static bool chromatic
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (version >= OVRP_1_7_0.version)
				return initialized && OVRP_1_7_0.ovrp_GetAppChromaticCorrection() == OVRPlugin.Bool.True;

#if UNITY_ANDROID && !UNITY_EDITOR
			return false;
#else
			return true;
#endif
#endif
		}

		set {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return;
#else
			if (initialized && version >= OVRP_1_7_0.version)
				OVRP_1_7_0.ovrp_SetAppChromaticCorrection(ToBool(value));
#endif
		}
	}

	public static bool monoscopic
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			return initialized && OVRP_1_1_0.ovrp_GetAppMonoscopic() == OVRPlugin.Bool.True;
#endif
		}
		set {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return;
#else
			if (initialized)
			{
				OVRP_1_1_0.ovrp_SetAppMonoscopic(ToBool(value));
			}
#endif
		}
	}

	public static bool rotation
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			return initialized && OVRP_1_1_0.ovrp_GetTrackingOrientationEnabled() == Bool.True;
#endif
		}
		set {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return;
#else
			if (initialized)
			{
				OVRP_1_1_0.ovrp_SetTrackingOrientationEnabled(ToBool(value));
			}
#endif
		}
	}

	public static bool position
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			return initialized && OVRP_1_1_0.ovrp_GetTrackingPositionEnabled() == Bool.True;
#endif
		}
		set {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return;
#else
			if (initialized)
			{
				OVRP_1_1_0.ovrp_SetTrackingPositionEnabled(ToBool(value));
			}
#endif
		}
	}

	public static bool useIPDInPositionTracking
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (initialized && version >= OVRP_1_6_0.version)
				return OVRP_1_6_0.ovrp_GetTrackingIPDEnabled() == OVRPlugin.Bool.True;

			return true;
#endif
		}

		set {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return;
#else
			if (initialized && version >= OVRP_1_6_0.version)
				OVRP_1_6_0.ovrp_SetTrackingIPDEnabled(ToBool(value));
#endif
		}
	}

	public static bool positionSupported
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			return initialized && OVRP_1_1_0.ovrp_GetTrackingPositionSupported() == Bool.True;
#endif
		}
	}

	public static bool positionTracked
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			return initialized && OVRP_1_1_0.ovrp_GetNodePositionTracked(Node.EyeCenter) == Bool.True;
#endif
		}
	}

	public static bool powerSaving
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			return initialized && OVRP_1_1_0.ovrp_GetSystemPowerSavingMode() == Bool.True;
#endif
		}
	}

	public static bool hmdPresent
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			return initialized && OVRP_1_1_0.ovrp_GetNodePresent(Node.EyeCenter) == Bool.True;
#endif
		}
	}

	public static bool userPresent
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			return initialized && OVRP_1_1_0.ovrp_GetUserPresent() == Bool.True;
#endif
		}
	}

	public static bool headphonesPresent
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			return initialized && OVRP_1_3_0.ovrp_GetSystemHeadphonesPresent() == OVRPlugin.Bool.True;
#endif
		}
	}

	public static int recommendedMSAALevel
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return 2;
#else
			if (initialized && version >= OVRP_1_6_0.version)
				return OVRP_1_6_0.ovrp_GetSystemRecommendedMSAALevel();
			else
				return 2;
#endif
		}
	}

	public static SystemRegion systemRegion
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return SystemRegion.Unspecified;
#else
			if (initialized && version >= OVRP_1_5_0.version)
				return OVRP_1_5_0.ovrp_GetSystemRegion();
			else
				return SystemRegion.Unspecified;
#endif
		}
	}

#if !OVRPLUGIN_UNSUPPORTED_PLATFORM
	private static GUID _nativeAudioOutGuid = new OVRPlugin.GUID();
	private static Guid _cachedAudioOutGuid;
	private static string _cachedAudioOutString;
#endif

	public static string audioOutId
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return string.Empty;
#else
			try
			{
				if (_nativeAudioOutGuid == null)
					_nativeAudioOutGuid = new OVRPlugin.GUID();

				IntPtr ptr = OVRP_1_1_0.ovrp_GetAudioOutId();
				if (ptr != IntPtr.Zero)
				{
					Marshal.PtrToStructure(ptr, _nativeAudioOutGuid);
					Guid managedGuid = new Guid(
						_nativeAudioOutGuid.a,
						_nativeAudioOutGuid.b,
						_nativeAudioOutGuid.c,
						_nativeAudioOutGuid.d0,
						_nativeAudioOutGuid.d1,
						_nativeAudioOutGuid.d2,
						_nativeAudioOutGuid.d3,
						_nativeAudioOutGuid.d4,
						_nativeAudioOutGuid.d5,
						_nativeAudioOutGuid.d6,
						_nativeAudioOutGuid.d7);

					if (managedGuid != _cachedAudioOutGuid)
					{
						_cachedAudioOutGuid = managedGuid;
						_cachedAudioOutString = _cachedAudioOutGuid.ToString();
					}

					return _cachedAudioOutString;
				}
			}
			catch { }

			return string.Empty;
#endif
		}
	}

#if !OVRPLUGIN_UNSUPPORTED_PLATFORM
	private static GUID _nativeAudioInGuid = new OVRPlugin.GUID();
	private static Guid _cachedAudioInGuid;
	private static string _cachedAudioInString;
#endif

	public static string audioInId
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return string.Empty;
#else
			try
			{
				if (_nativeAudioInGuid == null)
					_nativeAudioInGuid = new OVRPlugin.GUID();

				IntPtr ptr = OVRP_1_1_0.ovrp_GetAudioInId();
				if (ptr != IntPtr.Zero)
				{
					Marshal.PtrToStructure(ptr, _nativeAudioInGuid);
					Guid managedGuid = new Guid(
						_nativeAudioInGuid.a,
						_nativeAudioInGuid.b,
						_nativeAudioInGuid.c,
						_nativeAudioInGuid.d0,
						_nativeAudioInGuid.d1,
						_nativeAudioInGuid.d2,
						_nativeAudioInGuid.d3,
						_nativeAudioInGuid.d4,
						_nativeAudioInGuid.d5,
						_nativeAudioInGuid.d6,
						_nativeAudioInGuid.d7);

					if (managedGuid != _cachedAudioInGuid)
					{
						_cachedAudioInGuid = managedGuid;
						_cachedAudioInString = _cachedAudioInGuid.ToString();
					}

					return _cachedAudioInString;
				}
			}
			catch { }

			return string.Empty;
#endif
		}
	}

	public static bool hasVrFocus
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			return OVRP_1_1_0.ovrp_GetAppHasVrFocus() == Bool.True;
#endif
		}
	}

	public static bool hasInputFocus
	{
		get
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return true;
#else
			if (version >= OVRP_1_18_0.version)
			{
				Bool inputFocus = Bool.False;
				Result result = OVRP_1_18_0.ovrp_GetAppHasInputFocus(out inputFocus);
				if (Result.Success == result)
					return inputFocus == Bool.True;
				else
				{
					//Debug.LogWarning("ovrp_GetAppHasInputFocus return " + result);
					return false;
				}
			}

			return true;
#endif
		}
	}

	public static bool shouldQuit
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			return OVRP_1_1_0.ovrp_GetAppShouldQuit() == Bool.True;
#endif
		}
	}

	public static bool shouldRecenter
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			return OVRP_1_1_0.ovrp_GetAppShouldRecenter() == Bool.True;
#endif
		}
	}

	public static string productName
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return string.Empty;
#else
			return OVRP_1_1_0.ovrp_GetSystemProductName();
#endif
		}
	}

	public static string latency
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return string.Empty;
#else
			if (!initialized)
				return string.Empty;

			return OVRP_1_1_0.ovrp_GetAppLatencyTimings();
#endif
		}
	}

	public static float eyeDepth
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return 0.0f;
#else
			if (!initialized)
				return 0.0f;

			return OVRP_1_1_0.ovrp_GetUserEyeDepth();
#endif
		}
		set {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return;
#else
			OVRP_1_1_0.ovrp_SetUserEyeDepth(value);
#endif
		}
	}

	public static float eyeHeight
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return 0.0f;
#else
			return OVRP_1_1_0.ovrp_GetUserEyeHeight();
#endif
		}
		set {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return;
#else
			OVRP_1_1_0.ovrp_SetUserEyeHeight(value);
#endif
		}
	}

	public static float batteryLevel
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return 0.0f;
#else
			return OVRP_1_1_0.ovrp_GetSystemBatteryLevel();
#endif
		}
	}

	public static float batteryTemperature
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return 0.0f;
#else
			return OVRP_1_1_0.ovrp_GetSystemBatteryTemperature();
#endif
		}
	}

	public static int cpuLevel
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return 0;
#else
			return OVRP_1_1_0.ovrp_GetSystemCpuLevel();
#endif
		}
		set {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return;
#else
			OVRP_1_1_0.ovrp_SetSystemCpuLevel(value);
#endif
		}
	}

	public static int gpuLevel
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return 0;
#else
			return OVRP_1_1_0.ovrp_GetSystemGpuLevel();
#endif
		}
		set {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return;
#else
			OVRP_1_1_0.ovrp_SetSystemGpuLevel(value);
#endif
		}
	}

	public static int vsyncCount
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return 0;
#else
			return OVRP_1_1_0.ovrp_GetSystemVSyncCount();
#endif
		}
		set {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return;
#else
			OVRP_1_2_0.ovrp_SetSystemVSyncCount(value);
#endif
		}
	}

	public static float systemVolume
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return 0.0f;
#else
			return OVRP_1_1_0.ovrp_GetSystemVolume();
#endif
		}
	}

	public static float ipd
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return 0.0f;
#else
			return OVRP_1_1_0.ovrp_GetUserIPD();
#endif
		}
		set {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return;
#else
			OVRP_1_1_0.ovrp_SetUserIPD(value);
#endif
		}
	}

	public static bool occlusionMesh
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			return initialized && (OVRP_1_3_0.ovrp_GetEyeOcclusionMeshEnabled() == Bool.True);
#endif
		}
		set {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return;
#else
			if (!initialized)
				return;

			OVRP_1_3_0.ovrp_SetEyeOcclusionMeshEnabled(ToBool(value));
#endif
		}
	}

	public static BatteryStatus batteryStatus
	{
		get {
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return default(BatteryStatus);
#else
			return OVRP_1_1_0.ovrp_GetSystemBatteryStatus();
#endif
		}
	}

	public static Frustumf GetEyeFrustum(Eye eyeId)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return new Frustumf();
#else
		return OVRP_1_1_0.ovrp_GetNodeFrustum((Node)eyeId);
#endif
	}

	public static Sizei GetEyeTextureSize(Eye eyeId)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return new Sizei();
#else
		return OVRP_0_1_0.ovrp_GetEyeTextureSize(eyeId);
#endif
	}

	public static Posef GetTrackerPose(Tracker trackerId)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return Posef.identity;
#else
		return GetNodePose((Node)((int)trackerId + (int)Node.TrackerZero), Step.Render);
#endif
	}

	public static Frustumf GetTrackerFrustum(Tracker trackerId)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return new Frustumf();
#else
		return OVRP_1_1_0.ovrp_GetNodeFrustum((Node)((int)trackerId + (int)Node.TrackerZero));
#endif
	}

	public static bool ShowUI(PlatformUI ui)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		return OVRP_1_1_0.ovrp_ShowSystemUI(ui) == Bool.True;
#endif
	}

	public static bool EnqueueSubmitLayer(bool onTop, bool headLocked, bool noDepthBufferTesting, IntPtr leftTexture, IntPtr rightTexture, int layerId, int frameIndex, Posef pose, Vector3f scale, int layerIndex = 0, OverlayShape shape = OverlayShape.Quad,
										bool overrideTextureRectMatrix = false, TextureRectMatrixf textureRectMatrix = default(TextureRectMatrixf), bool overridePerLayerColorScaleAndOffset = false, Vector4 colorScale = default(Vector4), Vector4 colorOffset = default(Vector4),
										bool expensiveSuperSample = false)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (!initialized)
			return false;

		if (version >= OVRP_1_6_0.version)
		{
			uint flags = (uint)OverlayFlag.None;
			if (onTop)
				flags |= (uint)OverlayFlag.OnTop;
			if (headLocked)
				flags |= (uint)OverlayFlag.HeadLocked;
			if (noDepthBufferTesting)
				flags |= (uint)OverlayFlag.NoDepth;
			if (expensiveSuperSample)
				flags |= (uint)OverlayFlag.ExpensiveSuperSample;

			if (shape == OverlayShape.Cylinder || shape == OverlayShape.Cubemap)
			{
#if UNITY_ANDROID
				if (version >= OVRP_1_7_0.version)
					flags |= (uint)(shape) << OverlayShapeFlagShift;
				else
#else
				if (shape == OverlayShape.Cubemap && version >= OVRP_1_10_0.version)
					flags |= (uint)(shape) << OverlayShapeFlagShift;
				else if (shape == OverlayShape.Cylinder && version >= OVRP_1_16_0.version)
					flags |= (uint)(shape) << OverlayShapeFlagShift;
				else
#endif
					return false;
			}

			if (shape == OverlayShape.OffcenterCubemap)
			{
#if UNITY_ANDROID
				if (version >= OVRP_1_11_0.version)
					flags |= (uint)(shape) << OverlayShapeFlagShift;
				else
#endif
					return false;
			}

			if (shape == OverlayShape.Equirect)
			{
#if UNITY_ANDROID
				if (version >= OVRP_1_21_0.version)
					flags |= (uint)(shape) << OverlayShapeFlagShift;
				else
#endif
					return false;
			}

			if (version >= OVRP_1_34_0.version && layerId != -1)
				return OVRP_1_34_0.ovrp_EnqueueSubmitLayer2(flags, leftTexture, rightTexture, layerId, frameIndex, ref pose, ref scale, layerIndex,
				overrideTextureRectMatrix ? Bool.True : Bool.False, ref textureRectMatrix, overridePerLayerColorScaleAndOffset ? Bool.True : Bool.False, ref colorScale, ref colorOffset) == Result.Success;
			else if (version >= OVRP_1_15_0.version && layerId != -1)
				return OVRP_1_15_0.ovrp_EnqueueSubmitLayer(flags, leftTexture, rightTexture, layerId, frameIndex, ref pose, ref scale, layerIndex) == Result.Success;

			return OVRP_1_6_0.ovrp_SetOverlayQuad3(flags, leftTexture, rightTexture, IntPtr.Zero, pose, scale, layerIndex) == Bool.True;
		}

		if (layerIndex != 0)
			return false;

		return OVRP_0_1_1.ovrp_SetOverlayQuad2(ToBool(onTop), ToBool(headLocked), leftTexture, IntPtr.Zero, pose, scale) == Bool.True;
#endif
	}

	public static LayerDesc CalculateLayerDesc(OverlayShape shape, LayerLayout layout, Sizei textureSize,
		int mipLevels, int sampleCount, EyeTextureFormat format, int layerFlags)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return new LayerDesc();
#else
		LayerDesc layerDesc = new LayerDesc();
		if (!initialized)
			return layerDesc;

		if (version >= OVRP_1_15_0.version)
		{
			OVRP_1_15_0.ovrp_CalculateLayerDesc(shape, layout, ref textureSize,
				mipLevels, sampleCount, format, layerFlags, ref layerDesc);
		}

		return layerDesc;
#endif
	}

	public static bool EnqueueSetupLayer(LayerDesc desc, int compositionDepth, IntPtr layerID)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (!initialized)
			return false;

		if (version >= OVRP_1_28_0.version)
			return OVRP_1_28_0.ovrp_EnqueueSetupLayer2(ref desc, compositionDepth, layerID) == Result.Success;
		else if (version >= OVRP_1_15_0.version)
		{
			if (compositionDepth != 0)
			{
				Debug.LogWarning("Use Oculus Plugin 1.28.0 or above to support non-zero compositionDepth");
			}
			return OVRP_1_15_0.ovrp_EnqueueSetupLayer(ref desc, layerID) == Result.Success;
		}

		return false;
#endif
	}

	public static bool EnqueueDestroyLayer(IntPtr layerID)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (!initialized)
			return false;
		if (version >= OVRP_1_15_0.version)
			return OVRP_1_15_0.ovrp_EnqueueDestroyLayer(layerID) == Result.Success;

		return false;
#endif
	}

	public static IntPtr GetLayerTexture(int layerId, int stage, Eye eyeId)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return IntPtr.Zero;
#else
		IntPtr textureHandle = IntPtr.Zero;
		if (!initialized)
			return textureHandle;

		if (version >= OVRP_1_15_0.version)
			OVRP_1_15_0.ovrp_GetLayerTexturePtr(layerId, stage, eyeId, ref textureHandle);

		return textureHandle;
#endif
	}

	public static int GetLayerTextureStageCount(int layerId)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return 1;
#else
		if (!initialized)
			return 1;

		int stageCount = 1;

		if (version >= OVRP_1_15_0.version)
			OVRP_1_15_0.ovrp_GetLayerTextureStageCount(layerId, ref stageCount);

		return stageCount;
#endif
	}

	public static IntPtr GetLayerAndroidSurfaceObject(int layerId)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return IntPtr.Zero;
#else
		IntPtr surfaceObject = IntPtr.Zero;
		if (!initialized)
			return surfaceObject;

		if (version >= OVRP_1_29_0.version)
			OVRP_1_29_0.ovrp_GetLayerAndroidSurfaceObject(layerId, ref surfaceObject);

		return surfaceObject;
#endif
	}

	public static bool UpdateNodePhysicsPoses(int frameIndex, double predictionSeconds)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_8_0.version)
			return OVRP_1_8_0.ovrp_Update2((int)Step.Physics, frameIndex, predictionSeconds) == Bool.True;

		return false;
#endif
	}

	public static Posef GetNodePose(Node nodeId, Step stepId)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return Posef.identity;
#else
		if (version >= OVRP_1_12_0.version)
			return OVRP_1_12_0.ovrp_GetNodePoseState(stepId, nodeId).Pose;

		if (version >= OVRP_1_8_0.version && stepId == Step.Physics)
			return OVRP_1_8_0.ovrp_GetNodePose2(0, nodeId);

		return OVRP_0_1_2.ovrp_GetNodePose(nodeId);
#endif
	}

	public static Vector3f GetNodeVelocity(Node nodeId, Step stepId)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return new Vector3f();
#else
		if (version >= OVRP_1_12_0.version)
			return OVRP_1_12_0.ovrp_GetNodePoseState(stepId, nodeId).Velocity;

		if (version >= OVRP_1_8_0.version && stepId == Step.Physics)
			return OVRP_1_8_0.ovrp_GetNodeVelocity2(0, nodeId).Position;

		return OVRP_0_1_3.ovrp_GetNodeVelocity(nodeId).Position;
#endif
	}

	public static Vector3f GetNodeAngularVelocity(Node nodeId, Step stepId)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return new Vector3f();
#else
		if (version >= OVRP_1_12_0.version)
			return OVRP_1_12_0.ovrp_GetNodePoseState(stepId, nodeId).AngularVelocity;

		return new Vector3f(); //TODO: Convert legacy quat to vec3?
#endif
	}

	public static Vector3f GetNodeAcceleration(Node nodeId, Step stepId)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return new Vector3f();
#else
		if (version >= OVRP_1_12_0.version)
			return OVRP_1_12_0.ovrp_GetNodePoseState(stepId, nodeId).Acceleration;

		if (version >= OVRP_1_8_0.version && stepId == Step.Physics)
			return OVRP_1_8_0.ovrp_GetNodeAcceleration2(0, nodeId).Position;

		return OVRP_0_1_3.ovrp_GetNodeAcceleration(nodeId).Position;
#endif
	}

	public static Vector3f GetNodeAngularAcceleration(Node nodeId, Step stepId)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return new Vector3f();
#else
		if (version >= OVRP_1_12_0.version)
			return OVRP_1_12_0.ovrp_GetNodePoseState(stepId, nodeId).AngularAcceleration;

		return new Vector3f(); //TODO: Convert legacy quat to vec3?
#endif
	}

	public static bool GetNodePresent(Node nodeId)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		return OVRP_1_1_0.ovrp_GetNodePresent(nodeId) == Bool.True;
#endif
	}

	public static bool GetNodeOrientationTracked(Node nodeId)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		return OVRP_1_1_0.ovrp_GetNodeOrientationTracked(nodeId) == Bool.True;
#endif
	}

	public static bool GetNodeOrientationValid(Node nodeId)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_38_0.version)
		{
			Bool orientationValid = Bool.False;
			Result result = OVRP_1_38_0.ovrp_GetNodeOrientationValid(nodeId, ref orientationValid);
			return result == Result.Success && orientationValid == Bool.True;
		}
		else
		{
			return GetNodeOrientationTracked(nodeId);
		}
#endif

	}

	public static bool GetNodePositionTracked(Node nodeId)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		return OVRP_1_1_0.ovrp_GetNodePositionTracked(nodeId) == Bool.True;
#endif
	}

	public static bool GetNodePositionValid(Node nodeId)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_38_0.version)
		{
			Bool positionValid = Bool.False;
			Result result = OVRP_1_38_0.ovrp_GetNodePositionValid(nodeId, ref positionValid);
			return result == Result.Success && positionValid == Bool.True;
		}
		else
		{
			return GetNodePositionTracked(nodeId);
		}
#endif
	}

	public static PoseStatef GetNodePoseStateRaw(Node nodeId, Step stepId)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return PoseStatef.identity;
#else
		if (version >= OVRP_1_29_0.version)
		{
			PoseStatef nodePoseState;
			Result result = OVRP_1_29_0.ovrp_GetNodePoseStateRaw(stepId, -1, nodeId, out nodePoseState);
			if (result == Result.Success)
			{
				return nodePoseState;
			}
			else
			{
				return PoseStatef.identity;
			}
		}
		if (version >= OVRP_1_12_0.version)
			return OVRP_1_12_0.ovrp_GetNodePoseState(stepId, nodeId);
		else
			return PoseStatef.identity;
#endif
	}

	public static Posef GetCurrentTrackingTransformPose()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return Posef.identity;
#else
		if (version >= OVRP_1_30_0.version)
		{
			Posef trackingTransformPose;
			Result result = OVRP_1_30_0.ovrp_GetCurrentTrackingTransformPose(out trackingTransformPose);
			if (result == Result.Success)
			{
				return trackingTransformPose;
			}
			else
			{
				return Posef.identity;
			}
		}
		else
		{
			return Posef.identity;
		}
#endif
	}

	public static Posef GetTrackingTransformRawPose()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return Posef.identity;
#else
		if (version >= OVRP_1_30_0.version)
		{
			Posef trackingTransforRawPose;
			Result result = OVRP_1_30_0.ovrp_GetTrackingTransformRawPose(out trackingTransforRawPose);
			if (result == Result.Success)
			{
				return trackingTransforRawPose;
			}
			else
			{
				return Posef.identity;
			}
		}
		else
		{
			return Posef.identity;
		}
#endif
	}

	public static Posef GetTrackingTransformRelativePose(TrackingOrigin trackingOrigin)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return Posef.identity;
#else
		if (version >= OVRP_1_38_0.version)
		{
			Posef trackingTransformRelativePose = Posef.identity;
			Result result = OVRP_1_38_0.ovrp_GetTrackingTransformRelativePose(ref trackingTransformRelativePose, trackingOrigin);
			if (result == Result.Success)
			{
				return trackingTransformRelativePose;
			}
			else
			{
				return Posef.identity;
			}
		}
		else
		{
			return Posef.identity;
		}
#endif
	}

	public static ControllerState GetControllerState(uint controllerMask)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return new ControllerState();
#else
		return OVRP_1_1_0.ovrp_GetControllerState(controllerMask);
#endif
	}

	public static ControllerState2 GetControllerState2(uint controllerMask)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return new ControllerState2();
#else
		if (version >= OVRP_1_12_0.version)
		{
			return OVRP_1_12_0.ovrp_GetControllerState2(controllerMask);
		}

		return new ControllerState2(OVRP_1_1_0.ovrp_GetControllerState(controllerMask));
#endif
	}

	public static ControllerState4 GetControllerState4(uint controllerMask)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return new ControllerState4();
#else
		if (version >= OVRP_1_16_0.version)
		{
			ControllerState4 controllerState = new ControllerState4();
			OVRP_1_16_0.ovrp_GetControllerState4(controllerMask, ref controllerState);
			return controllerState;
		}

		return new ControllerState4(GetControllerState2(controllerMask));
#endif
	}

	public static bool SetControllerVibration(uint controllerMask, float frequency, float amplitude)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		return OVRP_0_1_2.ovrp_SetControllerVibration(controllerMask, frequency, amplitude) == Bool.True;
#endif
	}

	public static HapticsDesc GetControllerHapticsDesc(uint controllerMask)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return new HapticsDesc();
#else
		if (version >= OVRP_1_6_0.version)
		{
			return OVRP_1_6_0.ovrp_GetControllerHapticsDesc(controllerMask);
		}
		else
		{
			return new HapticsDesc();
		}
#endif
	}

	public static HapticsState GetControllerHapticsState(uint controllerMask)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return new HapticsState();
#else
		if (version >= OVRP_1_6_0.version)
		{
			return OVRP_1_6_0.ovrp_GetControllerHapticsState(controllerMask);
		}
		else
		{
			return new HapticsState();
		}
#endif
	}

	public static bool SetControllerHaptics(uint controllerMask, HapticsBuffer hapticsBuffer)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_6_0.version)
		{
			return OVRP_1_6_0.ovrp_SetControllerHaptics(controllerMask, hapticsBuffer) == Bool.True;
		}
		else
		{
			return false;
		}
#endif
	}

	public static float GetEyeRecommendedResolutionScale()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return 1.0f;
#else
		if (version >= OVRP_1_6_0.version)
		{
			return OVRP_1_6_0.ovrp_GetEyeRecommendedResolutionScale();
		}
		else
		{
			return 1.0f;
		}
#endif
	}

	public static float GetAppCpuStartToGpuEndTime()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return 0.0f;
#else
		if (version >= OVRP_1_6_0.version)
		{
			return OVRP_1_6_0.ovrp_GetAppCpuStartToGpuEndTime();
		}
		else
		{
			return 0.0f;
		}
#endif
	}

	public static bool GetBoundaryConfigured()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_8_0.version)
		{
			return OVRP_1_8_0.ovrp_GetBoundaryConfigured() == OVRPlugin.Bool.True;
		}
		else
		{
			return false;
		}
#endif
	}

	public static BoundaryTestResult TestBoundaryNode(Node nodeId, BoundaryType boundaryType)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return new BoundaryTestResult();
#else
		if (version >= OVRP_1_8_0.version)
		{
			return OVRP_1_8_0.ovrp_TestBoundaryNode(nodeId, boundaryType);
		}
		else
		{
			return new BoundaryTestResult();
		}
#endif
	}

	public static BoundaryTestResult TestBoundaryPoint(Vector3f point, BoundaryType boundaryType)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return new BoundaryTestResult();
#else
		if (version >= OVRP_1_8_0.version)
		{
			return OVRP_1_8_0.ovrp_TestBoundaryPoint(point, boundaryType);
		}
		else
		{
			return new BoundaryTestResult();
		}
#endif
	}

	public static BoundaryGeometry GetBoundaryGeometry(BoundaryType boundaryType)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return new BoundaryGeometry();
#else
		if (version >= OVRP_1_8_0.version)
		{
			return OVRP_1_8_0.ovrp_GetBoundaryGeometry(boundaryType);
		}
		else
		{
			return new BoundaryGeometry();
		}
#endif
	}

	public static bool GetBoundaryGeometry2(BoundaryType boundaryType, IntPtr points, ref int pointsCount)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		pointsCount = 0;
		return false;
#else
		if (version >= OVRP_1_9_0.version)
		{
			return OVRP_1_9_0.ovrp_GetBoundaryGeometry2(boundaryType, points, ref pointsCount) == OVRPlugin.Bool.True;
		}
		else
		{
			pointsCount = 0;

			return false;
		}
#endif
	}

	public static AppPerfStats GetAppPerfStats()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return new AppPerfStats();
#else
		if (version >= OVRP_1_9_0.version)
		{
			return OVRP_1_9_0.ovrp_GetAppPerfStats();
		}
		else
		{
			return new AppPerfStats();
		}
#endif
	}

	public static bool ResetAppPerfStats()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else

		if (version >= OVRP_1_9_0.version)
		{
			return OVRP_1_9_0.ovrp_ResetAppPerfStats() == OVRPlugin.Bool.True;
		}
		else
		{
			return false;
		}
#endif
	}

	public static float GetAppFramerate()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return 0.0f;
#else
		if (version >= OVRP_1_12_0.version)
		{
			return OVRP_1_12_0.ovrp_GetAppFramerate();
		}
		else
		{
			return 0.0f;
		}
#endif
	}

	public static bool SetHandNodePoseStateLatency(double latencyInSeconds)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_18_0.version)
		{
			Result result = OVRP_1_18_0.ovrp_SetHandNodePoseStateLatency(latencyInSeconds);
			if (result == Result.Success)
			{
				return true;
			}
			else
			{
				//Debug.LogWarning("ovrp_SetHandNodePoseStateLatency return " + result);
				return false;
			}
		}
		else
		{
			return false;
		}
#endif
	}

	public static double GetHandNodePoseStateLatency()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return 0.0;
#else
		if (version >= OVRP_1_18_0.version)
		{
			double value = 0.0;
			if (OVRP_1_18_0.ovrp_GetHandNodePoseStateLatency(out value) == OVRPlugin.Result.Success)
			{
				return value;
			}
			else
			{
				return 0.0;
			}
		}
		else
		{
			return 0.0;
		}
#endif
	}

	public static EyeTextureFormat GetDesiredEyeTextureFormat()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return EyeTextureFormat.Default;
#else
		if (version >= OVRP_1_11_0.version)
		{
			uint eyeTextureFormatValue = (uint)OVRP_1_11_0.ovrp_GetDesiredEyeTextureFormat();

			// convert both R8G8B8A8 and R8G8B8A8_SRGB to R8G8B8A8 here for avoid confusing developers
			if (eyeTextureFormatValue == 1)
				eyeTextureFormatValue = 0;

			return (EyeTextureFormat)eyeTextureFormatValue;
		}
		else
		{
			return EyeTextureFormat.Default;
		}
#endif
	}

	public static bool SetDesiredEyeTextureFormat(EyeTextureFormat value)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_11_0.version)
		{
			return OVRP_1_11_0.ovrp_SetDesiredEyeTextureFormat(value) == OVRPlugin.Bool.True;
		}
		else
		{
			return false;
		}
#endif
	}

	public static bool InitializeMixedReality()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else

#if OVRPLUGIN_INCLUDE_MRC_ANDROID
		if (version >= OVRP_1_38_0.version)		// MRC functions are invalid before 1.38.0
#else
		if (version >= OVRP_1_15_0.version)
#endif
		{
			Result result = OVRP_1_15_0.ovrp_InitializeMixedReality();
			if (result != Result.Success)
			{
				//Debug.LogWarning("ovrp_InitializeMixedReality return " + result);
			}
			return result == Result.Success;
		}
		else
		{
			return false;
		}
#endif
	}

	public static bool ShutdownMixedReality()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else

#if OVRPLUGIN_INCLUDE_MRC_ANDROID
		if (version >= OVRP_1_38_0.version)		// MRC functions are invalid before 1.38.0
#else
		if (version >= OVRP_1_15_0.version)
#endif
		{
			Result result = OVRP_1_15_0.ovrp_ShutdownMixedReality();
			if (result != Result.Success)
			{
				//Debug.LogWarning("ovrp_ShutdownMixedReality return " + result);
			}
			return result == Result.Success;
		}
		else
		{
			return false;
		}
#endif
	}

	public static bool IsMixedRealityInitialized()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else

#if OVRPLUGIN_INCLUDE_MRC_ANDROID
		if (version >= OVRP_1_38_0.version)		// MRC functions are invalid before 1.38.0
#else
		if (version >= OVRP_1_15_0.version)
#endif
		{
			return OVRP_1_15_0.ovrp_GetMixedRealityInitialized() == Bool.True;
		}
		else
		{
			return false;
		}
#endif
	}

	public static int GetExternalCameraCount()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return 0;
#else

#if OVRPLUGIN_INCLUDE_MRC_ANDROID
		if (version >= OVRP_1_38_0.version)		// MRC functions are invalid before 1.38.0
#else
		if (version >= OVRP_1_15_0.version)
#endif
		{
			int cameraCount = 0;
			Result result = OVRP_1_15_0.ovrp_GetExternalCameraCount(out cameraCount);
			if (result != OVRPlugin.Result.Success)
			{
				//Debug.LogWarning("ovrp_GetExternalCameraCount return " + result);
				return 0;
			}

			return cameraCount;
		}
		else
		{
			return 0;
		}
#endif
	}

	public static bool UpdateExternalCamera()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else

#if OVRPLUGIN_INCLUDE_MRC_ANDROID
		if (version >= OVRP_1_38_0.version)		// MRC functions are invalid before 1.38.0
#else
		if (version >= OVRP_1_15_0.version)
#endif
		{
			Result result = OVRP_1_15_0.ovrp_UpdateExternalCamera();
			if (result != Result.Success)
			{
				//Debug.LogWarning("ovrp_UpdateExternalCamera return " + result);
			}
			return result == Result.Success;
		}
		else
		{
			return false;
		}
#endif
	}

	public static bool GetMixedRealityCameraInfo(int cameraId, out CameraExtrinsics cameraExtrinsics, out CameraIntrinsics cameraIntrinsics, out Posef calibrationRawPose)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		cameraExtrinsics = default(CameraExtrinsics);
		cameraIntrinsics = default(CameraIntrinsics);
		calibrationRawPose = Posef.identity;
		return false;
#else

		cameraExtrinsics = default(CameraExtrinsics);
		cameraIntrinsics = default(CameraIntrinsics);
		calibrationRawPose = Posef.identity;

#if OVRPLUGIN_INCLUDE_MRC_ANDROID
		if (version >= OVRP_1_38_0.version)		// MRC functions are invalid before 1.38.0
#else
		if (version >= OVRP_1_15_0.version)
#endif
		{
			bool retValue = true;

			Result result = OVRP_1_15_0.ovrp_GetExternalCameraExtrinsics(cameraId, out cameraExtrinsics);
			if (result != Result.Success)
			{
				retValue = false;
				//Debug.LogWarning("ovrp_GetExternalCameraExtrinsics return " + result);
			}

			result = OVRP_1_15_0.ovrp_GetExternalCameraIntrinsics(cameraId, out cameraIntrinsics);
			if (result != Result.Success)
			{
				retValue = false;
				//Debug.LogWarning("ovrp_GetExternalCameraIntrinsics return " + result);
			}

#if OVRPLUGIN_INCLUDE_MRC_ANDROID
            result = OVRP_1_38_0.ovrp_GetExternalCameraCalibrationRawPose(cameraId, out calibrationRawPose);
			if (result != Result.Success)
			{
				retValue = false;
				//Debug.LogWarning("ovrp_GetExternalCameraCalibrationRawPose return " + result);
			}
#endif

			return retValue;
		}
		else
		{
			return false;
		}
#endif
	}

	public static Vector3f GetBoundaryDimensions(BoundaryType boundaryType)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return new Vector3f();
#else
		if (version >= OVRP_1_8_0.version)
		{
			return OVRP_1_8_0.ovrp_GetBoundaryDimensions(boundaryType);
		}
		else
		{
			return new Vector3f();
		}
#endif
	}

	public static bool GetBoundaryVisible()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_8_0.version)
		{
			return OVRP_1_8_0.ovrp_GetBoundaryVisible() == OVRPlugin.Bool.True;
		}
		else
		{
			return false;
		}
#endif
	}

	public static bool SetBoundaryVisible(bool value)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_8_0.version)
		{
			return OVRP_1_8_0.ovrp_SetBoundaryVisible(ToBool(value)) == OVRPlugin.Bool.True;
		}
		else
		{
			return false;
		}
#endif
	}

	public static SystemHeadset GetSystemHeadsetType()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return SystemHeadset.None;
#else
		if (version >= OVRP_1_9_0.version)
			return OVRP_1_9_0.ovrp_GetSystemHeadsetType();

		return SystemHeadset.None;
#endif
	}

	public static Controller GetActiveController()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return Controller.None;
#else
		if (version >= OVRP_1_9_0.version)
			return OVRP_1_9_0.ovrp_GetActiveController();

		return Controller.None;
#endif
	}

	public static Controller GetConnectedControllers()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return Controller.None;
#else
		if (version >= OVRP_1_9_0.version)
			return OVRP_1_9_0.ovrp_GetConnectedControllers();

		return Controller.None;
#endif
	}

	private static Bool ToBool(bool b)
	{
		return (b) ? OVRPlugin.Bool.True : OVRPlugin.Bool.False;
	}

	public static TrackingOrigin GetTrackingOriginType()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return default(TrackingOrigin);
#else
		return OVRP_1_0_0.ovrp_GetTrackingOriginType();
#endif
	}

	public static bool SetTrackingOriginType(TrackingOrigin originType)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		return OVRP_1_0_0.ovrp_SetTrackingOriginType(originType) == Bool.True;
#endif
	}

	public static Posef GetTrackingCalibratedOrigin()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return Posef.identity;
#else
		return OVRP_1_0_0.ovrp_GetTrackingCalibratedOrigin();
#endif
	}

	public static bool SetTrackingCalibratedOrigin()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		return OVRP_1_2_0.ovrpi_SetTrackingCalibratedOrigin() == Bool.True;
#endif
	}

	public static bool RecenterTrackingOrigin(RecenterFlags flags)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		return OVRP_1_0_0.ovrp_RecenterTrackingOrigin((uint)flags) == Bool.True;
#endif
	}

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
	public static bool UpdateCameraDevices()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_16_0.version)
		{
			Result result = OVRP_1_16_0.ovrp_UpdateCameraDevices();
			if (result != Result.Success)
			{
				//Debug.LogWarning("ovrp_UpdateCameraDevices return " + result);
			}
			return result == Result.Success;
		}
		else
		{
			return false;
		}
#endif
	}

	public static bool IsCameraDeviceAvailable(CameraDevice cameraDevice)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_16_0.version)
		{
			Bool result = OVRP_1_16_0.ovrp_IsCameraDeviceAvailable(cameraDevice);
			return result == Bool.True;
		}
		else
		{
			return false;
		}
#endif
	}

	public static bool SetCameraDevicePreferredColorFrameSize(CameraDevice cameraDevice, int width, int height)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_16_0.version)
		{
			Sizei size = new Sizei();
			size.w = width;
			size.h = height;
			Result result = OVRP_1_16_0.ovrp_SetCameraDevicePreferredColorFrameSize(cameraDevice, size);
			if (result != Result.Success)
			{
				//Debug.LogWarning("ovrp_SetCameraDevicePreferredColorFrameSize return " + result);
			}
			return result == Result.Success;
		}
		else
		{
			return false;
		}
#endif
	}

	public static bool OpenCameraDevice(CameraDevice cameraDevice)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_16_0.version)
		{
			Result result = OVRP_1_16_0.ovrp_OpenCameraDevice(cameraDevice);
			if (result != Result.Success)
			{
				//Debug.LogWarning("ovrp_OpenCameraDevice return " + result);
			}
			return result == Result.Success;
		}
		else
		{
			return false;
		}
#endif
	}

	public static bool CloseCameraDevice(CameraDevice cameraDevice)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_16_0.version)
		{
			Result result = OVRP_1_16_0.ovrp_CloseCameraDevice(cameraDevice);
			if (result != Result.Success)
			{
				//Debug.LogWarning("ovrp_OpenCameraDevice return " + result);
			}
			return result == Result.Success;
		}
		else
		{
			return false;
		}
#endif
	}

	public static bool HasCameraDeviceOpened(CameraDevice cameraDevice)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_16_0.version)
		{
			Bool result = OVRP_1_16_0.ovrp_HasCameraDeviceOpened(cameraDevice);
			return result == Bool.True;
		}
		else
		{
			return false;
		}
#endif
	}

	public static bool IsCameraDeviceColorFrameAvailable(CameraDevice cameraDevice)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_16_0.version)
		{
			Bool result = OVRP_1_16_0.ovrp_IsCameraDeviceColorFrameAvailable(cameraDevice);
			return result == Bool.True;
		}
		else
		{
			return false;
		}
#endif
	}

#if !OVRPLUGIN_UNSUPPORTED_PLATFORM
	private static Texture2D cachedCameraFrameTexture = null;
#endif
	public static Texture2D GetCameraDeviceColorFrameTexture(CameraDevice cameraDevice)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return null;
#else
		if (version >= OVRP_1_16_0.version)
		{
			Sizei size = new Sizei();
			Result result = OVRP_1_16_0.ovrp_GetCameraDeviceColorFrameSize(cameraDevice, out size);
			if (result != Result.Success)
			{
				//Debug.LogWarning("ovrp_GetCameraDeviceColorFrameSize return " + result);
				return null;
			}
			IntPtr pixels;
			int rowPitch;
			result = OVRP_1_16_0.ovrp_GetCameraDeviceColorFrameBgraPixels(cameraDevice, out pixels, out rowPitch);
			if (result != Result.Success)
			{
				//Debug.LogWarning("ovrp_GetCameraDeviceColorFrameBgraPixels return " + result);
				return null;
			}
			if (rowPitch != size.w * 4)
			{
				//Debug.LogWarning(string.Format("RowPitch mismatch, expected {0}, get {1}", size.w * 4, rowPitch));
				return null;
			}
			if (!cachedCameraFrameTexture || cachedCameraFrameTexture.width != size.w || cachedCameraFrameTexture.height != size.h)
			{
				cachedCameraFrameTexture = new Texture2D(size.w, size.h, TextureFormat.BGRA32, false);
			}
			cachedCameraFrameTexture.LoadRawTextureData(pixels, rowPitch * size.h);
			cachedCameraFrameTexture.Apply();
			return cachedCameraFrameTexture;
		}
		else
		{
			return null;
		}
#endif
	}

	public static bool DoesCameraDeviceSupportDepth(CameraDevice cameraDevice)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_17_0.version)
		{
			Bool supportDepth;
			Result result = OVRP_1_17_0.ovrp_DoesCameraDeviceSupportDepth(cameraDevice, out supportDepth);
			return result == Result.Success && supportDepth == Bool.True;
		}
		else
		{
			return false;
		}
#endif
	}

	public static bool SetCameraDeviceDepthSensingMode(CameraDevice camera, CameraDeviceDepthSensingMode depthSensoringMode)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_17_0.version)
		{
			Result result = OVRP_1_17_0.ovrp_SetCameraDeviceDepthSensingMode(camera, depthSensoringMode);
			return result == Result.Success;
		}
		else
		{
			return false;
		}
#endif
	}

	public static bool SetCameraDevicePreferredDepthQuality(CameraDevice camera, CameraDeviceDepthQuality depthQuality)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_17_0.version)
		{
			Result result = OVRP_1_17_0.ovrp_SetCameraDevicePreferredDepthQuality(camera, depthQuality);
			return result == Result.Success;
		}
		else
		{
			return false;
		}
#endif
	}

	public static bool IsCameraDeviceDepthFrameAvailable(CameraDevice cameraDevice)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_17_0.version)
		{
			Bool available;
			Result result = OVRP_1_17_0.ovrp_IsCameraDeviceDepthFrameAvailable(cameraDevice, out available);
			return result == Result.Success && available == Bool.True;
		}
		else
		{
			return false;
		}
#endif
	}

#if !OVRPLUGIN_UNSUPPORTED_PLATFORM
	private static Texture2D cachedCameraDepthTexture = null;
#endif
	public static Texture2D GetCameraDeviceDepthFrameTexture(CameraDevice cameraDevice)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return null;
#else
		if (version >= OVRP_1_17_0.version)
		{
			Sizei size = new Sizei();
			Result result = OVRP_1_17_0.ovrp_GetCameraDeviceDepthFrameSize(cameraDevice, out size);
			if (result != Result.Success)
			{
				//Debug.LogWarning("ovrp_GetCameraDeviceDepthFrameSize return " + result);
				return null;
			}
			IntPtr depthData;
			int rowPitch;
			result = OVRP_1_17_0.ovrp_GetCameraDeviceDepthFramePixels(cameraDevice, out depthData, out rowPitch);
			if (result != Result.Success)
			{
				//Debug.LogWarning("ovrp_GetCameraDeviceDepthFramePixels return " + result);
				return null;
			}
			if (rowPitch != size.w * 4)
			{
				//Debug.LogWarning(string.Format("RowPitch mismatch, expected {0}, get {1}", size.w * 4, rowPitch));
				return null;
			}
			if (!cachedCameraDepthTexture || cachedCameraDepthTexture.width != size.w || cachedCameraDepthTexture.height != size.h)
			{
				cachedCameraDepthTexture = new Texture2D(size.w, size.h, TextureFormat.RFloat, false);
				cachedCameraDepthTexture.filterMode = FilterMode.Point;
			}
			cachedCameraDepthTexture.LoadRawTextureData(depthData, rowPitch * size.h);
			cachedCameraDepthTexture.Apply();
			return cachedCameraDepthTexture;
		}
		else
		{
			return null;
		}
#endif
	}

#if !OVRPLUGIN_UNSUPPORTED_PLATFORM
	private static Texture2D cachedCameraDepthConfidenceTexture = null;
#endif
	public static Texture2D GetCameraDeviceDepthConfidenceTexture(CameraDevice cameraDevice)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return null;
#else
		if (version >= OVRP_1_17_0.version)
		{
			Sizei size = new Sizei();
			Result result = OVRP_1_17_0.ovrp_GetCameraDeviceDepthFrameSize(cameraDevice, out size);
			if (result != Result.Success)
			{
				//Debug.LogWarning("ovrp_GetCameraDeviceDepthFrameSize return " + result);
				return null;
			}
			IntPtr confidenceData;
			int rowPitch;
			result = OVRP_1_17_0.ovrp_GetCameraDeviceDepthConfidencePixels(cameraDevice, out confidenceData, out rowPitch);
			if (result != Result.Success)
			{
				//Debug.LogWarning("ovrp_GetCameraDeviceDepthConfidencePixels return " + result);
				return null;
			}
			if (rowPitch != size.w * 4)
			{
				//Debug.LogWarning(string.Format("RowPitch mismatch, expected {0}, get {1}", size.w * 4, rowPitch));
				return null;
			}
			if (!cachedCameraDepthConfidenceTexture || cachedCameraDepthConfidenceTexture.width != size.w || cachedCameraDepthConfidenceTexture.height != size.h)
			{
				cachedCameraDepthConfidenceTexture = new Texture2D(size.w, size.h, TextureFormat.RFloat, false);
			}
			cachedCameraDepthConfidenceTexture.LoadRawTextureData(confidenceData, rowPitch * size.h);
			cachedCameraDepthConfidenceTexture.Apply();
			return cachedCameraDepthConfidenceTexture;
		}
		else
		{
			return null;
		}
#endif
	}
#endif

	public static bool fixedFoveatedRenderingSupported
	{
		get
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (version >= OVRP_1_21_0.version)
			{
				Bool supported;
				Result result = OVRP_1_21_0.ovrp_GetTiledMultiResSupported(out supported);
				if (result == Result.Success)
				{
					return supported == Bool.True;
				}
				else
				{
					//Debug.LogWarning("ovrp_GetTiledMultiResSupported return " + result);
					return false;
				}
			}
			else
			{
				return false;
			}
#endif
		}
	}

	public static FixedFoveatedRenderingLevel fixedFoveatedRenderingLevel
	{
		get
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return FixedFoveatedRenderingLevel.Off;
#else
			if (version >= OVRP_1_21_0.version && fixedFoveatedRenderingSupported)
			{
				FixedFoveatedRenderingLevel level;
				Result result = OVRP_1_21_0.ovrp_GetTiledMultiResLevel(out level);
				if (result != Result.Success)
				{
					//Debug.LogWarning("ovrp_GetTiledMultiResLevel return " + result);
				}
				return level;
			}
			else
			{
				return FixedFoveatedRenderingLevel.Off;
			}
#endif
		}
		set
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return;
#else
			if (version >= OVRP_1_21_0.version && fixedFoveatedRenderingSupported)
			{
				Result result = OVRP_1_21_0.ovrp_SetTiledMultiResLevel(value);
				if (result != Result.Success)
				{
					//Debug.LogWarning("ovrp_SetTiledMultiResLevel return " + result);
				}
			}
#endif
		}
	}

	[Obsolete("Please use fixedFoveatedRenderingSupported instead", false)]
	public static bool tiledMultiResSupported
	{
		get
		{
			return fixedFoveatedRenderingSupported;
		}
	}

	[Obsolete("Please use fixedFoveatedRenderingLevel instead", false)]
	public static TiledMultiResLevel tiledMultiResLevel
	{
		get
		{
			return (TiledMultiResLevel)fixedFoveatedRenderingLevel;
		}
		set
		{
			fixedFoveatedRenderingLevel = (FixedFoveatedRenderingLevel)value;
		}
	}

	public static bool gpuUtilSupported
	{
		get
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (version >= OVRP_1_21_0.version)
			{
				Bool supported;
				Result result = OVRP_1_21_0.ovrp_GetGPUUtilSupported(out supported);
				if (result == Result.Success)
				{
					return supported == Bool.True;
				}
				else
				{
					//Debug.LogWarning("ovrp_GetGPUUtilSupported return " + result);
					return false;
				}
			}
			else
			{
				return false;
			}
#endif
		}
	}

	public static float gpuUtilLevel
	{
		get
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return 0.0f;
#else
			if (version >= OVRP_1_21_0.version && gpuUtilSupported)
			{
				float level;
				Result result = OVRP_1_21_0.ovrp_GetGPUUtilLevel(out level);
				if (result == Result.Success)
				{
					return level;
				}
				else
				{
					//Debug.LogWarning("ovrp_GetGPUUtilLevel return " + result);
					return 0.0f;
				}
			}
			else
			{
				return 0.0f;
			}
#endif
		}
	}

#if !OVRPLUGIN_UNSUPPORTED_PLATFORM
	private static OVRNativeBuffer _nativeSystemDisplayFrequenciesAvailable = null;
	private static float[] _cachedSystemDisplayFrequenciesAvailable = null;
#endif

	public static float[] systemDisplayFrequenciesAvailable
	{
		get
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return new float[0];
#else
			if (_cachedSystemDisplayFrequenciesAvailable == null)
			{
				_cachedSystemDisplayFrequenciesAvailable = new float[0];

				if (version >= OVRP_1_21_0.version)
				{
					int numFrequencies = 0;
					Result result = OVRP_1_21_0.ovrp_GetSystemDisplayAvailableFrequencies(IntPtr.Zero, ref numFrequencies);
					if (result == Result.Success)
					{
						if (numFrequencies > 0)
						{
							int maxNumElements = numFrequencies;
							_nativeSystemDisplayFrequenciesAvailable = new OVRNativeBuffer(sizeof(float) * maxNumElements);
							result = OVRP_1_21_0.ovrp_GetSystemDisplayAvailableFrequencies(_nativeSystemDisplayFrequenciesAvailable.GetPointer(), ref numFrequencies);
							if (result == Result.Success)
							{
								int numElementsToCopy = (numFrequencies <= maxNumElements) ? numFrequencies : maxNumElements;
								if (numElementsToCopy > 0)
								{
									_cachedSystemDisplayFrequenciesAvailable = new float[numElementsToCopy];
									Marshal.Copy(_nativeSystemDisplayFrequenciesAvailable.GetPointer(), _cachedSystemDisplayFrequenciesAvailable, 0, numElementsToCopy);
								}
							}
						}
					}
				}
			}

			return _cachedSystemDisplayFrequenciesAvailable;
#endif
		}
	}

	public static float systemDisplayFrequency
	{
		get
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return 0.0f;
#else
			if (version >= OVRP_1_21_0.version)
			{
				float displayFrequency;
				Result result = OVRP_1_21_0.ovrp_GetSystemDisplayFrequency2(out displayFrequency);
				if (result == Result.Success)
				{
					return displayFrequency;
				}

				return 0.0f;
			}
			else if (version >= OVRP_1_1_0.version)
			{
				return OVRP_1_1_0.ovrp_GetSystemDisplayFrequency();
			}
			else
			{
				return 0.0f;
			}
#endif
		}
		set
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return;
#else
			if (version >= OVRP_1_21_0.version)
			{
				OVRP_1_21_0.ovrp_SetSystemDisplayFrequency(value);
			}
#endif
		}
	}

	public static bool GetNodeFrustum2(Node nodeId, out Frustumf2 frustum)
	{
		frustum = default(Frustumf2);

#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_15_0.version)
		{
			Result result = OVRP_1_15_0.ovrp_GetNodeFrustum2(nodeId, out frustum);
			if (result != Result.Success)
			{
				return false;
			}
			else
			{
				return true;
			}
		}
		else
		{
			return false;
		}
#endif
	}

	public static bool AsymmetricFovEnabled
	{
		get
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (version >= OVRP_1_21_0.version)
			{
				Bool asymmetricFovEnabled = Bool.False;
				Result result = OVRP_1_21_0.ovrp_GetAppAsymmetricFov(out asymmetricFovEnabled);

				if (result != Result.Success)
				{
					return false;
				}
				else
				{
					return asymmetricFovEnabled == Bool.True;
				}
			}
			else
			{
				return false;
			}
#endif
		}
	}

	public static bool EyeTextureArrayEnabled
	{
		get
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (version >= OVRP_1_15_0.version)
			{
				Bool enabled = Bool.False;
				enabled = OVRP_1_15_0.ovrp_GetEyeTextureArrayEnabled();
				return enabled == Bool.True;
			}
			else
			{
				return false;
			}
#endif
		}
	}


	public static Handedness GetDominantHand()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return Handedness.Unsupported;
#else
		Handedness dominantHand;

		if (version >= OVRP_1_28_0.version && OVRP_1_28_0.ovrp_GetDominantHand(out dominantHand) == Result.Success)
		{
			return dominantHand;
		}

		return Handedness.Unsupported;
#endif
	}

	public static bool GetReorientHMDOnControllerRecenter()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		Bool recenterMode;
		if (version < OVRP_1_28_0.version || OVRP_1_28_0.ovrp_GetReorientHMDOnControllerRecenter(out recenterMode) != Result.Success)
			return false;

		return (recenterMode == Bool.True);
#endif
	}

	public static bool SetReorientHMDOnControllerRecenter(bool recenterSetting)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		Bool ovrpBoolRecenterSetting = recenterSetting ? Bool.True : Bool.False;
		if (version < OVRP_1_28_0.version || OVRP_1_28_0.ovrp_SetReorientHMDOnControllerRecenter(ovrpBoolRecenterSetting) != Result.Success)
			return false;

		return true;
#endif
	}

	public static bool SendEvent(string name, string param = "", string source = "")
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_30_0.version)
		{
			return OVRP_1_30_0.ovrp_SendEvent2(name, param, source.Length == 0 ? "integration": source) == Result.Success;
		}
		else if (version >= OVRP_1_28_0.version)
		{
			return OVRP_1_28_0.ovrp_SendEvent(name, param) == Result.Success;
		}
		else
		{
			return false;
		}
#endif
	}

	public static bool SetHeadPoseModifier(ref Quatf relativeRotation, ref Vector3f relativeTranslation)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_29_0.version)
		{
			return OVRP_1_29_0.ovrp_SetHeadPoseModifier(ref relativeRotation, ref relativeTranslation) == Result.Success;
		}
		else
		{
			return false;
		}
#endif
	}

	public static bool GetHeadPoseModifier(out Quatf relativeRotation, out Vector3f relativeTranslation)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		relativeRotation = Quatf.identity;
		relativeTranslation = Vector3f.zero;
		return false;
#else
		if (version >= OVRP_1_29_0.version)
		{
			return OVRP_1_29_0.ovrp_GetHeadPoseModifier(out relativeRotation, out relativeTranslation) == Result.Success;
		}
		else
		{
			relativeRotation = Quatf.identity;
			relativeTranslation = Vector3f.zero;
			return false;
		}
#endif
	}

	public static bool IsPerfMetricsSupported(PerfMetrics perfMetrics)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_30_0.version)
		{
			Bool isSupported;
			Result result = OVRP_1_30_0.ovrp_IsPerfMetricsSupported(perfMetrics, out isSupported);
			if (result == Result.Success)
			{
				return isSupported == Bool.True;
			}
			else
			{
				return false;
			}
		}
		else
		{
			return false;
		}
#endif
	}

	public static float? GetPerfMetricsFloat(PerfMetrics perfMetrics)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return null;
#else
		if (version >= OVRP_1_30_0.version)
		{
			float value;
			Result result = OVRP_1_30_0.ovrp_GetPerfMetricsFloat(perfMetrics, out value);
			if (result == Result.Success)
			{
				return value;
			}
			else
			{
				return null;
			}
		}
		else
		{
			return null;
		}
#endif
	}

	public static int? GetPerfMetricsInt(PerfMetrics perfMetrics)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return null;
#else
		if (version >= OVRP_1_30_0.version)
		{
			int value;
			Result result = OVRP_1_30_0.ovrp_GetPerfMetricsInt(perfMetrics, out value);
			if (result == Result.Success)
			{
				return value;
			}
			else
			{
				return null;
			}
		}
		else
		{
			return null;
		}
#endif
	}

	public static double GetTimeInSeconds()
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return 0.0;
#else
		if (version >= OVRP_1_31_0.version)
		{
			double value;
			Result result = OVRP_1_31_0.ovrp_GetTimeInSeconds(out value);
			if (result == Result.Success)
			{
				return value;
			}
			else
			{
				return 0.0;
			}
		}
		else
		{
			return 0.0;
		}
#endif
	}

	public static bool SetColorScaleAndOffset(Vector4 colorScale, Vector4 colorOffset, bool applyToAllLayers)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_31_0.version)
		{
			Bool ovrpApplyToAllLayers = applyToAllLayers ? Bool.True : Bool.False;
			return OVRP_1_31_0.ovrp_SetColorScaleAndOffset(colorScale, colorOffset, ovrpApplyToAllLayers) == Result.Success;
		}
		else
		{
			return false;
		}
#endif
	}

	public static bool AddCustomMetadata(string name, string param = "")
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if (version >= OVRP_1_32_0.version)
		{
			return OVRP_1_32_0.ovrp_AddCustomMetadata(name, param) == Result.Success;
		}
		else
		{
			return false;
		}
#endif
	}

	public class Media
	{
		public enum MrcActivationMode
		{
			Automatic = 0,
			Disabled = 1,
			EnumSize = 0x7fffffff
		}

		public enum InputVideoBufferType
		{
			Memory = 0,
			TextureHandle = 1,
			EnumSize = 0x7fffffff
		}

		public static bool Initialize()
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (version >= OVRP_1_38_0.version)
			{
				return OVRP_1_38_0.ovrp_Media_Initialize() == Result.Success;
			}
			else
			{
				return false;
			}
#endif
		}

		public static bool Shutdown()
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (version >= OVRP_1_38_0.version)
			{
				return OVRP_1_38_0.ovrp_Media_Shutdown() == Result.Success;
			}
			else
			{
				return false;
			}
#endif
		}

		public static bool GetInitialized()
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (version >= OVRP_1_38_0.version)
			{
				Bool initialized = Bool.False;
				Result result = OVRP_1_38_0.ovrp_Media_GetInitialized(out initialized);
				if (result == Result.Success)
				{
					return initialized == Bool.True ? true : false;
				}
				else
				{
					return false;
				}
			}
			else
			{
				return false;
			}
#endif
		}

		public static bool Update()
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (version >= OVRP_1_38_0.version)
			{
				return OVRP_1_38_0.ovrp_Media_Update() == Result.Success;
			}
			else
			{
				return false;
			}
#endif
		}

		public static MrcActivationMode GetMrcActivationMode()
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return MrcActivationMode.Disabled;
#else
			if (version >= OVRP_1_38_0.version)
			{
				MrcActivationMode mode;
				if (OVRP_1_38_0.ovrp_Media_GetMrcActivationMode(out mode) == Result.Success)
				{
					return mode;
				}
				else
				{
					return default(MrcActivationMode);
				}
			}
			else
			{
				return default(MrcActivationMode);
			}
#endif
		}

		public static bool SetMrcActivationMode(MrcActivationMode mode)
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (version >= OVRP_1_38_0.version)
			{
				return OVRP_1_38_0.ovrp_Media_SetMrcActivationMode(mode) == Result.Success;
			}
			else
			{
				return false;
			}
#endif
		}

		public static bool IsMrcEnabled()
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (version >= OVRP_1_38_0.version)
			{
				Bool b;
				if (OVRP_1_38_0.ovrp_Media_IsMrcEnabled(out b) == Result.Success)
				{
					return b == Bool.True ? true : false;
				}
				else
				{
					return false;
				}
			}
			else
			{
				return false;
			}
#endif
		}

		public static bool IsMrcActivated()
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (version >= OVRP_1_38_0.version)
			{
				Bool b;
				if (OVRP_1_38_0.ovrp_Media_IsMrcActivated(out b) == Result.Success)
				{
					return b == Bool.True ? true : false;
				}
				else
				{
					return false;
				}
			}
			else
			{
				return false;
			}
#endif
		}

		public static bool UseMrcDebugCamera()
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (version >= OVRP_1_38_0.version)
			{
				Bool b;
				if (OVRP_1_38_0.ovrp_Media_UseMrcDebugCamera(out b) == Result.Success)
				{
					return b == Bool.True ? true : false;
				}
				else
				{
					return false;
				}
			}
			else
			{
				return false;
			}
#endif
		}

		public static bool SetMrcInputVideoBufferType(InputVideoBufferType videoBufferType)
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (version >= OVRP_1_38_0.version)
			{
				if (OVRP_1_38_0.ovrp_Media_SetMrcInputVideoBufferType(videoBufferType) == Result.Success)
				{
					return true;
				}
				else
				{
					return false;
				}
			}
			else
			{
				return false;
			}
#endif
		}

		public static InputVideoBufferType GetMrcInputVideoBufferType()
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return InputVideoBufferType.Memory;
#else
			if (version >= OVRP_1_38_0.version)
			{
				InputVideoBufferType videoBufferType = InputVideoBufferType.Memory;
				OVRP_1_38_0.ovrp_Media_GetMrcInputVideoBufferType(ref videoBufferType);
				return videoBufferType;
			}
			else
			{
				return InputVideoBufferType.Memory;
			}
#endif
		}

		public static bool SetMrcFrameSize(int frameWidth, int frameHeight)
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (version >= OVRP_1_38_0.version)
			{
				if (OVRP_1_38_0.ovrp_Media_SetMrcFrameSize(frameWidth, frameHeight) == Result.Success)
				{
					return true;
				}
				else
				{
					return false;
				}
			}
			else
			{
				return false;
			}
#endif
		}

		public static void GetMrcFrameSize(out int frameWidth, out int frameHeight)
		{

			frameWidth = -1;
			frameHeight = -1;
#if !OVRPLUGIN_UNSUPPORTED_PLATFORM
			if (version >= OVRP_1_38_0.version)
			{
				OVRP_1_38_0.ovrp_Media_GetMrcFrameSize(ref frameWidth, ref frameHeight);
			}
#endif
		}


		public static bool SetMrcAudioSampleRate(int sampleRate)
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (version >= OVRP_1_38_0.version)
			{
				if (OVRP_1_38_0.ovrp_Media_SetMrcAudioSampleRate(sampleRate) == Result.Success)
				{
					return true;
				}
				else
				{
					return false;
				}
			}
			else
			{
				return false;
			}
#endif
		}

		public static int GetMrcAudioSampleRate()
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return 0;
#else
			int sampleRate = 0;
			if (version >= OVRP_1_38_0.version)
			{
				OVRP_1_38_0.ovrp_Media_GetMrcAudioSampleRate(ref sampleRate);
			}
			return sampleRate;
#endif
		}

		public static bool SetMrcFrameImageFlipped(bool imageFlipped)
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (version >= OVRP_1_38_0.version)
			{
				Bool flipped = imageFlipped ? Bool.True : Bool.False;
				if (OVRP_1_38_0.ovrp_Media_SetMrcFrameImageFlipped(flipped) == Result.Success)
				{
					return true;
				}
				else
				{
					return false;
				}
			}
			else
			{
				return false;
			}
#endif
		}

		public static bool GetMrcFrameImageFlipped()
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			Bool flipped = 0;
			if (version >= OVRP_1_38_0.version)
			{
				OVRP_1_38_0.ovrp_Media_GetMrcFrameImageFlipped(ref flipped);
			}
			return flipped == Bool.True ? true : false;
#endif
		}

		public static bool EncodeMrcFrame(System.IntPtr textureHandle, float[] audioData, int audioFrames, int audioChannels, double timestamp, ref int outSyncId)
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (version >= OVRP_1_38_0.version)
			{
				if (textureHandle == System.IntPtr.Zero)
				{
					Debug.LogError("EncodeMrcFrame: textureHandle is null");
					return false;
				}
				InputVideoBufferType videoBufferType = GetMrcInputVideoBufferType();
				if (videoBufferType != InputVideoBufferType.TextureHandle)
				{
					Debug.LogError("EncodeMrcFrame: videoBufferType mismatch");
					return false;
				}
				GCHandle pinnedAudioData = new GCHandle();
				IntPtr audioDataPtr = IntPtr.Zero;
				int audioDataLen = 0;
				if (audioData != null)
				{
					pinnedAudioData = GCHandle.Alloc(audioData, GCHandleType.Pinned);
					audioDataPtr = pinnedAudioData.AddrOfPinnedObject();
					audioDataLen = audioFrames * 4;
				}
				Result result = OVRP_1_38_0.ovrp_Media_EncodeMrcFrame(textureHandle, audioDataPtr, audioDataLen, audioChannels, timestamp, ref outSyncId);
				if (audioData != null)
				{
					pinnedAudioData.Free();
				}
				return result == Result.Success;
			}
			else
			{
				return false;
			}
#endif
		}

#if !OVRPLUGIN_UNSUPPORTED_PLATFORM
		static Texture2D cachedTexture = null;
#endif
		public static bool EncodeMrcFrame(RenderTexture frame, float[] audioData, int audioFrames, int audioChannels, double timestamp, ref int outSyncId)
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (version >= OVRP_1_38_0.version)
			{
				if (frame == null)
				{
					Debug.LogError("EncodeMrcFrame: frame is null");
					return false;
				}
				InputVideoBufferType videoBufferType = GetMrcInputVideoBufferType();
				if (videoBufferType != InputVideoBufferType.Memory)
				{
					Debug.LogError("EncodeMrcFrame: videoBufferType mismatch");
					return false;
				}

				GCHandle pinnedArray = new GCHandle();
				IntPtr pointer = IntPtr.Zero;
				if (cachedTexture == null || cachedTexture.width != frame.width || cachedTexture.height != frame.height)
				{
					cachedTexture = new Texture2D(frame.width, frame.height, TextureFormat.ARGB32, false);
				}
				RenderTexture lastActive = RenderTexture.active;
				RenderTexture.active = frame;
				cachedTexture.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0);
				RenderTexture.active = lastActive;
				Color32[] bytes = cachedTexture.GetPixels32(0);
				pinnedArray = GCHandle.Alloc(bytes, GCHandleType.Pinned);
				pointer = pinnedArray.AddrOfPinnedObject();

				GCHandle pinnedAudioData = new GCHandle();
				IntPtr audioDataPtr = IntPtr.Zero;
				int audioDataLen = 0;
				if (audioData != null)
				{
					pinnedAudioData = GCHandle.Alloc(audioData, GCHandleType.Pinned);
					audioDataPtr = pinnedAudioData.AddrOfPinnedObject();
					audioDataLen = audioFrames * 4;
				}
				Result result = OVRP_1_38_0.ovrp_Media_EncodeMrcFrame(pointer, audioDataPtr, audioDataLen, audioChannels, timestamp, ref outSyncId);

				pinnedArray.Free();
				if (audioData != null)
				{
					pinnedAudioData.Free();
				}
				return result == Result.Success;
			}
			else
			{
				return false;
			}
#endif
		}

		public static bool SyncMrcFrame(int syncId)
		{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
			return false;
#else
			if (version >= OVRP_1_38_0.version)
			{
				return OVRP_1_38_0.ovrp_Media_SyncMrcFrame(syncId) == Result.Success;
			}
			else
			{
				return false;
			}
#endif
		}

	}

	public static bool SetDeveloperMode(Bool active)
	{
#if OVRPLUGIN_UNSUPPORTED_PLATFORM
		return false;
#else
		if(version >= OVRP_1_38_0.version)
		{
			return OVRP_1_38_0.ovrp_SetDeveloperMode(active) == Result.Success;
		}
		else
		{
			return false;
		}
#endif
	}

	private const string pluginName = "OVRPlugin";
	private static System.Version _versionZero = new System.Version(0, 0, 0);

	// Disable all the DllImports when the platform is not supported
#if !OVRPLUGIN_UNSUPPORTED_PLATFORM

	private static class OVRP_0_1_0
	{
		public static readonly System.Version version = new System.Version(0, 1, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Sizei ovrp_GetEyeTextureSize(Eye eyeId);
	}

	private static class OVRP_0_1_1
	{
		public static readonly System.Version version = new System.Version(0, 1, 1);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_SetOverlayQuad2(Bool onTop, Bool headLocked, IntPtr texture, IntPtr device, Posef pose, Vector3f scale);
	}

	private static class OVRP_0_1_2
	{
		public static readonly System.Version version = new System.Version(0, 1, 2);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Posef ovrp_GetNodePose(Node nodeId);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_SetControllerVibration(uint controllerMask, float frequency, float amplitude);
	}

	private static class OVRP_0_1_3
	{
		public static readonly System.Version version = new System.Version(0, 1, 3);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Posef ovrp_GetNodeVelocity(Node nodeId);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Posef ovrp_GetNodeAcceleration(Node nodeId);
	}

	private static class OVRP_0_5_0
	{
		public static readonly System.Version version = new System.Version(0, 5, 0);
	}

	private static class OVRP_1_0_0
	{
		public static readonly System.Version version = new System.Version(1, 0, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern TrackingOrigin ovrp_GetTrackingOriginType();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_SetTrackingOriginType(TrackingOrigin originType);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Posef ovrp_GetTrackingCalibratedOrigin();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_RecenterTrackingOrigin(uint flags);
	}

	private static class OVRP_1_1_0
	{
		public static readonly System.Version version = new System.Version(1, 1, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetInitialized();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ovrp_GetVersion")]
		private static extern IntPtr _ovrp_GetVersion();
		public static string ovrp_GetVersion() { return Marshal.PtrToStringAnsi(_ovrp_GetVersion()); }

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ovrp_GetNativeSDKVersion")]
		private static extern IntPtr _ovrp_GetNativeSDKVersion();
		public static string ovrp_GetNativeSDKVersion() { return Marshal.PtrToStringAnsi(_ovrp_GetNativeSDKVersion()); }

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr ovrp_GetAudioOutId();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr ovrp_GetAudioInId();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern float ovrp_GetEyeTextureScale();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_SetEyeTextureScale(float value);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetTrackingOrientationSupported();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetTrackingOrientationEnabled();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_SetTrackingOrientationEnabled(Bool value);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetTrackingPositionSupported();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetTrackingPositionEnabled();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_SetTrackingPositionEnabled(Bool value);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetNodePresent(Node nodeId);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetNodeOrientationTracked(Node nodeId);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetNodePositionTracked(Node nodeId);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Frustumf ovrp_GetNodeFrustum(Node nodeId);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern ControllerState ovrp_GetControllerState(uint controllerMask);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int ovrp_GetSystemCpuLevel();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_SetSystemCpuLevel(int value);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int ovrp_GetSystemGpuLevel();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_SetSystemGpuLevel(int value);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetSystemPowerSavingMode();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern float ovrp_GetSystemDisplayFrequency();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int ovrp_GetSystemVSyncCount();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern float ovrp_GetSystemVolume();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern BatteryStatus ovrp_GetSystemBatteryStatus();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern float ovrp_GetSystemBatteryLevel();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern float ovrp_GetSystemBatteryTemperature();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ovrp_GetSystemProductName")]
		private static extern IntPtr _ovrp_GetSystemProductName();
		public static string ovrp_GetSystemProductName() { return Marshal.PtrToStringAnsi(_ovrp_GetSystemProductName()); }

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_ShowSystemUI(PlatformUI ui);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetAppMonoscopic();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_SetAppMonoscopic(Bool value);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetAppHasVrFocus();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetAppShouldQuit();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetAppShouldRecenter();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ovrp_GetAppLatencyTimings")]
		private static extern IntPtr _ovrp_GetAppLatencyTimings();
		public static string ovrp_GetAppLatencyTimings() { return Marshal.PtrToStringAnsi(_ovrp_GetAppLatencyTimings()); }

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetUserPresent();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern float ovrp_GetUserIPD();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_SetUserIPD(float value);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern float ovrp_GetUserEyeDepth();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_SetUserEyeDepth(float value);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern float ovrp_GetUserEyeHeight();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_SetUserEyeHeight(float value);
	}

	private static class OVRP_1_2_0
	{
		public static readonly System.Version version = new System.Version(1, 2, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_SetSystemVSyncCount(int vsyncCount);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrpi_SetTrackingCalibratedOrigin();
	}

	private static class OVRP_1_3_0
	{
		public static readonly System.Version version = new System.Version(1, 3, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetEyeOcclusionMeshEnabled();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_SetEyeOcclusionMeshEnabled(Bool value);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetSystemHeadphonesPresent();
	}

	private static class OVRP_1_5_0
	{
		public static readonly System.Version version = new System.Version(1, 5, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern SystemRegion ovrp_GetSystemRegion();
	}

	private static class OVRP_1_6_0
	{
		public static readonly System.Version version = new System.Version(1, 6, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetTrackingIPDEnabled();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_SetTrackingIPDEnabled(Bool value);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern HapticsDesc ovrp_GetControllerHapticsDesc(uint controllerMask);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern HapticsState ovrp_GetControllerHapticsState(uint controllerMask);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_SetControllerHaptics(uint controllerMask, HapticsBuffer hapticsBuffer);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_SetOverlayQuad3(uint flags, IntPtr textureLeft, IntPtr textureRight, IntPtr device, Posef pose, Vector3f scale, int layerIndex);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern float ovrp_GetEyeRecommendedResolutionScale();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern float ovrp_GetAppCpuStartToGpuEndTime();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int ovrp_GetSystemRecommendedMSAALevel();
	}

	private static class OVRP_1_7_0
	{
		public static readonly System.Version version = new System.Version(1, 7, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetAppChromaticCorrection();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_SetAppChromaticCorrection(Bool value);
	}

	private static class OVRP_1_8_0
	{
		public static readonly System.Version version = new System.Version(1, 8, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetBoundaryConfigured();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern BoundaryTestResult ovrp_TestBoundaryNode(Node nodeId, BoundaryType boundaryType);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern BoundaryTestResult ovrp_TestBoundaryPoint(Vector3f point, BoundaryType boundaryType);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern BoundaryGeometry ovrp_GetBoundaryGeometry(BoundaryType boundaryType);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Vector3f ovrp_GetBoundaryDimensions(BoundaryType boundaryType);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetBoundaryVisible();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_SetBoundaryVisible(Bool value);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_Update2(int stateId, int frameIndex, double predictionSeconds);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Posef ovrp_GetNodePose2(int stateId, Node nodeId);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Posef ovrp_GetNodeVelocity2(int stateId, Node nodeId);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Posef ovrp_GetNodeAcceleration2(int stateId, Node nodeId);
	}

	private static class OVRP_1_9_0
	{
		public static readonly System.Version version = new System.Version(1, 9, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern SystemHeadset ovrp_GetSystemHeadsetType();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Controller ovrp_GetActiveController();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Controller ovrp_GetConnectedControllers();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetBoundaryGeometry2(BoundaryType boundaryType, IntPtr points, ref int pointsCount);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern AppPerfStats ovrp_GetAppPerfStats();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_ResetAppPerfStats();
	}

	private static class OVRP_1_10_0
	{
		public static readonly System.Version version = new System.Version(1, 10, 0);
	}

	private static class OVRP_1_11_0
	{
		public static readonly System.Version version = new System.Version(1, 11, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_SetDesiredEyeTextureFormat(EyeTextureFormat value);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern EyeTextureFormat ovrp_GetDesiredEyeTextureFormat();
	}

	private static class OVRP_1_12_0
	{
		public static readonly System.Version version = new System.Version(1, 12, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern float ovrp_GetAppFramerate();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern PoseStatef ovrp_GetNodePoseState(Step stepId, Node nodeId);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern ControllerState2 ovrp_GetControllerState2(uint controllerMask);
	}

	private static class OVRP_1_15_0
	{
		public static readonly System.Version version = new System.Version(1, 15, 0);

		public const int OVRP_EXTERNAL_CAMERA_NAME_SIZE = 32;

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_InitializeMixedReality();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_ShutdownMixedReality();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetMixedRealityInitialized();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_UpdateExternalCamera();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetExternalCameraCount(out int cameraCount);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetExternalCameraName(int cameraId, [MarshalAs(UnmanagedType.LPArray, SizeConst = OVRP_EXTERNAL_CAMERA_NAME_SIZE)] char[] cameraName);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetExternalCameraIntrinsics(int cameraId, out CameraIntrinsics cameraIntrinsics);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetExternalCameraExtrinsics(int cameraId, out CameraExtrinsics cameraExtrinsics);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_CalculateLayerDesc(OverlayShape shape, LayerLayout layout, ref Sizei textureSize,
			int mipLevels, int sampleCount, EyeTextureFormat format, int layerFlags, ref LayerDesc layerDesc);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_EnqueueSetupLayer(ref LayerDesc desc, IntPtr layerId);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_EnqueueDestroyLayer(IntPtr layerId);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetLayerTextureStageCount(int layerId, ref int layerTextureStageCount);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetLayerTexturePtr(int layerId, int stage, Eye eyeId, ref IntPtr textureHandle);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_EnqueueSubmitLayer(uint flags, IntPtr textureLeft, IntPtr textureRight, int layerId, int frameIndex, ref Posef pose, ref Vector3f scale, int layerIndex);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetNodeFrustum2(Node nodeId, out Frustumf2 nodeFrustum);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_GetEyeTextureArrayEnabled();
	}

	private static class OVRP_1_16_0
	{
		public static readonly System.Version version = new System.Version(1, 16, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_UpdateCameraDevices();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_IsCameraDeviceAvailable(CameraDevice cameraDevice);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_SetCameraDevicePreferredColorFrameSize(CameraDevice cameraDevice, Sizei preferredColorFrameSize);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_OpenCameraDevice(CameraDevice cameraDevice);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_CloseCameraDevice(CameraDevice cameraDevice);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_HasCameraDeviceOpened(CameraDevice cameraDevice);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Bool ovrp_IsCameraDeviceColorFrameAvailable(CameraDevice cameraDevice);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetCameraDeviceColorFrameSize(CameraDevice cameraDevice, out Sizei colorFrameSize);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetCameraDeviceColorFrameBgraPixels(CameraDevice cameraDevice, out IntPtr colorFrameBgraPixels, out int colorFrameRowPitch);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetControllerState4(uint controllerMask, ref ControllerState4 controllerState);
	}

	private static class OVRP_1_17_0
	{
		public static readonly System.Version version = new System.Version(1, 17, 0);

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetExternalCameraPose(CameraDevice camera, out Posef cameraPose);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_ConvertPoseToCameraSpace(CameraDevice camera, ref Posef trackingSpacePose, out Posef cameraSpacePose);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetCameraDeviceIntrinsicsParameters(CameraDevice camera, out Bool supportIntrinsics, out CameraDeviceIntrinsicsParameters intrinsicsParameters);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_DoesCameraDeviceSupportDepth(CameraDevice camera, out Bool supportDepth);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetCameraDeviceDepthSensingMode(CameraDevice camera, out CameraDeviceDepthSensingMode depthSensoringMode);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_SetCameraDeviceDepthSensingMode(CameraDevice camera, CameraDeviceDepthSensingMode depthSensoringMode);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetCameraDevicePreferredDepthQuality(CameraDevice camera, out CameraDeviceDepthQuality depthQuality);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_SetCameraDevicePreferredDepthQuality(CameraDevice camera, CameraDeviceDepthQuality depthQuality);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_IsCameraDeviceDepthFrameAvailable(CameraDevice camera, out Bool available);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetCameraDeviceDepthFrameSize(CameraDevice camera, out Sizei depthFrameSize);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetCameraDeviceDepthFramePixels(CameraDevice cameraDevice, out IntPtr depthFramePixels, out int depthFrameRowPitch);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetCameraDeviceDepthConfidencePixels(CameraDevice cameraDevice, out IntPtr depthConfidencePixels, out int depthConfidenceRowPitch);
#endif
	}

	private static class OVRP_1_18_0
	{
		public static readonly System.Version version = new System.Version(1, 18, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_SetHandNodePoseStateLatency(double latencyInSeconds);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetHandNodePoseStateLatency(out double latencyInSeconds);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetAppHasInputFocus(out Bool appHasInputFocus);
	}

	private static class OVRP_1_19_0
	{
		public static readonly System.Version version = new System.Version(1, 19, 0);
	}

	private static class OVRP_1_21_0
	{
		public static readonly System.Version version = new System.Version(1, 21, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetTiledMultiResSupported(out Bool foveationSupported);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetTiledMultiResLevel(out FixedFoveatedRenderingLevel level);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_SetTiledMultiResLevel(FixedFoveatedRenderingLevel level);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetGPUUtilSupported(out Bool gpuUtilSupported);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetGPUUtilLevel(out float gpuUtil);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetSystemDisplayFrequency2(out float systemDisplayFrequency);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetSystemDisplayAvailableFrequencies(IntPtr systemDisplayAvailableFrequencies, ref int numFrequencies);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_SetSystemDisplayFrequency(float requestedFrequency);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetAppAsymmetricFov(out Bool useAsymmetricFov);
	}

	private static class OVRP_1_28_0
	{
		public static readonly System.Version version = new System.Version(1, 28, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetDominantHand(out Handedness dominantHand);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetReorientHMDOnControllerRecenter(out Bool recenter);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_SetReorientHMDOnControllerRecenter(Bool recenter);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_SendEvent(string name, string param);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_EnqueueSetupLayer2(ref LayerDesc desc, int compositionDepth, IntPtr layerId);
	}

	private static class OVRP_1_29_0
	{
		public static readonly System.Version version = new System.Version(1, 29, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetLayerAndroidSurfaceObject(int layerId, ref IntPtr surfaceObject);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_SetHeadPoseModifier(ref Quatf relativeRotation, ref Vector3f relativeTranslation);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetHeadPoseModifier(out Quatf relativeRotation, out Vector3f relativeTranslation);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetNodePoseStateRaw(Step stepId, int frameIndex, Node nodeId, out PoseStatef nodePoseState);
	}

	private static class OVRP_1_30_0
	{
		public static readonly System.Version version = new System.Version(1, 30, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetCurrentTrackingTransformPose(out Posef trackingTransformPose);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetTrackingTransformRawPose(out Posef trackingTransformRawPose);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_SendEvent2(string name, string param, string source);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_IsPerfMetricsSupported(PerfMetrics perfMetrics, out Bool isSupported);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetPerfMetricsFloat(PerfMetrics perfMetrics, out float value);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetPerfMetricsInt(PerfMetrics perfMetrics, out int value);
	}

	private static class OVRP_1_31_0
	{
		public static readonly System.Version version = new System.Version(1, 31, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetTimeInSeconds(out double value);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_SetColorScaleAndOffset(Vector4 colorScale, Vector4 colorOffset, Bool applyToAllLayers);
	}

	private static class OVRP_1_32_0
	{
		public static readonly System.Version version = new System.Version(1, 32, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_AddCustomMetadata(string name, string param);
	}

	private static class OVRP_1_34_0
	{
		public static readonly System.Version version = new System.Version(1, 34, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_EnqueueSubmitLayer2(uint flags, IntPtr textureLeft, IntPtr textureRight, int layerId, int frameIndex, ref Posef pose, ref Vector3f scale, int layerIndex,
		Bool overrideTextureRectMatrix, ref TextureRectMatrixf textureRectMatrix, Bool overridePerLayerColorScaleAndOffset, ref Vector4 colorScale, ref Vector4 colorOffset);

	}

	private static class OVRP_1_35_0
	{
		public static readonly System.Version version = new System.Version(1, 35, 0);
	}

	private static class OVRP_1_36_0
	{
		public static readonly System.Version version = new System.Version(1, 36, 0);
	}

	private static class OVRP_1_37_0
	{
		public static readonly System.Version version = new System.Version(1, 37, 0);
	}

	private static class OVRP_1_38_0
	{
		public static readonly System.Version version = new System.Version(1, 38, 0);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetTrackingTransformRelativePose(ref Posef trackingTransformRelativePose, TrackingOrigin trackingOrigin);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_Media_Initialize();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_Media_Shutdown();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_Media_GetInitialized(out Bool initialized);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_Media_Update();

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_Media_GetMrcActivationMode(out Media.MrcActivationMode activationMode);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_Media_SetMrcActivationMode(Media.MrcActivationMode activationMode);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_Media_IsMrcEnabled(out Bool mrcEnabled);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_Media_IsMrcActivated(out Bool mrcActivated);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_Media_UseMrcDebugCamera(out Bool useMrcDebugCamera);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_Media_SetMrcInputVideoBufferType(Media.InputVideoBufferType inputVideoBufferType);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_Media_GetMrcInputVideoBufferType(ref Media.InputVideoBufferType inputVideoBufferType);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_Media_SetMrcFrameSize(int frameWidth, int frameHeight);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_Media_GetMrcFrameSize(ref int frameWidth, ref int frameHeight);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_Media_SetMrcAudioSampleRate(int sampleRate);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_Media_GetMrcAudioSampleRate(ref int sampleRate);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_Media_SetMrcFrameImageFlipped(Bool flipped);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_Media_GetMrcFrameImageFlipped(ref Bool flipped);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_Media_EncodeMrcFrame(System.IntPtr rawBuffer, System.IntPtr audioDataPtr, int audioDataLen, int audioChannels, double timestamp, ref int outSyncId);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_Media_EncodeMrcFrameWithDualTextures(System.IntPtr backgroundTextureHandle, System.IntPtr foregroundTextureHandle, System.IntPtr audioData, int audioDataLen, int audioChannels, double timestamp, ref int outSyncId);


		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_Media_SyncMrcFrame(int syncId);


		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetExternalCameraCalibrationRawPose(int cameraId, out Posef rawPose);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_SetDeveloperMode(Bool active);


		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetNodeOrientationValid(Node nodeId, ref Bool nodeOrientationValid);

		[DllImport(pluginName, CallingConvention = CallingConvention.Cdecl)]
		public static extern Result ovrp_GetNodePositionValid(Node nodeId, ref Bool nodePositionValid);
	}

	private static class OVRP_1_39_0
	{
		public static readonly System.Version version = new System.Version(1, 39, 0);
	}

	private static class OVRP_1_40_0
	{
		public static readonly System.Version version = new System.Version(1, 40, 0);
	}

	private static class OVRP_1_41_0
	{
		public static readonly System.Version version = new System.Version(1, 41, 0);
	}

#endif // !OVRPLUGIN_UNSUPPORTED_PLATFORM

}
