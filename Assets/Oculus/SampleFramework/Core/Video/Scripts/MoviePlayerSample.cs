/************************************************************************************

Copyright (c) Facebook Technologies, LLC and its affiliates. All rights reserved.  

See SampleFramework license.txt for license terms.  Unless required by applicable law 
or agreed to in writing, the sample code is provided “AS IS” WITHOUT WARRANTIES OR 
CONDITIONS OF ANY KIND, either express or implied.  See the license for specific 
language governing permissions and limitations under the license.

************************************************************************************/

using UnityEngine;
using System;
using System.IO;

public class MoviePlayerSample : MonoBehaviour
{
    private bool    videoPausedBeforeAppPause = false;

	private UnityEngine.Video.VideoPlayer videoPlayer = null;
	private OVROverlay          overlay = null;
	private Renderer 			mediaRenderer = null;

    public bool isPlaying { get; private set; }

    private RenderTexture copyTexture;
    private Material externalTex2DMaterial;

    public string MovieName;
    public string DrmLicenseUrl;
    public bool LoopVideo;
    public VideoShape Shape;
    public VideoStereo Stereo;


    public enum VideoShape
    {
        _360,
        _180,
        Quad
    }

    public enum VideoStereo
    {
        Mono,
        TopBottom,
        LeftRight
    }

    /// <summary>
    /// Initialization of the movie surface
    /// </summary>
    void Awake()
    {
        Debug.Log("MovieSample Awake");

        mediaRenderer = GetComponent<Renderer>();

        videoPlayer = GetComponent<UnityEngine.Video.VideoPlayer>();
        if (videoPlayer == null)
            videoPlayer = gameObject.AddComponent<UnityEngine.Video.VideoPlayer>();
        videoPlayer.isLooping = LoopVideo;

        overlay = GetComponent<OVROverlay>();
        if (overlay == null)
            overlay = gameObject.AddComponent<OVROverlay>();

        Rect destRect = new Rect(0, 0, 1, 1);
        switch (Shape)
        {
            case VideoShape._360:
                // set shape to Equirect
                overlay.currentOverlayShape = OVROverlay.OverlayShape.Equirect;
                break;
            case VideoShape._180:
                overlay.currentOverlayShape = OVROverlay.OverlayShape.Equirect;
                destRect = new Rect(0.25f, 0, 0.5f, 1.0f);
                break;
            case VideoShape.Quad:
            default:
                overlay.currentOverlayShape = OVROverlay.OverlayShape.Quad;
                break;
        }

        overlay.overrideTextureRectMatrix = true;

        Rect sourceLeft = new Rect(0, 0, 1, 1);
        Rect sourceRight = new Rect(0, 0, 1, 1);
        switch (Stereo)
        {
            case VideoStereo.LeftRight:
                // set source matrices for left/right
                sourceLeft = new Rect(0, 0, 0.5f, 1.0f);
                sourceRight = new Rect(0.5f, 0, 0.5f, 1.0f);
                break;
            case VideoStereo.TopBottom:
                // set source matrices for top/bottom
                sourceLeft = new Rect(0, 0, 1.0f, 0.5f);
                sourceRight = new Rect(0, 0.5f, 1.0f, 0.5f);
                break;
        }
        overlay.SetSrcDestRects(sourceLeft, sourceRight, destRect, destRect);

        // disable it to reset it.
        overlay.enabled = false;
        // only can use external surface with native plugin
        overlay.isExternalSurface = NativeVideoPlayer.IsAvailable;
        // only mobile has Equirect shape
        overlay.enabled = (overlay.currentOverlayShape != OVROverlay.OverlayShape.Equirect || Application.platform == RuntimePlatform.Android);

#if UNITY_EDITOR
        overlay.currentOverlayShape = OVROverlay.OverlayShape.Quad;
        overlay.enabled = true;
#endif
    }

    private bool IsLocalVideo(string movieName)
    {
        // if the path contains any url scheme, it is not local
        return !movieName.Contains("://");
    }

