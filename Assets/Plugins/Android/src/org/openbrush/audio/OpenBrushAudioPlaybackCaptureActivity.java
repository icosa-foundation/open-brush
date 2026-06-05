package org.openbrush.audio;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.media.projection.MediaProjectionManager;
import android.os.Bundle;
import android.util.Log;

public class OpenBrushAudioPlaybackCaptureActivity extends Activity {
    private static final String TAG = "AR_AUDIO_DBG_20260605";
    private static final int REQUEST_MEDIA_PROJECTION = 9204;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        Log.i(TAG, "OpenBrushAudioPlaybackCaptureActivity onCreate");
        MediaProjectionManager manager =
                (MediaProjectionManager)getSystemService(Context.MEDIA_PROJECTION_SERVICE);
        startActivityForResult(manager.createScreenCaptureIntent(), REQUEST_MEDIA_PROJECTION);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        Log.i(TAG, "OpenBrushAudioPlaybackCaptureActivity onActivityResult requestCode="
                + requestCode + " resultCode=" + resultCode + " dataNull=" + (data == null));
        if (requestCode == REQUEST_MEDIA_PROJECTION) {
            OpenBrushAudioPlaybackCapture.onProjectionResult(resultCode, data);
        }
        finish();
    }
}
