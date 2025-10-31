using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class AndroidHttpServerBridge
{
    public static void StartNativeHttpServer(int port)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {

            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaClass httpServerClass = new AndroidJavaClass("com.example.myhttpserver.MyHttpServer");
                AndroidJavaObject httpServer = httpServerClass.CallStatic<AndroidJavaObject>("getInstance", port);
                
                // Start the native server
                httpServer.Call("start");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to start native HTTP server: " + ex.Message);
        }
#endif
    }
}
