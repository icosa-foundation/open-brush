package org.openbrush.audio;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.media.AudioAttributes;
import android.media.AudioFormat;
import android.media.AudioPlaybackCaptureConfiguration;
import android.media.AudioRecord;
import android.media.MediaRecorder;
import android.media.projection.MediaProjection;
import android.media.projection.MediaProjectionManager;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import com.unity3d.player.UnityPlayer;

public class OpenBrushAudioPlaybackCapture {
    private static final String TAG = "AR_AUDIO_DBG_20260605";
    private static final String UNITY_CALLBACK_OBJECT = "AndroidPlaybackAudio";
    private static final String UNITY_CALLBACK_METHOD = "OnAndroidPlaybackCaptureEvent";
    private static final int SAMPLE_RATE = 48000;
    private static final int CHANNEL_COUNT = 2;
    private static final int RING_SAMPLES = 8192;
    private static final int READ_LOG_INTERVAL = 60;

    private static Activity sActivity;
    private static MediaProjection sProjection;
    private static MediaProjection.Callback sProjectionCallback;
    private static AudioRecord sAudioRecord;
    private static Thread sThread;
    private static boolean sRunning;
    private static boolean sRequestPending;
    private static String sLastError = "";
    private static final Object sLock = new Object();
    private static final float[] sRing = new float[RING_SAMPLES];
    private static long sSamplesWritten;
    private static int sLastReadResult;
    private static int sReadLogCountdown;

    public static void initialize(Activity activity) {
        sActivity = activity;
        Log.i(TAG, "OpenBrushAudioPlaybackCapture initialize activityNull=" + (activity == null)
                + " sdk=" + Build.VERSION.SDK_INT
                + " supported=" + isSupported());
        sendUnityEvent("initialize activityNull=" + (activity == null)
                + " sdk=" + Build.VERSION.SDK_INT
                + " supported=" + isSupported());
    }

    public static boolean isSupported() {
        return Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q;
    }

    public static void requestCapture() {
        if (!isSupported()) {
            sLastError = "AudioPlaybackCapture requires Android 10/API 29";
            Log.w(TAG, sLastError);
            sendUnityEvent("requestCapture unsupported error='" + sLastError + "'");
            return;
        }
        if (sActivity == null) {
            sLastError = "Unity activity is not initialized";
            Log.w(TAG, sLastError);
            sendUnityEvent("requestCapture noActivity error='" + sLastError + "'");
            return;
        }
        if (isCapturing()) {
            Log.i(TAG, "OpenBrushAudioPlaybackCapture requestCapture skipped; already capturing");
            sendUnityEvent("requestCapture alreadyCapturing");
            return;
        }

        sRequestPending = true;
        Intent intent = new Intent(sActivity, OpenBrushAudioPlaybackCaptureActivity.class);
        Log.i(TAG, "OpenBrushAudioPlaybackCapture requestCapture starting permission activity");
        sendUnityEvent("requestCapture startPermissionActivity");
        sActivity.startActivity(intent);
    }

    static void onProjectionResult(int resultCode, Intent data) {
        sRequestPending = false;
        Log.i(TAG, "OpenBrushAudioPlaybackCapture onProjectionResult resultCode=" + resultCode
                + " dataNull=" + (data == null));
        sendUnityEvent("onProjectionResult resultCode=" + resultCode + " dataNull=" + (data == null));
        try {
            if (sActivity == null) {
                sLastError = "Unity activity is not initialized";
                Log.w(TAG, sLastError);
                sendUnityEvent("onProjectionResult noActivity error='" + sLastError + "'");
                return;
            }
            if (resultCode != Activity.RESULT_OK || data == null) {
                sLastError = "MediaProjection permission denied";
                Log.w(TAG, sLastError);
                sendUnityEvent("onProjectionResult denied error='" + sLastError + "'");
                return;
            }

            OpenBrushMediaProjectionService.startProjection(sActivity, resultCode, data);
            sendUnityEvent("onProjectionResult foregroundServiceStartRequested");
        } catch (Exception e) {
            sLastError = e.toString();
            Log.w(TAG, "MediaProjection result failed", e);
            sendUnityEvent("onProjectionResult exception error='" + sLastError + "'");
            stop();
        }
    }

    static void onForegroundServiceReady(Context context, int resultCode, Intent data) {
        Log.i(TAG, "OpenBrushAudioPlaybackCapture onForegroundServiceReady resultCode=" + resultCode
                + " dataNull=" + (data == null));
        sendUnityEvent("onForegroundServiceReady resultCode=" + resultCode
                + " dataNull=" + (data == null));
        try {
            if (resultCode != Activity.RESULT_OK || data == null) {
                sLastError = "MediaProjection permission denied";
                Log.w(TAG, sLastError);
                sendUnityEvent("onForegroundServiceReady denied error='" + sLastError + "'");
                return;
            }

            MediaProjectionManager manager =
                    (MediaProjectionManager)context.getSystemService(Context.MEDIA_PROJECTION_SERVICE);
            sProjection = manager.getMediaProjection(resultCode, data);
            if (sProjection == null) {
                sLastError = "MediaProjection result did not create a projection";
                Log.w(TAG, sLastError);
                sendUnityEvent("onForegroundServiceReady nullProjection error='" + sLastError + "'");
                return;
            }
            registerProjectionCallback();
            startAudioRecord();
        } catch (Exception e) {
            sLastError = e.toString();
            Log.w(TAG, "MediaProjection foreground service failed", e);
            sendUnityEvent("onForegroundServiceReady exception error='" + sLastError + "'");
            stop();
        }
    }

