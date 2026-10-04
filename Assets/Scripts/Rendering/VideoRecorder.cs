// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#undef ENABLE_AUDIO_DEBUG

using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace TiltBrush
{

    /// A video recorder module for capturing and encoding a video stream.
    /// This class will:
    ///
    ///  * Start an encoder process (ffmpeg) and launch two threads to manage piped I/O.
    ///  * Use a background garbage collector to avoid hitches due to Unity memory allocation.
    ///  * Capture the camera target in endCameraRendering via AsyncGPUReadback.
    ///  * Send captured frames are to background input thread for processing.
    ///  * Report a log from the output thread when capture is complete.
    ///
    public class VideoRecorder : MonoBehaviour
    {
        private string m_filePath = "video_0.avi";

        private int m_width;
        private int m_height;

        private bool m_isPlayingBack = false;
        private bool m_isCapturing = false;
        private bool m_isSaving = false;
        private bool m_isCapturingAudio = false;

        private bool m_isPortrait = false;

        private Texture2D m_playbackTexture;

        private System.Diagnostics.Stopwatch m_frameTimer;

        private FfmpegPipe m_ffmpegVideoReader;
        private FfmpegPipe m_ffmpegVideo;
        private FfmpegPipe m_ffmpegAudio;

        private decimal m_FPS = 30.0m;

        int m_lastVideoFrame;
        int m_nextAudioFrame;

        private int m_playbackFrameCount = 0;
        private int m_playbackCurFrame = 0;

        private int m_videoFrameCount = 0;
        private int m_playbackLoops = 0;
        private int m_playbackNumLoops = 3;
        bool m_playbackRequested = false;

        StereoBuffer m_audioBuffer;

        // This value is incremented by 1 each time a frame is captured.
        private int m_audioFramesRequired;

        // The number of audio frames captured.
        int m_audioFrameCount = 0;

        // Frames reused during rendering to avoid generating garbage.
        Queue<Color32[]> m_videoFramePool = new Queue<Color32[]>();

        Queue<Color32[]> m_bufferedVideoFrames = new Queue<Color32[]>();
        const long kMaxQueueSizeBytes = 8L * 1024L * 1024L * 1024L;

        private bool m_forcedCaptureFramerate;

        // Readbacks of the camera target, oldest first.
        Queue<AsyncGPUReadbackRequest> m_pendingReadbacks = new Queue<AsyncGPUReadbackRequest>();
        // Post-processing (CameraConfig.PostEffects) is applied to the camera while capturing, as the
        // built-in pipeline's image effects (e.g. bloom) were applied before capture.
        UrpPostProcessingController.CameraPostProcessingState? m_capturePostProcessingState;

        class StereoBuffer
        {
            const int kSizeInSeconds = 4;

            // We keep secondary buffers around to avoid generating garbage / triggering collection.
            decimal m_FPS;
            float[] m_scratch;
            float[] m_stereo;
            int m_frameSamples = 0;
            int m_sampleRate;

            RingBuffer<float> m_leftChannel;
            RingBuffer<float> m_rightChannel;

            public StereoBuffer()
            {
                SetSampleRates(30.0m, 44100);
            }

            public void SetSampleRates(decimal fps, int hz)
            {
                if (m_FPS == fps && m_sampleRate == hz)
                {
                    return;
                }

                // If either fps or hz changed, we must reallocate these buffers.
                m_scratch = new float[(int)(hz / fps)];
                m_stereo = new float[(int)(hz / fps * 2)];

                // Here only hz matters.
                if (hz != m_sampleRate)
                {
                    m_leftChannel = new RingBuffer<float>(hz * kSizeInSeconds);
                    m_rightChannel = new RingBuffer<float>(hz * kSizeInSeconds);
                }

                m_FPS = fps;
                m_sampleRate = hz;
            }

            public bool IsFrameReady(int frame)
            {
                int numFrames = frame - GetCurFrame();
                if (numFrames < 0)
                {
                    return false;
                }

                numFrames = Mathf.Max(1, numFrames);

                return m_leftChannel.Count >= (m_scratch.Length * numFrames);
            }

            public void Clear()
            {
                m_frameSamples = 0;
                m_leftChannel.Clear();
                m_rightChannel.Clear();
            }

            public void Add(float left, float right)
            {
                if (m_leftChannel.IsFull)
                {
                    m_leftChannel.Dequeue(m_scratch.Length);
                    m_rightChannel.Dequeue(m_scratch.Length);
                    m_frameSamples += m_scratch.Length;
                }

                m_leftChannel.Enqueue(left);
                m_rightChannel.Enqueue(right);
            }

            int GetCurFrame()
            {
                // Warning: this code is careful to multiply before dividing, which is important to avoid
                // compounding floating point error which can lead to being off-by-epsilon when we should hit
                // an exact frame.
                return (int)(m_frameSamples * m_FPS) / m_sampleRate;
            }

            public FloatBytable PopFrame(int frame, int realTimeFrame, int videoFrame)
            {
#if ENABLE_AUDIO_DEBUG
        m_debugLog.Add(new DebugLogEntry {
                                            frame = frame,
                                            delta = frame - GetCurFrame(),
                                            m_frameSamples = m_frameSamples,
                                            realtimeFrame = realTimeFrame,
                                            videoFrame = videoFrame
                                          });
#endif
                // TODO: When the audio sample rate (Hz) is not an even multiple of the video sample
                // rate (FPS), audio samples can accumulate. For example, consider the case where the video
                // sample rate is 23.99 and the audio rate is 48kHz, the result number of audio samples per
                // video sample is ~2000.83368, which will round down to 2000, which result in one audio
                // sample being dropped on every video frame. While this will take a long time to accumulate,
                // about 49 seconds to lag one frame (at 30fps, 44.1kHz), it's worth fixing.
                //
                // Instead of attempting to submit one video frame worth of audio, we should convert the video
                // frame offset into a time line offset and then convert that into audio samples and finally
                // determine the difference between the number of audio samples submitted vs. offset at the
                // current video signal.
                //
                // This accumulation will manifest as drift in the audio sync.

                if (GetCurFrame() < frame - 1)
                {

                    // Audio interoplation:
                    //
                    // In the example below, frame 2 and frame 4 are interpolated over the duration of frame 4,
                    // completely ignoring frame 3. The values of frame 4 are overwritten with the interpolated
                    // values. If additional video frames were dropped after frame 3, they also would have no
                    // contribution.
                    //
                    //         frame 1  frame 2  frame 3  frame 4  frame 5
                    // Audio: [.......][.......][.......][.......][.......]
                    // Video: [.......] dropped  dropped [.......][.......]
                    // Interp windows: |.......|         |.......|
                    // oldOffset       ^
                    // curOffset                         ^
                    //
                    //
                    // oldOffset marks the start of the first interpolation window and curOffset marks the frame
                    // about to be submitted. We blend the frame starting at oldOffset into the current frame
                    // below.

                    int curOffset = (int)(((frame - 1) * m_sampleRate) / m_FPS) - m_frameSamples;
                    int oldOffset = 0;
                    int interp = m_scratch.Length;

                    for (int i = 0; i < interp; i++)
                    {
                        m_leftChannel[curOffset] = Mathf.Lerp(m_leftChannel[oldOffset],
                            m_leftChannel[curOffset],
                            i / (float)interp);
                        m_rightChannel[curOffset] = Mathf.Lerp(m_rightChannel[oldOffset],
                            m_rightChannel[curOffset],
                            i / (float)interp);
                        oldOffset++;
                        curOffset++;
                    }

                    while (GetCurFrame() < frame - 1)
                    {
                        m_leftChannel.Dequeue(m_scratch.Length);
                        m_rightChannel.Dequeue(m_scratch.Length);
                        m_frameSamples += m_scratch.Length;
                    }
                }

                // Longer term, we probably want to use a phase vocoder here instead to avoid audible pops.
                // Adding an envelope to the interpolation window may be a simpler way to reduce noise, though
                // pitch will still be effected.

                for (int i = 0; i < m_scratch.Length; i++)
                {
                    m_stereo[i * 2 + 0] = m_leftChannel[i];
                    m_stereo[i * 2 + 1] = m_rightChannel[i];
                }

                m_leftChannel.Dequeue(m_scratch.Length);
                m_rightChannel.Dequeue(m_scratch.Length);
                m_frameSamples += m_scratch.Length;

                return new FloatBytable(m_stereo);
            }

#if ENABLE_AUDIO_DEBUG
    struct DebugLogEntry {
      public int frame;
      public int delta;
      public int m_frameSamples;
      public int realtimeFrame;
      public int videoFrame;
    }
    List<DebugLogEntry> m_debugLog = new List<DebugLogEntry>();
    public void WriteLog() {
      List<DebugLogEntry> l = m_debugLog;
      m_debugLog = new List<DebugLogEntry>();
      StringWriter sr = new StringWriter();
      foreach (DebugLogEntry i in l) {
        sr.WriteLine("frame: {0} delta: {1} samples: {2} rt: {3} vid: {4}",
                      i.frame, i.delta, i.m_frameSamples, i.realtimeFrame,
                      i.videoFrame);
      }
      UnityEngine.Debug.Log(sr.ToString());
    }
#endif
        }

        // -------------------------------------------------------------------------------------------- //
        // Properties
        // -------------------------------------------------------------------------------------------- //

        // The target framerate (frames per second) of the captured video.
        public decimal FPS
        {
            get { return m_FPS; }
        }

        // The file path to which the video stream will be captured.
        public string FilePath
        {
            get { return m_filePath; }
            // Set is intentionally not implemented here to avoid error condition of changing the file name
            // during capture. Instead, the file name can only be set by calling BeginCapture();
        }

        // The number of times to loop the video during playback.
        public int PlaybackNumLoops
        {
            get { return m_playbackNumLoops; }
            set { m_playbackNumLoops = value; }
        }

        public bool IsPlayingBack
        {
            get { return m_isPlayingBack; }
        }

        public bool IsSaving
        {
            get { return !m_isCapturing && m_isSaving; }
        }

        // The current capture state, indicates if the encoder is currently waiting for input.
        public bool IsCapturing
        {
            get { return m_isCapturing; }
        }

        public bool IsCapturingAudio
        {
            get { return m_isCapturingAudio; }
        }

        public bool IsPortrait
        {
            get { return m_isPortrait; }
            set { m_isPortrait = value; }
        }

        // The number of frames captured so far.
        public int FrameCount
        {
            get
            {
                return m_videoFrameCount;
            }
        }

        // The number of frames the video has lagged behind real-time.
        public int VideoDelayFrameCount
        {
            get
            {
                return (int)(RealTimeFrameCount - m_videoFrameCount);
            }
        }

        public int RealTimeFrameCount
        {
            get
            {
                if (m_forcedCaptureFramerate)
                {
                    return m_videoFrameCount;
                }
                else
                {
                    return (int)(m_frameTimer.ElapsedMilliseconds / 1000.0m * m_FPS);
                }
            }
        }

        public int PlaybackFrameCount
        {
            get { return m_playbackCurFrame; }
        }

        public float PlaybackPercent
        {
            get
            {
                if (m_playbackFrameCount == 0)
                {
                    return 1.0f;
                }
                return m_playbackCurFrame / (float)m_playbackFrameCount;
            }
        }

        // -------------------------------------------------------------------------------------------- //
        // Private startup and shutdown
        // -------------------------------------------------------------------------------------------- //

        private void OnEnable()
        {
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        }

        // Start background threads.
        private void Start()
        {
            m_ffmpegVideoReader = new FfmpegPipe();
            m_ffmpegVideo = new FfmpegPipe();
            m_ffmpegAudio = new FfmpegPipe();
            m_audioBuffer = new StereoBuffer(); // SetFps is called below.

            // we use this to ensure StopCapture() is called on exit, even if this behavior is disabled.
            App.Instance.AppExit += OnGuaranteedAppQuit;
        }

        // Ensure background threads shutdown cleanly.
        private void OnGuaranteedAppQuit()
        {
            if (m_isCapturing)
            {
                StopCapture(save: true);
            }
            else if (m_isPlayingBack)
            {
                m_isPlayingBack = false;
                m_ffmpegVideoReader.Stop();
            }
        }

        // -------------------------------------------------------------------------------------------- //
        // Public API
        // -------------------------------------------------------------------------------------------- //

        // Launch the encoder targeting the given file path.
        // Return true on success, false if capture could not start.
        public bool StartCapture(string filePath, int audioSampleRate, bool captureAudio, bool blocking,
                                 float fps)
        {
            if (m_isCapturing)
            {
                return true;
            }
            if (m_isPlayingBack)
            {
                m_isPlayingBack = false;
                m_ffmpegVideoReader.Stop();
            }

            m_FPS = (decimal)fps;

            m_audioBuffer.SetSampleRates(m_FPS, audioSampleRate);

            m_nextAudioFrame = 1;
            m_lastVideoFrame = -1;
            m_audioFrameCount = 0;
            m_audioFramesRequired = 0;
            m_audioBuffer.Clear();
            m_isCapturingAudio = captureAudio;

            Camera cam = GetComponent<Camera>();

            m_filePath = filePath;

            m_videoFrameCount = 0;
            m_bufferedVideoFrames.Clear();

            int width = cam.pixelWidth;
            int height = cam.pixelHeight;

            if (cam.pixelHeight == 0)
            {
                width = Screen.width;
                height = Screen.height;
            }

            const string kPipeStdIn = @"pipe:0";

            // We need to "touch" the destination file immediately, to avoid overlapping encoder instances
            // from stomping each other.
            FileStream myFileStream = File.Open(m_filePath, FileMode.OpenOrCreate,
                FileAccess.Write, FileShare.ReadWrite);
            myFileStream.Close();
            myFileStream.Dispose();
            File.SetLastWriteTimeUtc(m_filePath, System.DateTime.UtcNow);

            string videoFileName = audioSampleRate > 0
                ? m_filePath + ".tmp." + App.UserConfig.Video.ContainerType
                : m_filePath;

            if (!m_ffmpegVideo.Start(kPipeStdIn, videoFileName, width, height, (float)m_FPS, blocking))
            {
                return false;
            }

            m_ffmpegAudio.OutputFile = "";
            if (m_isCapturingAudio
                && !m_ffmpegAudio.Start(kPipeStdIn, m_filePath + ".tmp.m4a",
                    width, height, audioSampleRate, blocking))
            {
                m_ffmpegVideo.Stop();
                return false;
            }

            // Give the encoder a means to return used frames
            m_ffmpegVideo.ReleaseFrame += ReturnFrameToPool;

            m_playbackTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);

            //
            // Finalize local state setup.
            //
            m_width = width;
            m_height = height;

            m_frameTimer = new Stopwatch();
            m_frameTimer.Start();

            if (UrpPostProcessingController.Instance != null)
            {
                m_capturePostProcessingState =
                    UrpPostProcessingController.Instance.BeginCapturePostProcessing(
                        cam, CameraConfig.PostEffects);
            }

            // Since audio capture is asynchronous, these *must* be set as the last step.
            m_isCapturing = true;

            return true;
        }

        // Stop the encoder and optionally save the captured stream.
        // When save is false, the stream is discarded.
        public void StopCapture(bool save)
        {
            if (m_isPlayingBack)
            {
                m_isPlayingBack = false;
                m_ffmpegVideoReader.Stop();
                m_ffmpegVideoReader = null;
                UnityEngine.Debug.LogWarning("Stop Video reader");
            }
            SetCaptureFramerate(0);
            if (!m_isCapturing)
            {
                return;
            }

#if ENABLE_AUDIO_DEBUG
    m_audioBuffer.WriteLog();
#endif

            // Collect frames still in flight on the GPU so the end of the video isn't truncated.
            ProcessReadbacks(waitForCompletion: true);

            m_videoFramePool.Clear();
            m_ffmpegVideo.ReleaseFrame -= ReturnFrameToPool;

            // Grab local references of the FFMPEG pipes, which is required to get them to ref-capture
            // correctly in the lambdas below (we want to capture a reference to the object, not a
            // reference to this classes members).
            FfmpegPipe videoPipe = m_ffmpegVideo;
            FfmpegPipe audioPipe = m_ffmpegAudio;
            bool draining = false;

            if (save && m_bufferedVideoFrames.Count > 0)
            {
                // Transfer the buffer to the encoder thread to finish processing.
                // Note the local captures, which allow release of the classes references.
                Queue<Color32[]> buffer = m_bufferedVideoFrames;
                System.Threading.Thread t = new System.Threading.Thread(
                    () => DrainStop(m_filePath, buffer, videoPipe, audioPipe));
                t.IsBackground = true;
                m_bufferedVideoFrames = new Queue<Color32[]>();
                m_ffmpegVideo = new FfmpegPipe();
                m_ffmpegAudio = new FfmpegPipe();
                try
                {
                    t.Start();
                    m_isSaving = true;
                }
                catch
                {
                    m_isSaving = false;
                    throw;
                }
                draining = true;
            }
            else
            {
                // Request background threads to stop.
                m_ffmpegVideo.Stop();
                m_ffmpegAudio.Stop();
                draining = false;
                m_playbackFrameCount = FrameCount;
            }

            // Clear the Stopwatch
            m_frameTimer.Reset();

            Texture2D.Destroy(m_playbackTexture);
            m_playbackTexture = null;

            if (!save)
            {
                // We need to trash the file on a background thread because we need to wait for the ffmpeg
                // process to fully exit before touching the file.
                //
                // TODO: This should be a task executed in a thread pool, using a thread is overkill.
                string filePath = m_filePath;
                System.Threading.Thread t = new System.Threading.Thread(
                    () => RemoveFile(filePath, videoPipe, audioPipe));
                m_ffmpegVideo = new FfmpegPipe();
                m_ffmpegAudio = new FfmpegPipe();
                t.IsBackground = true;
                t.Start();
            }
            else if (!draining)
            {
                // Only do this if drainstop is not already running, in that case, DrainStop() is responsible
                // for joining the files after they are complete.
                string filePath = m_filePath;
                System.Threading.Thread t = new System.Threading.Thread(
                    () => JoinFiles(filePath, videoPipe, audioPipe));
                m_ffmpegVideo = new FfmpegPipe();
                m_ffmpegAudio = new FfmpegPipe();
                t.IsBackground = true;
                try
                {
                    t.Start();
                    m_isSaving = true;
                }
                catch
                {
                    m_isSaving = false;
                    throw;
                }
            }

            if (m_capturePostProcessingState.HasValue)
            {
                if (UrpPostProcessingController.Instance != null)
                {
                    UrpPostProcessingController.Instance.EndCapturePostProcessing(
                        m_capturePostProcessingState.Value);
                }
                m_capturePostProcessingState = null;
            }

            m_isCapturing = false;
            m_isCapturingAudio = false;
        }

        private void DrainStop(string filePath,
                               Queue<Color32[]> buffer,
                               FfmpegPipe videoPipe,
                               FfmpegPipe audioPipe)
        {
            const int kTimeoutMs = 60 * 1000;
            try
            {
                Stopwatch timer = new Stopwatch();
                System.Console.WriteLine("VideoRecorder: DrainStop, frames buffered: {0}", buffer.Count);

                timer.Start();
                while (buffer.Count > 0)
                {
                    if (timer.ElapsedMilliseconds > kTimeoutMs)
                    {
                        break;
                    }
                    if (videoPipe.IsReadyForInput)
                    {
                        Color32Bytable c = new Color32Bytable(buffer.Dequeue());
                        videoPipe.QueueFrame(c);
                    }
                }

                m_playbackFrameCount = FrameCount;

                System.Console.WriteLine("VideoRecorder: DrainStop exit");
                videoPipe.Stop();
                audioPipe.Stop();

                JoinFiles(filePath, videoPipe, audioPipe);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }

        private void RemoveFile(string filePath, FfmpegPipe videoPipe, FfmpegPipe audioPipe)
        {
            try
            {
                videoPipe.WaitForEncoderExit( /*ms*/20 * 1000);
                System.IO.File.Delete(videoPipe.OutputFile);

                if (audioPipe.OutputFile.Length > 0)
                {
                    audioPipe.WaitForEncoderExit( /*ms*/20 * 1000);
                    System.IO.File.Delete(audioPipe.OutputFile);
                    // Remove the primary file last, to avoid file collisions with the next recording.
                    System.IO.File.Delete(filePath);
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogException(e);
                m_isSaving = false;
            }
        }

        private void JoinFiles(string filePath, FfmpegPipe videoPipe, FfmpegPipe audioPipe)
        {
            m_isSaving = true;
            try
            {
                videoPipe.WaitForEncoderExit( /*ms*/20 * 1000);

                if (audioPipe.OutputFile.Length > 0)
                {
                    audioPipe.WaitForEncoderExit( /*ms*/20 * 1000);
                    if (FfmpegPipe.Mux(audioPipe.OutputFile, videoPipe.OutputFile, filePath))
                    {
                        System.IO.File.Delete(videoPipe.OutputFile);
                        System.IO.File.Delete(audioPipe.OutputFile);
                    }
                }

                m_playbackLoops = 0;
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
            finally
            {
                m_isSaving = false;
            }
        }

        public void StopPlayback()
        {
            if (!IsPlayingBack) { return; }
            if (m_ffmpegVideoReader == null || m_ffmpegVideoReader.DidExit) { return; }
            m_isPlayingBack = false;
            m_ffmpegVideoReader.Stop();
        }

        private void StartPlaybackReader()
        {
            const string kPipeStdOut = @"pipe:1";
            m_playbackCurFrame = 0;

            if (m_playbackLoops >= m_playbackNumLoops)
            {
                m_isPlayingBack = false;
                return;
            }

            m_playbackLoops++;

            if (m_ffmpegVideoReader.Start(m_filePath, kPipeStdOut, m_width, m_height, (float)m_FPS, false))
            {
                m_isPlayingBack = true;
                m_frameTimer.Reset();
                m_frameTimer.Start();
            }
            else
            {
                m_isPlayingBack = false;
            }
        }


        // -------------------------------------------------------------------------------------------- //
        // Frame Capture
        // -------------------------------------------------------------------------------------------- //
        // Frames are requested with AsyncGPUReadback at the end of camera rendering and collected in
        // Update once the GPU has finished, so capture doesn't stall the GPU.

        public bool ShouldCapture()
        {
            // If the last frame captured is out of sync with the real-time clock, it's time to capture a
            // frame. It's important that video capture run at a fixed framerate, due to the fact that audio
            // is attempting to sync with it. It's acceptable to drop frames, however running faster than
            // rate will result in audio being unable to catch up with the video signal, unless a variable
            // framerate is introduced in the audio processing logic.
            bool ret = RealTimeFrameCount != m_lastVideoFrame;
            return ret || m_forcedCaptureFramerate;
        }

        public void ProcessAudio(float left, float right)
        {
            if (!m_isCapturing || !m_isCapturingAudio)
            {
                return;
            }

            try
            {
                // Audio may not be required yet, but buffer the value to avoid dropping samples.
                m_audioBuffer.Add(left, right);
                CaptureAudio();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }

        private void CaptureAudio()
        {
            try
            {
                if (m_audioFrameCount < m_videoFrameCount)
                {
                    m_audioFramesRequired++;
                    m_audioFrameCount++;
                }

                if (m_audioFramesRequired == 0)
                {
                    return;
                }

                // We grab the previous frame of audio, since the video takes a frame to capture.
                int curFrame = m_nextAudioFrame;

                if (RealTimeFrameCount - m_nextAudioFrame > 4)
                {
                    m_nextAudioFrame = RealTimeFrameCount - 1;
                }

                if (m_audioBuffer.IsFrameReady(curFrame))
                {
                    m_ffmpegAudio.QueueFrame(m_audioBuffer.PopFrame(curFrame, RealTimeFrameCount, FrameCount));
                    m_audioFramesRequired--;
                    m_nextAudioFrame++;
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }

        // Start playback as soon as possible, but will wait for any existing files currently in flight to
        // finish recording first.
        public void RequestPlayback()
        {
            m_playbackRequested = true;
        }

        public void SetCaptureFramerate(int framerate)
        {
            m_forcedCaptureFramerate = framerate != 0;
            Time.captureFramerate = framerate;
        }

        void Update()
        {
            ProcessReadbacks(waitForCompletion: false);
            DrainBufferedFrame();

            if (m_playbackRequested && !m_isSaving && !m_isCapturing)
            {
                StartPlaybackReader();
                m_playbackRequested = false;
            }
        }

        // Even when no frame was captured this update, there may be buffered frames to push to the
        // encoder. This lets the buffer drain between captured frames, assuming the encoder can keep up.
        void DrainBufferedFrame()
        {
            if (m_ffmpegVideo.IsReadyForInput && m_bufferedVideoFrames.Count > 0)
            {
                Color32Bytable c = new Color32Bytable(m_bufferedVideoFrames.Dequeue());
                m_ffmpegVideo.QueueFrame(c);
            }
        }

        // Buffer a captured frame and, if the encoder is ready, pass the oldest buffered frame along.
        // The m_bufferedVideoFrames queue is intentionally managed on the main thread, which implies a
        // single background worker and avoids the need for synchronization primitives.
        private void EnqueueFrame(Color32[] frame)
        {
            long kPixelSizeBytes = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Color32));
            long usedBufferBytes = frame.Length * kPixelSizeBytes * m_bufferedVideoFrames.Count;

            if (usedBufferBytes < kMaxQueueSizeBytes)
            {
                m_bufferedVideoFrames.Enqueue(frame);
            }
            else
            {
                System.Console.WriteLine("Dropped frame [{0}], buffer overflow", m_videoFrameCount);
            }

            if (m_ffmpegVideo.IsReadyForInput && m_bufferedVideoFrames.Count > 0)
            {
                Color32Bytable c = new Color32Bytable(m_bufferedVideoFrames.Dequeue());
                m_ffmpegVideo.QueueFrame(c);
            }
        }

        // Allocate a frame buffer, reusing one the encoder has finished with if possible.
        private Color32[] GetPooledFrame(int length)
        {
            lock (m_videoFramePool)
            {
                while (m_videoFramePool.Count > 0)
                {
                    Color32[] frame = m_videoFramePool.Dequeue();
                    if (frame.Length == length)
                    {
                        return frame;
                    }
                }
            }
            return new Color32[length];
        }

        // Move completed GPU readbacks into the encoder queue, in capture order.
        private void ProcessReadbacks(bool waitForCompletion)
        {
            while (m_pendingReadbacks.Count > 0)
            {
                AsyncGPUReadbackRequest request = m_pendingReadbacks.Peek();
                if (waitForCompletion)
                {
                    request.WaitForCompletion();
                }
                else if (!request.done)
                {
                    return;
                }
                m_pendingReadbacks.Dequeue();

                if (!m_isCapturing)
                {
                    continue;
                }
                if (request.hasError)
                {
                    UnityEngine.Debug.LogWarning("VideoRecorder: GPU readback failed, frame dropped.");
                    continue;
                }

                NativeArray<Color32> data = request.GetData<Color32>();
                if (data.Length != m_width * m_height)
                {
                    UnityEngine.Debug.LogWarning(
                        $"VideoRecorder: frame size {request.width}x{request.height} does not match " +
                        $"{m_width}x{m_height}, frame dropped.");
                    continue;
                }

                Color32[] frame = GetPooledFrame(data.Length);
                data.CopyTo(frame);
                EnqueueFrame(frame);
            }
        }

        // -------------------------------------------------------------------------------------------- //
        // Render Hook
        // -------------------------------------------------------------------------------------------- //

        // The camera renders into its target texture, which we read back for capture or overwrite with
        // the playback preview.
        void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera.gameObject != gameObject)
            {
                return;
            }
            RenderTexture target = camera.targetTexture;
            if (target == null)
            {
                return;
            }

            // Loop playback. We intentionally don't buffer the entire video, which means we have to run
            // FFMPEG in a loop until we're done previewing.
            if (m_isPlayingBack && m_ffmpegVideoReader.DidExit)
            {
                StartPlaybackReader();
            }

            if (m_isPlayingBack)
            {
                BlitPlaybackFrame(target);
                return;
            }
            if (!m_isCapturing)
            {
                return;
            }

            bool doCapture = ShouldCapture();
            m_lastVideoFrame = RealTimeFrameCount;
            if (doCapture)
            {
                m_pendingReadbacks.Enqueue(AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32));
                // The captured frame count increases regardless of buffering or queuing.
                m_videoFrameCount++;
            }
        }

        private void BlitPlaybackFrame(RenderTexture destination)
        {
            Color32Bytable b = null;

            if (m_playbackCurFrame >= RealTimeFrameCount)
            {
                return;
            }

            m_ffmpegVideoReader.GetFrame(ref b);

            if (b == null)
            {
                return;
            }

            Color32[] c = b.GetArray() as Color32[];
            if (c == null || c.Length == 0)
            {
                // Should never happen.
                UnityEngine.Debug.LogWarning("No data.");
                return;
            }

            RenderTexture trg = destination;
            if (!m_playbackTexture
                || m_playbackTexture.width != trg.width
                || m_playbackTexture.height != trg.height)
            {
                m_playbackTexture = new Texture2D(trg.width, trg.height, TextureFormat.ARGB32, false);
            }

            m_playbackCurFrame++;
            m_playbackTexture.SetPixels32(c);
            m_playbackTexture.Apply();

            Graphics.Blit(m_playbackTexture, destination);
        }

        // Return a no-longer-used frame to the pool
        private void ReturnFrameToPool(Bytable array)
        {
            lock (m_videoFramePool)
            {
                m_videoFramePool.Enqueue((Color32[])array.GetArray());
            }
        }
    }

} // namespace TiltBrush
