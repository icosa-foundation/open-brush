package org.openbrush.audio;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.media.projection.MediaProjectionManager;
import android.os.Bundle;
import android.util.Log;
import com.unity3d.player.UnityPlayer;

public class OpenBrushAudioPlaybackCaptureActivity extends Activity {
    private static final String TAG = "AR_AUDIO_DBG_20260605";
    private static final String UNITY_CALLBACK_OBJECT = "AndroidPlaybackAudio";
    private static final String UNITY_CALLBACK_METHOD = "OnAndroidPlaybackCaptureEvent";
    private static final int REQUEST_MEDIA_PROJECTION = 9204;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        Log.i(TAG, "OpenBrushAudioPlaybackCaptureActivity onCreate");
        sendUnityEvent("activity onCreate savedInstanceStateNull=" + (savedInstanceState == null));
        MediaProjectionManager manager =
                (MediaProjectionManager)getSystemService(Context.MEDIA_PROJECTION_SERVICE);
        startActivityForResult(manager.createScreenCaptureIntent(), REQUEST_MEDIA_PROJECTION);
        sendUnityEvent("activity startedMediaProjectionIntent");
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        Log.i(TAG, "OpenBrushAudioPlaybackCaptureActivity onActivityResult requestCode="
                + requestCode + " resultCode=" + resultCode + " dataNull=" + (data == null));
        sendUnityEvent("activity onActivityResult requestCode=" + requestCode
                + " resultCode=" + resultCode + " dataNull=" + (data == null));
        if (requestCode == REQUEST_MEDIA_PROJECTION) {
            OpenBrushAudioPlaybackCapture.onProjectionResult(resultCode, data);
        }
        finish();
    }

    private static void sendUnityEvent(String message) {
        try {
            UnityPlayer.UnitySendMessage(UNITY_CALLBACK_OBJECT, UNITY_CALLBACK_METHOD, message);
        } catch (Exception e) {
            Log.w(TAG, "Unity callback failed message=" + message, e);
        }
    }
}
