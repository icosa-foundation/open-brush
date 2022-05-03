/************************************************************************************
Filename    :   OVRMicInput.cs
Content     :   Interface to microphone input
Created     :   May 12, 2015
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
using System.Collections;

[RequireComponent(typeof(AudioSource))]

public class OVRMicInput : MonoBehaviour 
{
	public enum micActivation 
	{
		HoldToSpeak,
		PushToSpeak,
		ConstantSpeak
	}

	// PUBLIC MEMBERS
	public AudioSource audioSource = null;
	public bool GuiSelectDevice    = true;

	[SerializeField]
	private float sensitivity  = 100;
	public float Sensitivity
	{
		get{return sensitivity;}
		set{sensitivity = Mathf.Clamp (value, 0, 100);}
	}

	[SerializeField]
	private float sourceVolume = 100;
	public float SourceVolume
	{
		get{return sourceVolume;}
		set{sourceVolume = Mathf.Clamp (value, 0, 100);}
	}

	[SerializeField]
	private int micFrequency = 16000;
	public float MicFrequency
	{
		get{return micFrequency;}
		set{micFrequency = (int)Mathf.Clamp ((float)value, 0, 96000);}
	}


	public micActivation micControl;

	public string selectedDevice; 

	public float loudness; // Use this to chenge visual values. Range is 0 - 100

	// PRIVATE MEMBERS
	private bool micSelected = false;
	private int minFreq, maxFreq;
	private bool focused = true;
	
	//----------------------------------------------------
	// MONOBEHAVIOUR OVERRIDE FUNCTIONS
	//----------------------------------------------------

	/// <summary>
	/// Awake this instance.
	/// </summary>
	void Awake()
	{
		// First thing to do, cache the unity audio source (can be managed by the
		// user if audio source can change)
		if (!audioSource) audioSource = GetComponent<AudioSource>();
		if (!audioSource) return; // this should never happen
	}

	/// <summary>
	/// Start this instance.
	/// </summary>
	void Start() 
	{
		audioSource.loop = true; 	// Set the AudioClip to loop
		audioSource.mute = false; 	

		if(Microphone.devices.Length!= 0)
		{
			selectedDevice = Microphone.devices[0].ToString();
			micSelected = true;
			GetMicCaps();
		}
	}

	/// <summary>
	/// Update this instance.
	/// </summary>
	void Update() 
	{
		if (!focused)
			StopMicrophone();
		
		if (!Application.isPlaying) 
			StopMicrophone();
		
		audioSource.volume = (sourceVolume / 100);
		loudness = Mathf.Clamp(GetAveragedVolume() * sensitivity * (sourceVolume / 10), 0, 100);
		
		//Hold To Speak
		if (micControl == micActivation.HoldToSpeak) 
		{
			if (Microphone.IsRecording(selectedDevice) && Input.GetKey(KeyCode.Space) == false)
				StopMicrophone();
			
			if (Input.GetKeyDown(KeyCode.Space)) //Push to talk
				StartMicrophone();
			
			if (Input.GetKeyUp(KeyCode.Space))
				StopMicrophone();
		}
		
		//Push To Talk
		if (micControl == micActivation.PushToSpeak) 
		{
			if (Input.GetKeyDown(KeyCode.Space)) 
			{
				if (Microphone.IsRecording(selectedDevice))
					StopMicrophone();
				else if (!Microphone.IsRecording(selectedDevice))
					StartMicrophone();
			}
		}
		
		//Constant Speak
		if (micControl == micActivation.ConstantSpeak)
			if (!Microphone.IsRecording(selectedDevice))
				StartMicrophone();
		
		
		//Mic Slected = False
		if (Input.GetKeyDown(KeyCode.M))
			micSelected = false;
	}
	
	
	/// <summary>
	/// Raises the application focus event.
	/// </summary>
	/// <param name="focus">If set to <c>true</c> focus.</param>
	void OnApplicationFocus(bool focus) 
	{
		focused = focus;

		// fixes app with a delayed buffer if going out of focus
		if (!focused)
			StopMicrophone();
	}
	
	/// <summary>
	/// Raises the application pause event.
	/// </summary>
	/// <param name="focus">If set to <c>true</c> focus.</param>
	void OnApplicationPause(bool focus) 
	{
		focused = focus;

		// fixes app with a delayed buffer if going out of focus
		if (!focused)
			StopMicrophone();
	}

	void OnDisable()
	{
		StopMicrophone();
	}

	/// <summary>
	/// Raises the GU event.
	/// </summary>
	void OnGUI() 
	{
		MicDeviceGUI((Screen.width/2)-150, (Screen.height/2)-75, 300, 50, 10, -300);
	}

	//----------------------------------------------------
	// PUBLIC FUNCTIONS
	//----------------------------------------------------

	/// <summary>
	/// Mics the device GU.
	/// </summary>
	/// <param name="left">Left.</param>
	/// <param name="top">Top.</param>
	/// <param name="width">Width.</param>
	/// <param name="height">Height.</param>
	/// <param name="buttonSpaceTop">Button space top.</param>
	/// <param name="buttonSpaceLeft">Button space left.</param>
	public void MicDeviceGUI (float left, float top, float width, float height, float buttonSpaceTop, float buttonSpaceLeft) 
	{
		//If there is more than one device, choose one.
		if (Microphone.devices.Length >= 1 && GuiSelectDevice == true && micSelected == false)
		{
			for (int i = 0; i < Microphone.devices.Length; ++i)
			{
				if (GUI.Button(new Rect(left + ((width + buttonSpaceLeft) * i), top + ((height + buttonSpaceTop) * i), width, height), 
				               Microphone.devices[i].ToString())) 
				{
					StopMicrophone();
					selectedDevice = Microphone.devices[i].ToString();
					micSelected = true;
					GetMicCaps();
					StartMicrophone();
				}
			}
		}
	}

	/// <summary>
	/// Gets the mic caps.
	/// </summary>
	public void GetMicCaps () 
	{
		if(micSelected == false) return;

		//Gets the frequency of the device
		Microphone.GetDeviceCaps(selectedDevice, out minFreq, out maxFreq);

		if ( minFreq == 0 && maxFreq == 0 )
		{
			Debug.LogWarning ("GetMicCaps warning:: min and max frequencies are 0");
			minFreq = 44100;
			maxFreq = 44100;
		}
	
		if (micFrequency > maxFreq)
			micFrequency = maxFreq;
	}

	/// <summary>
	/// Starts the microphone.
	/// </summary>
	public void StartMicrophone () 
	{
		if(micSelected == false) return;
			
		//Starts recording
		audioSource.clip = Microphone.Start(selectedDevice, true, 1, micFrequency);

		// Wait until the recording has started
		while (!(Microphone.GetPosition(selectedDevice) > 0)){}

		// Play the audio source
		audioSource.Play();
	}

	/// <summary>
	/// Stops the microphone.
	/// </summary>
	public void StopMicrophone () 
	{
		if(micSelected == false) return;

		// Overriden with a clip to play? Don't stop the audio source
		if((audioSource != null) && (audioSource.clip != null) &&(audioSource.clip.name == "Microphone"))
			audioSource.Stop();

		Microphone.End(selectedDevice);
	}    


	//----------------------------------------------------
	// PRIVATE FUNCTIONS
	//----------------------------------------------------

	/// <summary>
	/// Gets the averaged volume.
	/// </summary>
	/// <returns>The averaged volume.</returns>
	float GetAveragedVolume() 
	{
		// We will use the SR to get average volume
		// return OVRSpeechRec.GetAverageVolume();
		return 0.0f;
	}
}
