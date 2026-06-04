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

public class OpenBrushAudioPlaybackCapture {
    private static final String TAG = "OpenBrushAudioPlayback";
    private static final int SAMPLE_RATE = 48000;
    private static final int CHANNEL_COUNT = 2;
    private static final int RING_SAMPLES = 8192;

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

    public static void initialize(Activity activity) {
        sActivity = activity;
    }

    public static boolean isSupported() {
        return Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q;
    }

    public static void requestCapture() {
        if (!isSupported()) {
            sLastError = "AudioPlaybackCapture requires Android 10/API 29";
            Log.w(TAG, sLastError);
            return;
        }
        if (sActivity == null) {
            sLastError = "Unity activity is not initialized";
            Log.w(TAG, sLastError);
            return;
        }
        if (isCapturing()) {
            return;
        }

        sRequestPending = true;
        Intent intent = new Intent(sActivity, OpenBrushAudioPlaybackCaptureActivity.class);
        sActivity.startActivity(intent);
    }

    static void onProjectionResult(int resultCode, Intent data) {
        sRequestPending = false;
        try {
            if (sActivity == null) {
                sLastError = "Unity activity is not initialized";
                Log.w(TAG, sLastError);
                return;
            }
            if (resultCode != Activity.RESULT_OK || data == null) {
                sLastError = "MediaProjection permission denied";
                Log.w(TAG, sLastError);
                return;
            }

            MediaProjectionManager manager =
                    (MediaProjectionManager)sActivity.getSystemService(Context.MEDIA_PROJECTION_SERVICE);
            sProjection = manager.getMediaProjection(resultCode, data);
            if (sProjection == null) {
                sLastError = "MediaProjection result did not create a projection";
                Log.w(TAG, sLastError);
                return;
            }
            registerProjectionCallback();
            startAudioRecord();
        } catch (Exception e) {
            sLastError = e.toString();
            Log.w(TAG, "MediaProjection result failed", e);
            stop();
        }
    }

    public static void stop() {
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

            sAudioRecord = new AudioRecord.Builder()
                    .setAudioFormat(format)
                    .setBufferSizeInBytes(bufferSize)
                    .setAudioPlaybackCaptureConfig(config)
                    .build();

            if (sAudioRecord.getState() != AudioRecord.STATE_INITIALIZED) {
                sLastError = "AudioRecord failed to initialize";
                Log.w(TAG, sLastError);
                stopAudioRecordOnly();
                return;
            }

            sAudioRecord.startRecording();
            sRunning = true;
            sLastError = "";
            synchronized (sLock) {
                sSamplesWritten = 0;
            }
            sThread = new Thread(OpenBrushAudioPlaybackCapture::readLoop, "OpenBrushAudioPlaybackCapture");
            sThread.start();
            Log.i(TAG, "AudioPlaybackCapture started");
        } catch (Exception e) {
            sLastError = e.toString();
            Log.w(TAG, "AudioPlaybackCapture failed", e);
            stopAudioRecordOnly();
        }
    }

    private static void stopAudioRecordOnly() {
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
                Log.i(TAG, "MediaProjection stopped");
                stopAudioRecordOnly();
                sProjection = null;
                sProjectionCallback = null;
            }
        };
        sProjection.registerCallback(sProjectionCallback, new Handler(Looper.getMainLooper()));
    }

    private static void readLoop() {
        short[] buffer = new short[1024 * CHANNEL_COUNT];
        while (sRunning && sAudioRecord != null) {
            int read = sAudioRecord.read(buffer, 0, buffer.length);
            if (read <= 0) {
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
        }
    }
}
