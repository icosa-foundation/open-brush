package org.openbrush.audio;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ServiceInfo;
import android.os.Build;
import android.os.IBinder;
import android.util.Log;

public class OpenBrushMediaProjectionService extends Service {
    private static final String TAG = "AR_AUDIO_DBG_20260605";
    private static final String CHANNEL_ID = "openbrush_audio_projection";
    private static final int NOTIFICATION_ID = 9205;
    private static final String EXTRA_RESULT_CODE = "org.openbrush.audio.RESULT_CODE";
    private static final String EXTRA_RESULT_DATA = "org.openbrush.audio.RESULT_DATA";

    public static void startProjection(Context context, int resultCode, Intent resultData) {
        Intent intent = new Intent(context, OpenBrushMediaProjectionService.class);
        intent.putExtra(EXTRA_RESULT_CODE, resultCode);
        intent.putExtra(EXTRA_RESULT_DATA, resultData);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            context.startForegroundService(intent);
        } else {
            context.startService(intent);
        }
    }

    public static void stopProjection(Context context) {
        if (context == null) {
            return;
        }
        context.stopService(new Intent(context, OpenBrushMediaProjectionService.class));
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        Log.i(TAG, "OpenBrushMediaProjectionService onStartCommand intentNull=" + (intent == null));
        try {
            startForegroundForMediaProjection();
        } catch (Exception e) {
            Log.w(TAG, "OpenBrushMediaProjectionService startForeground failed", e);
            OpenBrushAudioPlaybackCapture.onForegroundServiceFailed(e.toString());
            stopSelf();
            return START_NOT_STICKY;
        }

        if (intent != null && intent.hasExtra(EXTRA_RESULT_CODE)) {
            int resultCode = intent.getIntExtra(EXTRA_RESULT_CODE, 0);
            Intent resultData = intent.getParcelableExtra(EXTRA_RESULT_DATA);
            OpenBrushAudioPlaybackCapture.onForegroundServiceReady(this, resultCode, resultData);
        }

        return START_STICKY;
    }

    @Override
    public void onDestroy() {
        Log.i(TAG, "OpenBrushMediaProjectionService onDestroy");
        super.onDestroy();
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    private void startForegroundForMediaProjection() {
        Notification notification = buildNotification();
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(
                    NOTIFICATION_ID,
                    notification,
                    ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION);
        } else {
            startForeground(NOTIFICATION_ID, notification);
        }
        Log.i(TAG, "OpenBrushMediaProjectionService startForeground mediaProjection");
    }

    private Notification buildNotification() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            NotificationManager manager =
                    (NotificationManager)getSystemService(Context.NOTIFICATION_SERVICE);
            NotificationChannel channel = new NotificationChannel(
                    CHANNEL_ID,
                    "Open Brush audio capture",
                    NotificationManager.IMPORTANCE_LOW);
            manager.createNotificationChannel(channel);
            return new Notification.Builder(this, CHANNEL_ID)
                    .setContentTitle("Open Brush audio capture")
                    .setContentText("Capturing playback audio for audio-reactive brushes")
                    .setSmallIcon(android.R.drawable.ic_media_play)
                    .setOngoing(true)
                    .build();
        }

        return new Notification.Builder(this)
                .setContentTitle("Open Brush audio capture")
                .setContentText("Capturing playback audio for audio-reactive brushes")
                .setSmallIcon(android.R.drawable.ic_media_play)
                .setOngoing(true)
                .build();
    }
}