    static void onForegroundServiceFailed(String error) {
        sRequestPending = false;
        sLastError = error;
        Log.w(TAG, "OpenBrushAudioPlaybackCapture foreground service failed error=" + error);
        sendUnityEvent("onForegroundServiceFailed error='" + sLastError + "'");
        stop();
    }

    public static void stop() {
        Log.i(TAG, "OpenBrushAudioPlaybackCapture stop running=" + sRunning
                + " hasAudioRecord=" + (sAudioRecord != null)
                + " hasProjection=" + (sProjection != null)
                + " samplesWritten=" + sSamplesWritten
                + " lastRead=" + sLastReadResult
                + " lastError='" + sLastError + "'");
        sendUnityEvent("stop running=" + sRunning
                + " hasAudioRecord=" + (sAudioRecord != null)
                + " hasProjection=" + (sProjection != null)
                + " samplesWritten=" + sSamplesWritten
                + " lastRead=" + sLastReadResult
                + " lastError='" + sLastError + "'");
        sRunning = false;
        Thread thread = sThread;
        sThread = null;
        if (thread != null) {
            try {
                thread.join(500);
            } catch (InterruptedException ignored) {
            }
        }
        if (sAudioRecord != null) {
            try {
                sAudioRecord.stop();
            } catch (IllegalStateException ignored) {
            }
            sAudioRecord.release();
            sAudioRecord = null;
        }
        if (sProjection != null) {
            sProjection.stop();
            sProjection = null;
        }
        OpenBrushMediaProjectionService.stopProjection(sActivity);
        sProjectionCallback = null;
    }

    public static boolean isCapturing() {
        return sRunning && sAudioRecord != null;
    }

    public static boolean isRequestPending() {
        return sRequestPending;
    }

    public static int getSampleRate() {
        return SAMPLE_RATE;
    }

    public static String getLastError() {
        return sLastError;
    }

    public static long getSamplesWritten() {
        synchronized (sLock) {
            return sSamplesWritten;
        }
    }

    public static int getLastReadResult() {
        return sLastReadResult;
    }

    public static float[] readLatest(int sampleCount) {
        float[] result = new float[sampleCount];
        synchronized (sLock) {
            long available = Math.min(sSamplesWritten, RING_SAMPLES);
            int copyCount = (int)Math.min(sampleCount, available);
            long start = sSamplesWritten - copyCount;
            int outputOffset = sampleCount - copyCount;
            for (int i = 0; i < copyCount; ++i) {
                int ringIndex = (int)((start + i) % RING_SAMPLES);
                result[outputOffset + i] = sRing[ringIndex];
            }
        }
        return result;
    }

    private static void startAudioRecord() {
        stopAudioRecordOnly();
        try {
            AudioPlaybackCaptureConfiguration config =
                    new AudioPlaybackCaptureConfiguration.Builder(sProjection)
                            .addMatchingUsage(AudioAttributes.USAGE_MEDIA)
                            .addMatchingUsage(AudioAttributes.USAGE_GAME)
                            .addMatchingUsage(AudioAttributes.USAGE_UNKNOWN)
                            .build();

            AudioFormat format = new AudioFormat.Builder()
                    .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                    .setSampleRate(SAMPLE_RATE)
                    .setChannelMask(AudioFormat.CHANNEL_IN_STEREO)
                    .build();

            int minBuffer = AudioRecord.getMinBufferSize(
                    SAMPLE_RATE, AudioFormat.CHANNEL_IN_STEREO, AudioFormat.ENCODING_PCM_16BIT);
            int bufferSize = Math.max(minBuffer, 4096 * CHANNEL_COUNT * 2);
            Log.i(TAG, "OpenBrushAudioPlaybackCapture startAudioRecord minBuffer=" + minBuffer
                    + " bufferSize=" + bufferSize);
            sendUnityEvent("startAudioRecord minBuffer=" + minBuffer + " bufferSize=" + bufferSize);

            sAudioRecord = new AudioRecord.Builder()
                    .setAudioFormat(format)
                    .setBufferSizeInBytes(bufferSize)
                    .setAudioPlaybackCaptureConfig(config)
                    .build();

            if (sAudioRecord.getState() != AudioRecord.STATE_INITIALIZED) {
                sLastError = "AudioRecord failed to initialize";
                Log.w(TAG, sLastError);
                sendUnityEvent("startAudioRecord initFailed error='" + sLastError + "'");
                stopAudioRecordOnly();
                return;
            }

            sAudioRecord.startRecording();
            sRunning = true;
            sLastError = "";
            sLastReadResult = 0;
            sReadLogCountdown = 0;
            synchronized (sLock) {
                sSamplesWritten = 0;
            }
            sThread = new Thread(OpenBrushAudioPlaybackCapture::readLoop, "OpenBrushAudioPlaybackCapture");
            sThread.start();
            Log.i(TAG, "OpenBrushAudioPlaybackCapture AudioRecord started recordingState="
                    + sAudioRecord.getRecordingState());
            sendUnityEvent("startAudioRecord started recordingState=" + sAudioRecord.getRecordingState());
        } catch (Exception e) {
            sLastError = e.toString();
            Log.w(TAG, "AudioPlaybackCapture failed", e);
            sendUnityEvent("startAudioRecord exception error='" + sLastError + "'");
            stopAudioRecordOnly();
        }
    }

