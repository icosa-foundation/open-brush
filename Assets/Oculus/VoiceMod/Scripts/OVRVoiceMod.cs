/************************************************************************************
Filename    :   OVRVoiceMod.cs
Content     :   Interface to Oculus voice mod
Created     :   December 14th, 2015
Copyright   :   Copyright 2015 Oculus VR, Inc. All Rights reserved.

Licensed under the Oculus VR Rift SDK License Version 3.1 (the "License"); 
you may not use the Oculus VR Rift SDK except in compliance with the License, 
which is provided at the time of installation or download, or which 
otherwise accompanies this software in either electronic or hard copy form.

You may obtain a copy of the License at

http://www.oculusvr.com/licenses/LICENSE-3.1 

Unless required by applicable law or agreed to in writing, the Oculus VR SDK 
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
************************************************************************************/
using UnityEngine;
using System;
using System.Runtime.InteropServices;

//-------------------------------------------------------------------------------------
// ***** OVRVoiceMod
//
/// <summary>
/// OVRVoiceMod interfaces into the Oculus voice-mod engine. This component should be added
/// into the scene once. 
///
/// </summary>
public class OVRVoiceMod : MonoBehaviour 
{
	public const int ovrVoiceModSuccess = 0;

	// Error codes that may return from VoiceMod engine
	public enum ovrVoiceModError 
	{
		Unknown = 				-2250,	//< An unknown error has occurred
		CannotCreateContext = 	-2251, 	//< Unable to create a context
		InvalidParam = 			-2252,	//< An invalid parameter, e.g. NULL pointer or out of range
		BadSampleRate = 		-2253,	//< An unsupported sample rate was declared
		MissingDLL = 			-2254,	//< The DLL or shared library could not be found
		BadVersion = 			-2255,	//< Mismatched versions between header and libs
		UndefinedFunction = 	-2256	//< An undefined function 
	};

	/// Flags (unused at this time)
	public enum ovrViceModFlag
	{
		None = 0x0000,
	};

	/// NOTE: Opaque typedef for voice mod context is an unsigned int (uint)
	
	// * * * * * * * * * * * * *
    // Import functions
	public const string strOVRLS = "OVRVoiceMod";
	[DllImport(strOVRLS)]
	private static extern int ovrVoiceModDll_Initialize(int SampleRate, int BufferSize);
	[DllImport(strOVRLS)]
	private static extern void ovrVoiceModDll_Shutdown();
	[DllImport(strOVRLS)]
	private static extern IntPtr ovrVoicemodDll_GetVersion(ref int Major, 
	                                                       ref int Minor,
	                                                       ref int Patch);
	[DllImport(strOVRLS)]
	private static extern int ovrVoiceModDll_CreateContext(ref uint Context);
	[DllImport(strOVRLS)]
	private static extern int ovrVoiceModDll_DestroyContext(uint Context);	
	[DllImport(strOVRLS)]
	private static extern int ovrVoiceModDll_SendParameter(uint Context, int Parameter, int Value);
	[DllImport(strOVRLS)]
	private static extern int ovrVoiceModDll_ProcessFrame(uint Context, uint Flags, float [] AudioBuffer);
	[DllImport(strOVRLS)]
	private static extern int ovrVoiceModDll_ProcessFrameInterleaved(uint Context, uint Flags, float [] AudioBuffer);
	[DllImport(strOVRLS)]
	private static extern int ovrVoiceModDll_GetAverageAbsVolume(uint Context, ref float Volume);

	// * * * * * * * * * * * * *
	// Public members
	
	// * * * * * * * * * * * * *
    // Static members
	private static int sOVRVoiceModInit = (int)ovrVoiceModError.Unknown;

	// interface through this static member.
	public static OVRVoiceMod sInstance = null;
	
	// * * * * * * * * * * * * *
	// MonoBehaviour overrides

	/// <summary>
	/// Awake this instance.
	/// </summary>
	void Awake () 
	{	
		// We can only have one instance of OVRLipSync in a scene (use this for local property query)
		if(sInstance == null)
		{
			sInstance = this;
		}
		else
		{
			Debug.LogWarning (System.String.Format ("OVRVoiceMod Awake: Only one instance of OVRVoiceMod can exist in the scene."));
			return;
		}

		int samplerate;
		int bufsize;
		int numbuf;

		// Get the current sample rate
		samplerate = AudioSettings.outputSampleRate;
		// Get the current buffer size and number of buffers
		AudioSettings.GetDSPBufferSize (out bufsize, out numbuf);

		String str = System.String.Format 
		("OvrVoiceMod Awake: Queried SampleRate: {0:F0} BufferSize: {1:F0}", samplerate, bufsize);
		Debug.LogWarning (str);

		sOVRVoiceModInit = ovrVoiceModDll_Initialize(samplerate, bufsize);

		if(sOVRVoiceModInit != ovrVoiceModSuccess)
		{
			Debug.LogWarning (System.String.Format
			("OvrVoiceMod Awake: Failed to init VoiceMod library"));
		}

		// Important: Use the touchpad mechanism for input, call Create on the OVRTouchpad helper class
		OVRTouchpad.Create();
	}
   
