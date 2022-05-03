/************************************************************************************
Filename    :   ONSPAmbisonicsNative.cs
Content     :   Native interface into the Oculus Ambisonics
Copyright   :   Copyright (c) Facebook Technologies, LLC and its affiliates. All rights reserved.

Licensed under the Oculus SDK Version 3.5 (the "License"); 
you may not use the Oculus SDK except in compliance with the License, 
which is provided at the time of installation or download, or which 
otherwise accompanies this software in either electronic or hard copy form.

You may obtain a copy of the License at

https://developer.oculus.com/licenses/sdk-3.5/

Unless required by applicable law or agreed to in writing, the Oculus SDK 
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
************************************************************************************/
using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;

public class ONSPAmbisonicsNative : MonoBehaviour
{
#if !UNITY_5
	static int numFOAChannels    = 4;  // we are only dealing with 1st order Ambisonics at this time
    static int paramVSpeakerMode = 6;  // set speaker mode (OculusAmbi or VSpeaker)
    static int paramAmbiStat     = 7;  // use this to return internal Ambisonic status

    // Staus codes that may return from Ambisonic engine
    public enum ovrAmbisonicsNativeStatus
    {
        Uninitialized = -1,     // Ambisonic stream not initialized (inital status)
        NotEnabled,             // Ambisonic has not been enabled on clip 
        Success,                // Stream initialized and playing
        StreamError,            // Something wrong with input stream (not a 4-channel AmbiX format stream?)
        ProcessError,           // Handling of stream error
        MaxStatValue
    };

    // current status
    ovrAmbisonicsNativeStatus currentStatus = ovrAmbisonicsNativeStatus.Uninitialized;

    // true to use Virtual Speaker output. Otherwise use OculusAmbi
    [SerializeField]
    private bool useVirtualSpeakers = false;
    public bool UseVirtualSpeakers
    {
        get
        {
            return useVirtualSpeakers;
        }
        set
        {
            useVirtualSpeakers = value;
        }
    }
#endif
    /// <summary>
    /// OnEnable this instance.
    /// </summary>
    void OnEnable()
	{
// Unity 4 is deprecated; UNITY_5 still valid with plug-in
#if UNITY_5
        Debug.Log("Ambisonic ERROR: Ambisonic support in Unity 2017 or higher");
#else

        AudioSource source = GetComponent<AudioSource>();

        currentStatus = ovrAmbisonicsNativeStatus.Uninitialized;

        if (source == null)
		{
			Debug.Log("Ambisonic ERROR: AudioSource does not exist.");
		}
		else
		{
			if(source.spatialize == true)
            {
                Debug.Log("Ambisonic WARNING: Turning spatialize field off for Ambisonic sources.");
                source.spatialize = false;
            }

            if (source.clip == null)
			{
				Debug.Log("Ambisonic ERROR: AudioSource does not contain an audio clip.");
			}
			else
			{
				if(source.clip.channels != numFOAChannels)
				{
					Debug.Log("Ambisonic ERROR: AudioSource clip does not have correct number of channels.");
				}
			}
		}
#endif
    }

// Unity 4 is deprecated; UNITY_5 still valid with plug-in
#if !UNITY_5
    /// <summary>
    /// Update this instance.
    /// </summary>
    void Update()
    {
        AudioSource source = GetComponent<AudioSource>();

        if (source == null)
        {
            // We already caught the error in Awake so bail
            return;
        }

        // Set speaker mode
        if(useVirtualSpeakers == true)
            source.SetAmbisonicDecoderFloat(paramVSpeakerMode, 1.0f); // VSpeakerMode
        else
            source.SetAmbisonicDecoderFloat(paramVSpeakerMode, 0.0f); // OclusAmbi 

        float statusF = 0.0f;
        // PGG 5/25/2017 There is a bug in the 2017.2 beta that does not
        // allow for ambisonic params to be passed through to native
        // from C# Get latest editor from Unity when available
        source.GetAmbisonicDecoderFloat(paramAmbiStat, out statusF);

        ovrAmbisonicsNativeStatus status = (ovrAmbisonicsNativeStatus)statusF;

        // TODO: Add native result/error codes
        if (status != currentStatus)
        {
            switch(status)
            {
                case (ovrAmbisonicsNativeStatus.NotEnabled):
                    Debug.Log("Ambisonic Native: Ambisonic not enabled on clip. Check clip field and turn it on");
                    break;

                case (ovrAmbisonicsNativeStatus.Uninitialized):
                    Debug.Log("Ambisonic Native: Stream uninitialized");
                    break;

                case (ovrAmbisonicsNativeStatus.Success):
                    Debug.Log("Ambisonic Native: Stream successfully initialized and playing/playable");
                    break;

                case (ovrAmbisonicsNativeStatus.StreamError):
                    Debug.Log("Ambisonic Native WARNING: Stream error (bad input format?)");
                    break;

                case (ovrAmbisonicsNativeStatus.ProcessError):
                    Debug.Log("Ambisonic Native WARNING: Stream process error (check default speaker setup)");
                    break;

                default:
                    break;
            }
        }

        currentStatus = status;
    }
#endif
}