    private static void stopAudioRecordOnly() {
        Log.i(TAG, "OpenBrushAudioPlaybackCapture stopAudioRecordOnly running=" + sRunning
                + " hasAudioRecord=" + (sAudioRecord != null)
                + " samplesWritten=" + sSamplesWritten
                + " lastRead=" + sLastReadResult);
        sRunning = false;
        sRequestPending = false;
        if (sAudioRecord != null) {
            try {
                sAudioRecord.stop();
            } catch (IllegalStateException ignored) {
            }
            sAudioRecord.release();
            sAudioRecord = null;
        }
    }

    private static void registerProjectionCallback() {
        if (sProjection == null) {
            return;
        }
        sProjectionCallback = new MediaProjection.Callback() {
            @Override
            public void onStop() {
                Log.i(TAG, "OpenBrushAudioPlaybackCapture MediaProjection stopped");
                sendUnityEvent("projectionStopped samplesWritten=" + sSamplesWritten
                        + " lastRead=" + sLastReadResult);
                stopAudioRecordOnly();
                sProjection = null;
                sProjectionCallback = null;
            }
        };
        sProjection.registerCallback(sProjectionCallback, new Handler(Looper.getMainLooper()));
    }

    private static void readLoop() {
        short[] buffer = new short[1024 * CHANNEL_COUNT];
        Log.i(TAG, "OpenBrushAudioPlaybackCapture readLoop start");
        sendUnityEvent("readLoop start");
        while (sRunning && sAudioRecord != null) {
            int read = sAudioRecord.read(buffer, 0, buffer.length);
            sLastReadResult = read;
            if (read <= 0) {
                if (sReadLogCountdown <= 0) {
                    Log.w(TAG, "OpenBrushAudioPlaybackCapture readLoop read=" + read
                            + " recordingState=" + sAudioRecord.getRecordingState()
                            + " samplesWritten=" + sSamplesWritten);
                    sReadLogCountdown = READ_LOG_INTERVAL;
                } else {
                    --sReadLogCountdown;
                }
                continue;
            }

            synchronized (sLock) {
                for (int i = 0; i + 1 < read; i += CHANNEL_COUNT) {
                    float left = buffer[i] / 32768.0f;
                    float right = buffer[i + 1] / 32768.0f;
                    sRing[(int)(sSamplesWritten % RING_SAMPLES)] = (left + right) * 0.5f;
                    ++sSamplesWritten;
                }
            }
            if (sReadLogCountdown <= 0) {
                Log.i(TAG, "OpenBrushAudioPlaybackCapture readLoop read=" + read
                        + " samplesWritten=" + sSamplesWritten);
                sReadLogCountdown = READ_LOG_INTERVAL;
            } else {
                --sReadLogCountdown;
            }
        }
        Log.i(TAG, "OpenBrushAudioPlaybackCapture readLoop exit running=" + sRunning
                + " hasAudioRecord=" + (sAudioRecord != null)
                + " samplesWritten=" + sSamplesWritten
                + " lastRead=" + sLastReadResult);
        sendUnityEvent("readLoop exit running=" + sRunning
                + " hasAudioRecord=" + (sAudioRecord != null)
                + " samplesWritten=" + sSamplesWritten
                + " lastRead=" + sLastReadResult);
    }

    static void sendUnityEvent(String message) {
        final String eventMessage = message;
        Activity activity = sActivity;
        if (activity == null) {
            sendUnityEventNow(eventMessage);
            return;
        }
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                sendUnityEventNow(eventMessage);
            }
        });
    }

    private static void sendUnityEventNow(String message) {
        try {
            UnityPlayer.UnitySendMessage(UNITY_CALLBACK_OBJECT, UNITY_CALLBACK_METHOD, message);
        } catch (Exception e) {
            Log.w(TAG, "Unity callback failed message=" + message, e);
        }
    }
}
