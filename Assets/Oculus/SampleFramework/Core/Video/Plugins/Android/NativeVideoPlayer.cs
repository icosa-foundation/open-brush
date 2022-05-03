using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class NativeVideoPlayer {

    private static System.IntPtr? _Activity;
    private static System.IntPtr? _VideoPlayerClass;

    private static System.IntPtr VideoPlayerClass
    {
        get
        {
            if (!_VideoPlayerClass.HasValue)
            {
                try 
                {
                    System.IntPtr myVideoPlayerClass = AndroidJNI.FindClass("com/oculus/videoplayer/NativeVideoPlayer");

                    if (myVideoPlayerClass != System.IntPtr.Zero)
                    {
                        _VideoPlayerClass = AndroidJNI.NewGlobalRef(myVideoPlayerClass);

                        AndroidJNI.DeleteLocalRef(myVideoPlayerClass);
                    }
                    else
                    {
                        Debug.LogError("Failed to find NativeVideoPlayer class");
                        _VideoPlayerClass = System.IntPtr.Zero;
                    }
                }
                catch(System.Exception ex)
                {
                    Debug.LogError("Failed to find NativeVideoPlayer class");
                    Debug.LogException(ex);
                    _VideoPlayerClass = System.IntPtr.Zero;
                }
            }
            return _VideoPlayerClass.GetValueOrDefault();
        }
    }

    private static System.IntPtr Activity
    {
        get
        {
            if (!_Activity.HasValue)
            {
                try
                {
                    System.IntPtr unityPlayerClass = AndroidJNI.FindClass("com/unity3d/player/UnityPlayer");
                    System.IntPtr currentActivityField = AndroidJNI.GetStaticFieldID(unityPlayerClass, "currentActivity", "Landroid/app/Activity;");
                    System.IntPtr activity = AndroidJNI.GetStaticObjectField(unityPlayerClass, currentActivityField);

                    _Activity = AndroidJNI.NewGlobalRef(activity);

                    AndroidJNI.DeleteLocalRef(activity);
                    AndroidJNI.DeleteLocalRef(unityPlayerClass);
                }
                catch(System.Exception ex)
                {
                    Debug.LogException(ex);
                    _Activity = System.IntPtr.Zero;
                }
            }
            return _Activity.GetValueOrDefault();
        }
    }

    private static System.IntPtr playVideoMethodId;
    private static System.IntPtr stopMethodId;
    private static System.IntPtr resumeMethodId;
    private static System.IntPtr pauseMethodId;
    private static System.IntPtr setPlaybackSpeedMethodId;
    private static System.IntPtr setLoopingMethodId;

    public static bool IsAvailable
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return VideoPlayerClass != System.IntPtr.Zero;
#else
            return false;
#endif
        }
    }

    public static void PlayVideo(string path, string drmLicenseUrl, System.IntPtr surfaceObj)
    {
        if (playVideoMethodId == System.IntPtr.Zero)
        {
            playVideoMethodId = AndroidJNI.GetStaticMethodID(VideoPlayerClass, "playVideo", "(Landroid/content/Context;Ljava/lang/String;Ljava/lang/String;Landroid/view/Surface;)V");
        }

        System.IntPtr filePathJString = AndroidJNI.NewStringUTF(path);
        System.IntPtr drmLicenseUrlJString = AndroidJNI.NewStringUTF(drmLicenseUrl);

        AndroidJNI.CallStaticVoidMethod(VideoPlayerClass, playVideoMethodId, new jvalue[] { new jvalue { l = Activity }, new jvalue { l = filePathJString }, new jvalue { l = drmLicenseUrlJString }, new jvalue { l = surfaceObj } });

        AndroidJNI.DeleteLocalRef(filePathJString);
        AndroidJNI.DeleteLocalRef(drmLicenseUrlJString);
    }

    public static void Stop()
    {
        if (stopMethodId == System.IntPtr.Zero)
        {
            stopMethodId = AndroidJNI.GetStaticMethodID(VideoPlayerClass, "stop", "()V");
        }

        AndroidJNI.CallStaticVoidMethod(VideoPlayerClass, stopMethodId, new jvalue[0]);
    }

    public static void Play()
    {
        if (resumeMethodId == System.IntPtr.Zero)
        {
            resumeMethodId = AndroidJNI.GetStaticMethodID(VideoPlayerClass, "resume", "()V");
        }

        AndroidJNI.CallStaticVoidMethod(VideoPlayerClass, resumeMethodId, new jvalue[0]);        
    }

    public static void Pause()
    {
        if (pauseMethodId == System.IntPtr.Zero)
        {
            pauseMethodId = AndroidJNI.GetStaticMethodID(VideoPlayerClass, "pause", "()V");
        }

        AndroidJNI.CallStaticVoidMethod(VideoPlayerClass, pauseMethodId, new jvalue[0]);        
    }

    public static void SetPlaybackSpeed(float speed)
    {
        if (setPlaybackSpeedMethodId == System.IntPtr.Zero)
        {
            setPlaybackSpeedMethodId = AndroidJNI.GetStaticMethodID(VideoPlayerClass, "setPlaybackSpeed", "(f)V");
        }

        AndroidJNI.CallStaticVoidMethod(VideoPlayerClass, setPlaybackSpeedMethodId, new jvalue[] { new jvalue { f = speed } });
    }
    public static void SetLooping(bool looping)
    {
        if (setLoopingMethodId == System.IntPtr.Zero)
        {
            setLoopingMethodId = AndroidJNI.GetStaticMethodID(VideoPlayerClass, "setLooping", "(Z)V");
        }

        AndroidJNI.CallStaticVoidMethod(VideoPlayerClass, setLoopingMethodId, new jvalue[] { new jvalue { z = looping } });
    }
}
