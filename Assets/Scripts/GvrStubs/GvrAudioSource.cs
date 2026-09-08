using System;
using UnityEngine;
using UnityEngine.Audio;

public class GvrAudioSource : MonoBehaviour
{

    /// Denotes whether the room effects should be bypassed.
    public bool bypassRoomEffects = false;

    /// Directivity pattern shaping factor.
    public float directivityAlpha = 0.0f;

    /// Directivity pattern order.
    public float directivitySharpness = 1.0f;

    /// Listener directivity pattern shaping factor.
    public float listenerDirectivityAlpha = 0.0f;

    /// Listener directivity pattern order.
    public float listenerDirectivitySharpness = 1.0f;

    /// Input gain in decibels.
    public float gainDb = 0.0f;

    /// Occlusion effect toggle.
    public bool occlusionEnabled = false;

    /// Play source on awake.
    public bool playOnAwake = true;

    /// Disable the gameobject when sound isn't playing.
    public bool disableOnStop = false;

    /// The default AudioClip to play.
    public AudioClip sourceClip = null;

    /// Is the audio clip looping?
    public bool sourceLoop = false;

    /// Un- / Mutes the source. Mute sets the volume=0, Un-Mute restore the original volume.
    public bool sourceMute = false;

    /// The pitch of the audio source.
    [Range(-3.0f, 3.0f)]
    public float sourcePitch = 1.0f;

    /// Sets the priority of the audio source.
    public int sourcePriority = 128;

    /// Sets how much this source is affected by 3D spatialization calculations (attenuation, doppler).
    public float sourceSpatialBlend = 1.0f;

    /// Sets the Doppler scale for this audio source.
    public float sourceDopplerLevel = 1.0f;

    /// Sets the spread angle (in degrees) in 3D space.
    public float sourceSpread = 0.0f;

    /// The volume of the audio source (0.0 to 1.0).
    public float sourceVolume = 1.0f;

    /// Volume rolloff model with respect to the distance.
    public AudioRolloffMode sourceRolloffMode = AudioRolloffMode.Logarithmic;

    public float sourceMaxDistance = 500.0f;

    public float sourceMinDistance = 1.0f;

    /// Binaural (HRTF) rendering toggle.
    public bool hrtfEnabled = true;

    // Unity audio source attached to the game object.
    public AudioSource audioSource = null;

}
