/************************************************************************************
Filename    :   OVRVoiceModContext.cs
Content     :   Interface to Oculus Lip-Sync engine
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


[RequireComponent(typeof(AudioSource))]

//-------------------------------------------------------------------------------------
// ***** OVRVoiceModContext
//
/// <summary>
/// OVRVoiceModContext interfaces into VoiceMod engine. 
/// Includes preset bank.
/// This component should be added into the scene once for each Audio Source. 
///
/// </summary>
public class OVRVoiceModContext : MonoBehaviour 
{
	// Enum for various voice mod parameters (lines up with native context, OVRVoiceModContext.h)
	public enum ovrVoiceModParams
	{
		MixInputAudio,				// 0.0->1.0
		PitchInputAudio,			// 0.5->2.0
		SetBands,           		// 1 -> 128
		FormantCorrection,			// 0 = off, 1 = on
		Carrier1_TrackPitch,		// 0 = off, 1 = om
		Carrier1_Type,				// Carrier type: 0 = Noise, 1 = CycledNoise, 2 = SawUp, 3 = Pulse
		Carrier1_Gain,				// Linear multiplier (0.0 -> 1.0)
		Carrier1_Frequency,			// Frequency of carrier (used by SawUp and Pulse) 0.0 -> Sample Rate
		Carrier1_Note,				// Translates to frequency (0-127)
		Carrier1_PulseWidth,		// Used by Pulse carrier (0.0 -> 1.0)
		Carrier1_CycledNoiseSize,	// Number of samples in cycled noise carrier: 1 - 1024
		Carrier2_TrackPitch,		// Same as Carrier 1
		Carrier2_Type,				// 
		Carrier2_Gain,				//
		Carrier2_Frequency,			// 
		Carrier2_Note,				// 
		Carrier2_PulseWidth,		//
		Carrier2_CycledNoiseSize,	//
		Count
	};

	// * * * * * * * * * * * * *
	// Public members
	public AudioSource audioSource     = null;
	public float 	   gain 		   = 1.0f;
	public bool        audioMute       = true; 
	public KeyCode     loopback        = KeyCode.L;

	// Voice Mod Preset
	public struct VMPreset
	{
		public string info;
		public Color  color;
		public float  mix;
		public float  pitch;
		public int    bands;
		public int    formant;
		public int    c1PTrack;
		public int    c1Type;
		public float  c1Gain;
		public float  c1Freq;
		public int    c1Note;
		public float  c1PW;
		public int    c1CNS;
		public int    c2PTrack;
		public int    c2Type;
		public float  c2Gain;
		public float  c2Freq;
		public int    c2Note;
		public float  c2PW;
		public int    c2CNS;
	};
		
	private VMPreset [] VMPresets = new VMPreset[]
	{
		// INIT
		new VMPreset{info="-INIT-\nNo pitch shift, no vocode", 
			color = Color.gray,
			mix = 1.0f, pitch = 1.0f, bands = 32, formant = 0,
			c1PTrack = 0, c1Type = 0, c1Gain = 0.0f, c1Freq = 440.0f, c1Note = -1, c1PW = 0.5f, c1CNS = 512,
			c2PTrack = 0, c2Type = 0, c2Gain = 0.0f, c2Freq = 440.0f, c2Note = -1, c2PW = 0.5f, c2CNS = 512,},

		new VMPreset{info="FULL VOCODE\nCarrier 1: Full noise", 
			color = Color.white,
			mix = 0.0f, pitch = 1.0f, bands = 32, formant = 0,
			c1PTrack = 0, c1Type = 0, c1Gain = 1.0f, c1Freq = 440.0f, c1Note = -1, c1PW = 0.5f, c1CNS = 512,
			c2PTrack = 0, c2Type = 0, c2Gain = 0.0f, c2Freq = 440.0f, c2Note = -1, c2PW = 0.5f, c2CNS = 512,},

		new VMPreset{info="FULL VOCODE\nCarrier 1: Cycled noise 512", 
			color = Color.blue,
			mix = 0.0f, pitch = 1.0f, bands = 32, formant = 0,
			c1PTrack = 0, c1Type = 1, c1Gain = 1.0f, c1Freq = 440.0f, c1Note = -1, c1PW = 0.5f, c1CNS = 512,
			c2PTrack = 0, c2Type = 0, c2Gain = 0.0f, c2Freq = 440.0f, c2Note = -1, c2PW = 0.5f, c2CNS = 512,},

		new VMPreset{info="FULL VOCODE\nCarrier 1: Saw Up, Freq 220", 
			color = Color.magenta,
			mix = 0.0f, pitch = 1.0f, bands = 32, formant = 0,
			c1PTrack = 0, c1Type = 2, c1Gain = 1.0f, c1Freq = 220.0f, c1Note = -1, c1PW = 0.5f, c1CNS = 512,
			c2PTrack = 0, c2Type = 0, c2Gain = 0.0f, c2Freq = 440.0f, c2Note = -1, c2PW = 0.5f, c2CNS = 512,},

		new VMPreset{info="FULL VOCODE\nCarrier 1: Saw Up, Pitch tracked\n", 
			color = Color.cyan,
			mix = 0.0f, pitch = 1.0f, bands = 32, formant = 0,
			c1PTrack = 1, c1Type = 2, c1Gain = 0.34f, c1Freq = 440.0f, c1Note = -1, c1PW = 0.1f, c1CNS = 512,
			c2PTrack = 0, c2Type = 0, c2Gain = 0.0f,  c2Freq = 440.0f, c2Note = -1, c2PW = 0.5f, c2CNS = 512,},
		 
		new VMPreset{info="INPUT PLUS VOCODE\nInput 50%, Vocode 50%\nPitch 1.0\nCarrier 1: Full Noise,\nCarrier 2: Cycled Noise 512", 
			color = Color.green,
			mix = 0.5f, pitch = 1.0f, bands = 32, formant = 0,
			c1PTrack = 0, c1Type = 0, c1Gain = 0.5f, c1Freq = 440.0f, c1Note = 57, c1PW = 0.5f,  c1CNS = 512,
			c2PTrack = 0, c2Type = 1, c2Gain = 0.5f, c2Freq = 440.0f, c2Note = 45, c2PW = 0.25f, c2CNS = 512,},

		new VMPreset{info="INPUT PLUS VOCODE PLUS PITCH DOWN\nInput 50%, Vocode 50%\nPitch 0.75\nCarrier 1: Cycled Noise 512\nCarrier 2: Cycled Noise 768", 
			color = Color.red,
			mix = 0.5f, pitch = 0.75f, bands = 32, formant = 0,
			c1PTrack = 0, c1Type = 1, c1Gain = 0.6f, c1Freq = 440.0f, c1Note = 57, c1PW = 0.5f,  c1CNS = 512,
			c2PTrack = 0, c2Type = 3, c2Gain = 0.2f, c2Freq = 440.0f, c2Note = 40, c2PW = 0.25f, c2CNS = 768,},

		new VMPreset{info="PITCH ONLY\nPitch 1.25 (Formant correction)", 
			color = Color.blue,
			mix = 1.0f, pitch = 1.25f, bands = 32, formant = 1,
			c1PTrack = 0, c1Type = 1, c1Gain = 1.0f, c1Freq = 440.0f, c1Note = 57, c1PW = 0.5f, c1CNS = 400,
			c2PTrack = 0, c2Type = 3, c2Gain = 0.0f, c2Freq = 440.0f, c2Note = 52, c2PW = 0.5f, c2CNS = 512,},

		new VMPreset{info="PITCH ONLY\nPitch 0.5 (Formant correction)", 
			color = Color.green,
			mix = 1.0f, pitch = 0.5f, bands = 32, formant = 1,
			c1PTrack = 0, c1Type = 1, c1Gain = 1.0f, c1Freq = 440.0f, c1Note = 57, c1PW = 0.5f, c1CNS = 400,
			c2PTrack = 0, c2Type = 3, c2Gain = 0.0f, c2Freq = 440.0f, c2Note = 52, c2PW = 0.5f, c2CNS = 512,},

		new VMPreset{info="PITCH ONLY\nPitch 2.0 (Formant correction)", 
			color = Color.yellow,
			mix = 1.0f, pitch = 2.0f, bands = 32, formant = 1,
			c1PTrack = 0, c1Type = 1, c1Gain = 1.0f, c1Freq = 440.0f, c1Note = 57, c1PW = 0.5f, c1CNS = 400,
			c2PTrack = 0, c2Type = 3, c2Gain = 0.0f, c2Freq = 440.0f, c2Note = 52, c2PW = 0.5f, c2CNS = 512,},
	};
	
	// Current VoiceMod values (visible in inspector without writing a editor helper class)
	public float 	   VM_MixAudio           = 1.0f;
	public float       VM_Pitch              = 1.0f;
	public int         VM_Bands		         = 32;
	public int         VM_FormantCorrect     = 0;

	// Carrier 1
	public int		   VM_C1_TrackPitch      = 0;
	public int         VM_C1_Type 		     = 0;
	public float       VM_C1_Gain 		     = 0.5f;
	public float       VM_C1_Freq            = 440.0f;
	public int         VM_C1_Note 			 = 67;
	public float       VM_C1_PulseWidth 	 = 0.5f;
	public int         VM_C1_CycledNoiseSize = 512;

	// Carrier 2
	public int		   VM_C2_TrackPitch      = 0;
	public int         VM_C2_Type 		     = 0;
	public float       VM_C2_Gain 		     = 0.5f;
	public float       VM_C2_Freq            = 440.0f;
	public int         VM_C2_Note 			 = 67;
	public float       VM_C2_PulseWidth 	 = 0.5f;
	public int         VM_C2_CycledNoiseSize = 512;

	// * * * * * * * * * * * * *
	// Private members
	private uint  context = 0;		// 0 is no context
	private float prevVol = 0.0f;	// used for smoothing avg volume		

	// * * * * * * * * * * * * *
    // Static members
	
	// * * * * * * * * * * * * *
	// MonoBehaviour overrides

	/// <summary>
	/// Awake this instance.
	/// </summary>
	void Awake () 
	{	
		// Cache the audio source we are going to be using to pump data to the SR
		if (!audioSource) audioSource = GetComponent<AudioSource>();
		if (!audioSource) return; 
	}
   
	/// <summary>
	/// Start this instance.
	/// Note: make sure to always have a Start function for classes that have editor scripts.
	/// </summary>
	void Start()
	{
		// Create the context that we will feed into the audio buffer
		lock(this)
		{
			if(context == 0)
			{
				if(OVRVoiceMod.CreateContext(ref context) != OVRVoiceMod.ovrVoiceModSuccess)
				{
					Debug.Log ("OVRVoiceModContext.Start ERROR: Could not create VoiceMod context.");
					return;
				}
			}
		}

		// Add a listener to the OVRMessenger for touch events
		OVRMessenger.AddListener<OVRTouchpad.TouchEvent>("Touchpad", LocalTouchEventCallback);

		// VoiceMod: Set the current state of the voice mod as set in the inspector
		SendVoiceModUpdate();
	}
	
	/// <summary>
	/// Run processes that need to be updated in our game thread
	/// </summary>
	void Update()
	{
		// Turn loopback on/off
		if (Input.GetKeyDown(loopback))
		{
			audioMute = !audioMute;

			OVRDebugConsole.Clear();
			OVRDebugConsole.ClearTimeout(1.5f);
			
			if(!audioMute)
				OVRDebugConsole.Log("LOOPBACK MODE: ENABLED");
			else
				OVRDebugConsole.Log("LOOPBACK MODE: DISABLED");
			
		}
		else if(Input.GetKeyDown(KeyCode.LeftArrow))
		{
			gain -= 0.1f;
			if(gain < 0.5f) gain = 0.5f;

			string g = "LINEAR GAIN: ";
			g += gain;
			OVRDebugConsole.Clear();
			OVRDebugConsole.Log(g);
			OVRDebugConsole.ClearTimeout(1.5f);
		
		}
		else if(Input.GetKeyDown(KeyCode.RightArrow))
		{
			gain += 0.1f;
			if(gain > 3.0f)
				gain = 3.0f;

			string g = "LINEAR GAIN: ";
			g += gain;
			OVRDebugConsole.Clear();
			OVRDebugConsole.Log(g);
			OVRDebugConsole.ClearTimeout(1.5f);
		
		}

		UpdateVoiceModUpdate();
	}

	/// <summary>
	/// Raises the destroy event.
	/// </summary>
	void OnDestroy()
	{
		// Create the context that we will feed into the audio buffer
		lock(this)
		{
			if(context != 0)
			{
				if(OVRVoiceMod.DestroyContext(context) != OVRVoiceMod.ovrVoiceModSuccess)
				{
					Debug.Log ("OVRVoiceModContext.OnDestroy ERROR: Could not delete VoiceMod context.");
				}
			}
		}
	}
	
	/// <summary>
	/// Raises the audio filter read event.
	/// </summary>
	/// <param name="data">Data.</param>
	/// <param name="channels">Channels.</param>
	void OnAudioFilterRead(float[] data, int channels)
	{
		// Do not spatialize if we are not initialized, or if there is no
		// audio source attached to game object
		if ((OVRVoiceMod.IsInitialized() != OVRVoiceMod.ovrVoiceModSuccess) || audioSource == null)
			return;

		// increase the gain of the input to get a better signal input
		for (int i = 0; i < data.Length; ++i)
			data[i] = data[i] * gain;	

		// Send data into VoiceMod context for processing (if context is not 0)
		lock(this)
		{
			if(context != 0)
			{	
				OVRVoiceMod.ProcessFrameInterleaved(context, data);
			}
		}

		// Turn off output (so that we don't get feedback from a mic too close to speakers)
		if(audioMute == true)
		{
			for (int i = 0; i < data.Length; ++i)
				data[i] = data[i] * 0.0f;	
		}
	}

	// * * * * * * * * * * * * *
	// Public Functions

	/// <summary>
	/// Sends the parameter.
	/// </summary>
	/// <returns>The parameter.</returns>
	/// <param name="parameter">Parameter.</param>
	/// <param name="value">Value.</param>
	public int SendParameter(ovrVoiceModParams parameter, int value)
	{
		if(OVRVoiceMod.IsInitialized() != OVRVoiceMod.ovrVoiceModSuccess)
			return (int)OVRVoiceMod.ovrVoiceModError.Unknown;

		return OVRVoiceMod.SendParameter(context, (int)parameter, value);
	}

	/// <summary>
	/// Sets the preset.
	/// </summary>
	/// <returns><c>true</c>, if preset was set, <c>false</c> otherwise.</returns>
	/// <param name="preset">Preset.</param>
	public bool SetPreset(int preset)
	{
		if(preset < 0 || preset >= VMPresets.Length)
			return false;

		VM_MixAudio   			= VMPresets[preset].mix;
		VM_Pitch      			= VMPresets[preset].pitch;
		VM_Bands      			= VMPresets[preset].bands;
		VM_FormantCorrect 		= VMPresets[preset].formant;
		VM_C1_TrackPitch		= VMPresets[preset].c1PTrack;
		VM_C1_Type    			= VMPresets[preset].c1Type;
		VM_C1_Gain 				= VMPresets[preset].c1Gain;
		VM_C1_Freq				= VMPresets[preset].c1Freq;
		VM_C1_Note				= VMPresets[preset].c1Note;
		VM_C1_PulseWidth		= VMPresets[preset].c1PW;
		VM_C1_CycledNoiseSize	= VMPresets[preset].c1CNS;
		VM_C2_TrackPitch		= VMPresets[preset].c2PTrack;
		VM_C2_Type    			= VMPresets[preset].c2Type;
		VM_C2_Gain 				= VMPresets[preset].c2Gain;
		VM_C2_Freq				= VMPresets[preset].c2Freq;
		VM_C2_Note				= VMPresets[preset].c2Note;
		VM_C2_PulseWidth		= VMPresets[preset].c2PW;
		VM_C2_CycledNoiseSize	= VMPresets[preset].c2CNS;

		SendVoiceModUpdate();

		OVRDebugConsole.Clear();
		OVRDebugConsole.Log(VMPresets[preset].info);
		OVRDebugConsole.ClearTimeout(5.0f);

		return true;
	}

	/// <summary>
	/// Gets the number presets.
	/// </summary>
	/// <returns>The number presets.</returns>
	public int GetNumPresets()
	{
		return VMPresets.Length;
	}

	/// <summary>
	/// Gets the color of the preset.
	/// </summary>
	/// <returns>The preset color.</returns>
	/// <param name="preset">Preset.</param>
	public Color GetPresetColor(int preset)
	{
		if(preset < 0 || preset >= VMPresets.Length)
			return Color.black;

		return VMPresets[preset].color;
	}

	/// <summary>
	/// Gets the average abs volume.
	/// </summary>
	/// <returns>The average abs volume.</returns>
	public float GetAverageAbsVolume()
	{
		if(context == 0)
			return 0.0f;

		float V = prevVol * 0.8f + OVRVoiceMod.GetAverageAbsVolume(context) * 0.2f;
		prevVol = V;

		return V;
	}

	// LocalTouchEventCallback
	// NOTE: We will not worry about gain on Android, since it will be
	// more important to switch presets. We will keep gain available on
	// Desktop
	void LocalTouchEventCallback(OVRTouchpad.TouchEvent touchEvent)
	{
		switch(touchEvent)
		{
			case(OVRTouchpad.TouchEvent.SingleTap):
				audioMute = !audioMute;

				OVRDebugConsole.Clear();
				OVRDebugConsole.ClearTimeout(1.5f);

				if(!audioMute)
					OVRDebugConsole.Log("LOOPBACK MODE: ENABLED");
				else
					OVRDebugConsole.Log("LOOPBACK MODE: DISABLED");

				break;
		}
	}

	// Update directly from current state in inspector
	void UpdateVoiceModUpdate()
	{
		// Send directly from inspector
		if(Input.GetKeyDown(KeyCode.Space))
		{
			SendVoiceModUpdate();
			OVRDebugConsole.Clear();
			OVRDebugConsole.Log("UPDATED VOICE MOD FROM INSPECTOR");
			OVRDebugConsole.ClearTimeout(1.0f);
		}
	}

	// Sends the current state of voice mod values 
	void SendVoiceModUpdate()
	{
		VM_MixAudio   			= Mathf.Clamp(VM_MixAudio,   			0.0f, 1.0f);
		VM_Pitch      			= Mathf.Clamp(VM_Pitch,      			0.5f, 2.0f);
		VM_Bands      			= Mathf.Clamp(VM_Bands,      			1,    128);
		VM_FormantCorrect       = Mathf.Clamp(VM_FormantCorrect,      	0,    1);
		VM_C1_TrackPitch        = Mathf.Clamp(VM_C1_TrackPitch,      	0,    1);
		VM_C1_Type    			= Mathf.Clamp(VM_C1_Type,    			0,    3);
		VM_C1_Gain 				= Mathf.Clamp(VM_C1_Gain,    			0.0f, 1.0f);
		VM_C1_Freq				= Mathf.Clamp(VM_C1_Freq,    			0.0f, 96000.0f);
		VM_C1_Note				= Mathf.Clamp(VM_C1_Note,    		   -1,    127);
		VM_C1_PulseWidth		= Mathf.Clamp(VM_C1_PulseWidth,    		0.0f, 1.0f);
		VM_C1_CycledNoiseSize	= Mathf.Clamp(VM_C1_CycledNoiseSize,	0,    1024);
		VM_C2_TrackPitch        = Mathf.Clamp(VM_C2_TrackPitch,      	0,    1);
		VM_C2_Type    			= Mathf.Clamp(VM_C2_Type,    			0,    3);
		VM_C2_Gain 				= Mathf.Clamp(VM_C2_Gain,    			0.0f, 1.0f);
		VM_C2_Freq				= Mathf.Clamp(VM_C2_Freq,    			0.0f, 96000.0f);
		VM_C2_Note				= Mathf.Clamp(VM_C2_Note,    		   -1,    127);
		VM_C2_PulseWidth		= Mathf.Clamp(VM_C2_PulseWidth,    		0.0f, 1.0f);
		VM_C2_CycledNoiseSize	= Mathf.Clamp(VM_C2_CycledNoiseSize,    0,    1024);

		// We will send these in as Int and use 100 as a scale
		// Might go to 1000 :)
		
		// VoiceMod and Vocoder values
		SendParameter(ovrVoiceModParams.MixInputAudio,  (int)(100.0f * VM_MixAudio));
		SendParameter(ovrVoiceModParams.PitchInputAudio,(int)(100.0f * VM_Pitch));
		SendParameter(ovrVoiceModParams.SetBands, (int)VM_Bands);
		SendParameter(ovrVoiceModParams.FormantCorrection, (int)VM_FormantCorrect);

		// Carrier 1
		SendParameter(ovrVoiceModParams.Carrier1_TrackPitch, (int)VM_C1_TrackPitch);
		SendParameter(ovrVoiceModParams.Carrier1_Type, (int)VM_C1_Type);
		SendParameter(ovrVoiceModParams.Carrier1_Gain, (int)(100.0f * VM_C1_Gain));

		// Note overrides Frequency if valid range
		if(VM_C1_Note == -1)
			SendParameter(ovrVoiceModParams.Carrier1_Frequency, (int)(100.0f * VM_C1_Freq));
		else
			SendParameter(ovrVoiceModParams.Carrier1_Note, (int)VM_C1_Note);

		SendParameter(ovrVoiceModParams.Carrier1_PulseWidth, (int)(100.0f * VM_C1_PulseWidth));
		SendParameter(ovrVoiceModParams.Carrier1_CycledNoiseSize, (int)VM_C1_CycledNoiseSize);

		// Carrier 2
		SendParameter(ovrVoiceModParams.Carrier2_TrackPitch, (int)VM_C2_TrackPitch);
		SendParameter(ovrVoiceModParams.Carrier2_Type, (int)VM_C2_Type);
		SendParameter(ovrVoiceModParams.Carrier2_Gain, (int)(100.0f * VM_C2_Gain));

		// Note overrides Frequency if valid range
		if(VM_C2_Note == -1)
			SendParameter(ovrVoiceModParams.Carrier2_Frequency, (int)(100.0f * VM_C2_Freq));
		else
			SendParameter(ovrVoiceModParams.Carrier2_Note, (int)VM_C2_Note);

		SendParameter(ovrVoiceModParams.Carrier2_PulseWidth, (int)(100.0f * VM_C2_PulseWidth));
		SendParameter(ovrVoiceModParams.Carrier2_CycledNoiseSize, (int)VM_C1_CycledNoiseSize);
	}
}