	/// <summary>
	/// Start this instance.
	/// Note: make sure to always have a Start function for classes that have editor scripts.
	/// </summary>
	void Start()
	{
	}
	
	/// <summary>
	/// Run processes that need to be updated in our game thread
	/// </summary>
	void Update()
	{
	}

	/// <summary>
	/// Raises the destroy event.
	/// </summary>
	void OnDestroy()
	{
		if(sInstance != this)
		{
			Debug.LogWarning ("OVRVoiceMod OnDestroy: This is not the correct OVRVoiceMod instance.");
		}

		ovrVoiceModDll_Shutdown();
		sOVRVoiceModInit = (int)ovrVoiceModError.Unknown;
	}
	
	// * * * * * * * * * * * * *
	// Public Functions
	
	/// <summary>
	/// Determines if is initialized.
	/// </summary>
	/// <returns><c>true</c> if is initialized; otherwise, <c>false</c>.</returns>
	public static int IsInitialized()
	{
		return sOVRVoiceModInit;
	}

	/// <summary>
	/// Creates the context.
	/// </summary>
	/// <returns>The context.</returns>
	/// <param name="context">Context.</param>
	public static int CreateContext(ref uint context)
	{
		if(IsInitialized() != ovrVoiceModSuccess)
			return (int)ovrVoiceModError.CannotCreateContext;

		return ovrVoiceModDll_CreateContext(ref context);
	}

	/// <summary>
	/// Destroies the context.
	/// </summary>
	/// <returns>The context.</returns>
	/// <param name="context">Context.</param>
	public static int DestroyContext (uint context)
	{
		if(IsInitialized() != ovrVoiceModSuccess)
			return (int)ovrVoiceModError.Unknown;

		return ovrVoiceModDll_DestroyContext(context);
	}

	/// <summary>
	/// Sends the parameter.
	/// </summary>
	/// <returns>The parameter.</returns>
	/// <param name="context">Context.</param>
	/// <param name="parameter">Parameter.</param>
	/// <param name="value">Value.</param>
	public static int SendParameter(uint context, int parameter, int value)
	{
		if(IsInitialized() != ovrVoiceModSuccess)
			return (int)ovrVoiceModError.Unknown;

		return ovrVoiceModDll_SendParameter(context, parameter, value);
	}

	/// <summary>
	/// Processes the frame.
	/// </summary>
	/// <returns>The frame.</returns>
	/// <param name="context">Context.</param>
	/// <param name="audioBuffer">Audio buffer.</param>
	public static int ProcessFrame(uint context, float [] audioBuffer)
	{
		if(IsInitialized() != ovrVoiceModSuccess)
			return (int)ovrVoiceModError.Unknown;

		return ovrVoiceModDll_ProcessFrame(context, (uint)ovrViceModFlag.None , audioBuffer);
	}

	/// <summary>
	/// Processes the frame interleaved.
	/// </summary>
	/// <returns>The frame interleaved.</returns>
	/// <param name="context">Context.</param>
	/// <param name="audioBuffer">Audio buffer.</param>
	public static int ProcessFrameInterleaved(uint context, float [] audioBuffer)
	{
		if(IsInitialized() != ovrVoiceModSuccess)
			return (int)ovrVoiceModError.Unknown;

		return ovrVoiceModDll_ProcessFrameInterleaved(context, (uint)ovrViceModFlag.None, audioBuffer);
	}

	/// <summary>
	/// Gets the average abs volume.
	/// </summary>
    /// <returns>The average abs volume.</returns>
	/// <param name="context">Context.</param>
    /// <param name="volume">Volume.</param>
	public static float GetAverageAbsVolume(uint context)
	{
		if(IsInitialized() != ovrVoiceModSuccess)
			return 0.0f;

        float volume = 0;

		int result = ovrVoiceModDll_GetAverageAbsVolume(context, ref volume);

        return volume;
	}
}
