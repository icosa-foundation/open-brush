using UnityEngine;

public class GvrAudioListener : MonoBehaviour
{
    public void Awake()
    {
        if (GetComponent<ResonanceAudioListener>() != null) return;
        var resAudioListener = gameObject.AddComponent<ResonanceAudioListener>();
    }
    
    /// Global gain in decibels to be applied to the processed output.
    public float globalGainDb = 0.0f;

    /// Global layer mask to be used in occlusion detection.
    public LayerMask occlusionMask = -1;

    /// Audio rendering quality of the system.
    public GvrAudio.Quality quality = GvrAudio.Quality.High;
}