    private System.Collections.IEnumerator Start()
    {
        if (mediaRenderer.material == null)
		{
			Debug.LogError("No material for movie surface");
            yield break;
		}

        // wait 1 second to start (there is a bug in Unity where starting
        // the video too soon will cause it to fail to load)
        yield return new WaitForSeconds(1.0f);

        if (!string.IsNullOrEmpty(MovieName))
        {
            if (IsLocalVideo(MovieName))
            {
#if UNITY_EDITOR
                // in editor, just pull in the movie file from wherever it lives (to test without putting in streaming assets)
                var guids = UnityEditor.AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(MovieName));

                if (guids.Length > 0)
                {
                    string video = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    Play(video, null);
                }
#else
                Play(Application.streamingAssetsPath +"/" + MovieName, null);
#endif
            }
            else
            {
                Play(MovieName, DrmLicenseUrl);
            }
        }
    }

    public void Play(string moviePath, string drmLicencesUrl)
    {
        if (moviePath != string.Empty)
        {
            Debug.Log("Playing Video: " + moviePath);
            if (overlay.isExternalSurface)
            {
                OVROverlay.ExternalSurfaceObjectCreated surfaceCreatedCallback = () =>
                {
                    Debug.Log("Playing ExoPlayer with SurfaceObject");
                    NativeVideoPlayer.PlayVideo(moviePath, drmLicencesUrl, overlay.externalSurfaceObject);
                    NativeVideoPlayer.SetLooping(LoopVideo);
                };

                if (overlay.externalSurfaceObject == IntPtr.Zero)
                {
                    overlay.externalSurfaceObjectCreated = surfaceCreatedCallback;
                }
                else
                {
                    surfaceCreatedCallback.Invoke();
                }
            }
            else
            {
                Debug.Log("Playing Unity VideoPlayer");
                videoPlayer.url = moviePath;
                videoPlayer.Prepare();
                videoPlayer.Play();                
            }

            Debug.Log("MovieSample Start");
            isPlaying = true;
        }
        else
        {
            Debug.LogError("No media file name provided");
        }
    }

    public void Play()
    {
        if (overlay.isExternalSurface)
        {
            NativeVideoPlayer.Play();
        }
        else
        {
            videoPlayer.Play();
        }
        isPlaying = true;
    }

    public void Pause()
    {
        if (overlay.isExternalSurface)
        {
            NativeVideoPlayer.Pause();
        }
        else
        {
            videoPlayer.Pause();
        }
        isPlaying = false;
    }

	void Update()
	{
        if (!overlay.isExternalSurface)            
        {
            var displayTexture = videoPlayer.texture != null ? videoPlayer.texture : Texture2D.blackTexture;
            if (overlay.enabled)
            {
                if (overlay.textures[0] != displayTexture)
                {
                    // OVROverlay won't check if the texture changed, so disable to clear old texture
                    overlay.enabled = false;
                    overlay.textures[0] = displayTexture;
                    overlay.enabled = true;
                }
            }
            else
            {
                mediaRenderer.material.mainTexture = displayTexture;
                mediaRenderer.material.SetVector("_SrcRectLeft", overlay.srcRectLeft.ToVector());
                mediaRenderer.material.SetVector("_SrcRectRight", overlay.srcRectRight.ToVector());
            }
        }
	}

    public void Rewind()
    {
        if (overlay.isExternalSurface)
        {
            NativeVideoPlayer.SetPlaybackSpeed(-1);
        }
        else
        {
            videoPlayer.playbackSpeed = -1;
        }
    }
    
    public void Stop()
    {
        if (overlay.isExternalSurface)
        {
            NativeVideoPlayer.Stop();
        }
        else
        {
            videoPlayer.Stop();
        }

        isPlaying = false;
    }

    /// <summary>
    /// Pauses video playback when the app loses or gains focus
    /// </summary>
    void OnApplicationPause(bool appWasPaused)
    {
        Debug.Log("OnApplicationPause: " + appWasPaused);
        if (appWasPaused)
        {
            videoPausedBeforeAppPause = !isPlaying;
        }
        
        // Pause/unpause the video only if it had been playing prior to app pause
        if (!videoPausedBeforeAppPause)
        {
            if (appWasPaused)
            {
                Pause();
            }
            else
            {
                Play();
            }
        }
    }
}
