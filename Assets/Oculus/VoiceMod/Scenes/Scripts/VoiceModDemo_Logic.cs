using UnityEngine;
using System.Collections;

public class VoiceModDemo_Logic : MonoBehaviour 
{
	public OVRVoiceModContext[] contexts;
	public Material material;
	public Transform[] xfrms;
	public VoiceModEnableSwitch SwitchTarget;

	private int targetSet = 0;

	// Use this as the init value for the model scale
	private Vector3 scale = new Vector3(3.0f, 3.0f, 3.0f);
	private float scaleMax = 10.0f;

	private int currentPreset = 0;

	// Use this for initialization
	void Start () 
	{
		// Add a listener to the OVRMessenger for touch events
		OVRMessenger.AddListener<OVRTouchpad.TouchEvent>("Touchpad", LocalTouchEventCallback);

		// Initialize the proper target set
		targetSet = 0;
		SwitchTarget.SetActive(0);

		// Set initial color on models
		if(material != null)
			material.SetColor("_Color", Color.grey);
		
	}
	
	// Update is called once per frame
	// Logic for LipSync_Demo
	void Update () 
	{
		// Change preset
		int preset = -1;

		if(Input.GetKeyDown(KeyCode.Alpha1))
			preset = 0;
		else
		if(Input.GetKeyDown(KeyCode.Alpha2))
			preset = 1;
		else
		if(Input.GetKeyDown(KeyCode.Alpha3))
			preset = 2;
		else
		if(Input.GetKeyDown(KeyCode.Alpha4))
			preset = 3;
		else
		if(Input.GetKeyDown(KeyCode.Alpha5))
			preset = 4;
		else
		if(Input.GetKeyDown(KeyCode.Alpha6))
			preset = 5;
		else
		if(Input.GetKeyDown(KeyCode.Alpha7))
			preset = 6;
		else
		if(Input.GetKeyDown(KeyCode.Alpha8))
			preset = 7;
		else
		if(Input.GetKeyDown(KeyCode.Alpha9))
			preset = 8;
		else
		if(Input.GetKeyDown(KeyCode.Alpha0))
			preset = 9;

		if(preset != -1)
		{
			Color c = Color.black;

			for (int i = 0; i < contexts.Length; i++)
			{
				if(contexts[i].SetPreset(preset) == true)
				{
					// query color from preset and set material color
					// of sphere
					c = contexts[i].GetPresetColor(preset);
				}
			}

			// Set the material(s) note: each context is sharing a single 
			// material in this demo :)
			if(material != null)
				material.SetColor("_Color", c);
		}

		// Update transforms with context average volume
		UpdateModelScale();

		// Change visible target context 
		if (Input.GetKeyDown(KeyCode.Z))
		{
			targetSet = 0;
			SetCurrentTarget();
		}
		else
		if (Input.GetKeyDown(KeyCode.X))
		{
			targetSet = 1;
			SetCurrentTarget();
		}

		// Close app
		if(Input.GetKeyDown (KeyCode.Escape))
		   Application.Quit();
	}

	/// <summary>
	/// Sets the current target.
	/// </summary>
	void SetCurrentTarget()
	{
		switch(targetSet)
		{
		case(0):
			SwitchTarget.SetActive(0);
			OVRDebugConsole.Clear();
			OVRDebugConsole.Log("MICROPHONE INPUT");
			OVRDebugConsole.ClearTimeout(1.5f);

			break;
		case(1):
			SwitchTarget.SetActive(1);
			OVRDebugConsole.Clear();
			OVRDebugConsole.Log("SAMPLE INPUT");
			OVRDebugConsole.ClearTimeout(1.5f);

			break;
		}
	}
	
	/// <summary>
	/// Local touch event callback.
	/// </summary>
	/// <param name="touchEvent">Touch event.</param>
	void LocalTouchEventCallback(OVRTouchpad.TouchEvent touchEvent)
	{
		switch(touchEvent)
		{
			case(OVRTouchpad.TouchEvent.Left):
		
			targetSet--;
			if(targetSet < 0)
				targetSet = 1;

			SetCurrentTarget();

			break;
			
			case(OVRTouchpad.TouchEvent.Right):

			targetSet++;
			if(targetSet > 1)
				targetSet = 0;

			SetCurrentTarget();

			break;

		case(OVRTouchpad.TouchEvent.Up):

			if(contexts.Length != 0)
			{
				if(contexts[0].GetNumPresets() == 0)
				{
					OVRDebugConsole.Clear();
					OVRDebugConsole.Log("NO PRESETS!");
					OVRDebugConsole.ClearTimeout(1.5f);
				}
				else
				{
					currentPreset++;
					if(currentPreset >= contexts[0].GetNumPresets())
						currentPreset = 0;

					Color c = Color.black;

					for (int i = 0; i < contexts.Length; i++)
					{
						if(contexts[i].SetPreset(currentPreset) == true)
						{
							// query color from preset and set material color
							// of sphere
							c = contexts[i].GetPresetColor(currentPreset);
						}
					}

					// Set the material(s) note: each context is sharing a single 
					// material in this demo :)
					if(material != null)
						material.SetColor("_Color", c);
				}
			}
				
			break;

		case(OVRTouchpad.TouchEvent.Down):
			if(contexts.Length != 0)
			{
				if(contexts[0].GetNumPresets() == 0)
				{
					OVRDebugConsole.Clear();
					OVRDebugConsole.Log("NO PRESETS!");
					OVRDebugConsole.ClearTimeout(1.5f);
				}
				else
				{
					currentPreset--;
					if(currentPreset < 0)
						currentPreset = contexts[0].GetNumPresets() - 1;

					Color c = Color.black;

					for (int i = 0; i < contexts.Length; i++)
					{
						if(contexts[i].SetPreset(currentPreset) == true)
						{
							// query color from preset and set material color
							// of sphere
							c = contexts[i].GetPresetColor(currentPreset);
						}
					}

					// Set the material(s) note: each context is sharing a single 
					// material in this demo :)
					if(material != null)
						material.SetColor("_Color", c);
				}
			}
				
			break;

		}
	}

	/// <summary>
	/// Updates the model scale.
	/// </summary>
	void UpdateModelScale()
	{
		for (int i = 0; i < xfrms.Length; i++)
		{
			if(i < contexts.Length)
			{
				xfrms[i].localScale = scale * (1.0f + (contexts[i].GetAverageAbsVolume() * scaleMax));
			}
		}
	}
}
