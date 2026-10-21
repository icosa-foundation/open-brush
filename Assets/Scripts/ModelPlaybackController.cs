// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.
using System;
using System.Collections;
using System.Linq;
using ImmPlayer;
using UnityEngine;

namespace TiltBrush
{
    [MoonSharp.Interpreter.MoonSharpUserData]
    public sealed class ModelViewpointInfo
    {
        public int id;
        public string name;
        public bool available;
        public bool active;
    }

    /// <summary>Per-widget playback and deferred restoration, independent of any panel.</summary>
    [DefaultExecutionOrder(110)]
    public sealed class ModelPlaybackController : MonoBehaviour
    {
        private const double ImmTicksPerSecond = 12600;
        private NativeImmReference m_Native;
        private PlayGltfAnimationClip m_Gltf;
        private AudioSource[] m_Audio = new AudioSource[0];
        private float[] m_AudioGains = new float[0];
        private float m_Volume = 1;
        private float? m_SpatialBlend;
        private bool m_Paused;
        private bool m_PlayingChangePending;
        private bool m_VolumeSet;
        private ModelPlaybackState m_InitialState;
        private bool m_RestoreFailed;
        private bool m_RestartAtChapter;
        private Coroutine m_Operation;
        private int[] m_ViewpointIds = new int[0];
        private int m_SelectedViewpointId = -1;
        public string LastError { get; private set; }
        public ImmDocument Document => m_Native != null ? m_Native.Document : null;
        public bool Initialized => Document != null || m_Gltf != null && m_Gltf.Initialized || m_Audio.Any(source => source != null && source.clip != null);
        public bool Busy => m_Operation != null;
        public bool IsRestoring => Busy || m_InitialState != null && Initialized && !m_RestoreFailed;
        public bool HasAudio => Document != null ? Document.HasAudio() : m_Audio.Length > 0;
        public bool CanSpatializeAudio => Document == null && m_Audio.Any(source => source != null);
        public float SpatialBlend => m_InitialState?.SpatialBlend ?? m_SpatialBlend ??
            (m_Audio.Any(source => source != null && source.spatialBlend > 0) ? 1f : 0f);
        public bool Playing => Document != null ? Document.GetStateInfo().Playback == ImmDocument.PlaybackState.Playing :
            m_Gltf != null && m_Gltf.Initialized ? m_Gltf.Playing : m_Audio.Any(source => source != null && source.isPlaying);
        public double Time => Document != null ? GetNativeTime().Time : m_Gltf != null && m_Gltf.Initialized ? m_Gltf.Time :
            m_Audio.FirstOrDefault(source => source != null && source.clip != null)?.time ?? 0;
        public double TargetTime => m_InitialState?.Time ?? Time;
        public double Length => Document != null ? (Document.TryGetChapterInfo(out var info) ? info.Lengths.Sum() / ImmTicksPerSecond : 0) :
            Math.Max(m_Gltf != null ? m_Gltf.Length : 0, m_Audio.Select(source => source != null && source.clip != null ? (double)source.clip.length : 0).DefaultIfEmpty().Max());
        public int ChapterCount => Document != null ? Document.GetChapterCount() : 0;
        public int Chapter => Document != null ? Document.GetCurrentChapter() : -1;
        public string[] Clips => m_Gltf != null ? m_Gltf.ClipNames : new string[0];
        public string ClipName => m_InitialState?.Clip ?? (m_Gltf != null ? m_Gltf.ClipName : null);
        public bool CanLoop => m_Gltf != null && m_Gltf.Initialized;
        public bool Loop => CanLoop && m_Gltf.Loop;
        public float Volume => m_InitialState?.Volume ?? (Document != null && !m_VolumeSet ? Document.GetVolume() : m_Volume);

        public void Bind(Transform root)
        {
            m_Native = root.GetComponent<NativeImmReference>();
            m_Gltf = root.GetComponentInChildren<PlayGltfAnimationClip>(true);
            m_Audio = root.GetComponentsInChildren<AudioSource>(true);
            m_AudioGains = m_Audio.Select(source => source.GetComponent<GltfAudioSource>()?.Gain ?? source.volume).ToArray();
        }

        public ModelPlaybackState GetSaveState()
        {
            if (m_InitialState != null) return m_InitialState.Copy();
            if (!Initialized) return null;
            var state = Document != null ? GetNativeTime() : new ModelPlaybackState { Time = Time };
            state.Paused = m_PlayingChangePending ? m_Paused : !Playing;
            state.Volume = Volume;
            state.SpatialBlend = m_SpatialBlend;
            if (Document != null) state.Chapter = Chapter;
            if (m_Gltf != null && m_Gltf.Initialized) { state.Clip = m_Gltf.ClipName; state.Loop = m_Gltf.Loop; }
            return state;
        }
        private ModelPlaybackState GetNativeTime()
        {
            Document.GetTime(out long start, out long stop);
            return new ModelPlaybackState { Time = start / ImmTicksPerSecond, StopTime = stop / ImmTicksPerSecond };
        }
        public void Restore(ModelPlaybackState state, bool restorePlayingPosition = false)
        {
            if (state == null) return;
            ValidateTime(state.Time);
            if (state.StopTime.HasValue) ValidateTime(state.StopTime.Value);
            ValidateVolume(state.Volume);
            if (state.SpatialBlend.HasValue) ValidateSpatialBlend(state.SpatialBlend.Value);
            m_InitialState = state.ForRestore(restorePlayingPosition);
            m_RestartAtChapter = !state.Paused && !restorePlayingPosition && state.Chapter.HasValue;
            m_RestoreFailed = false;
            LastError = null;
        }
        private void LateUpdate()
        {
            if (m_PlayingChangePending && (m_Paused ? !Playing : Playing))
                m_PlayingChangePending = false;
            if (m_InitialState != null && Initialized && !Busy && !m_RestoreFailed)
            {
                var state = m_InitialState;
                if (Document != null) StartNative(document => RestoreNative(document, state));
                else
                {
                    try
                    {
                        if (state.Clip != null) m_Gltf?.SelectClip(state.Clip);
                        if (state.Loop.HasValue && m_Gltf != null) m_Gltf.Loop = state.Loop.Value;
                        SetVolumeInternal(state.Volume);
                        m_SpatialBlend = state.SpatialBlend;
                        SeekInternal(state.Time);
                        SetPlayingInternal(!state.Paused);
                        m_InitialState = null;
                    }
                    catch (Exception error) { ReportError(error); }
                }
            }
            for (int i = 0; i < m_Audio.Length; i++)
            {
                if (m_Audio[i] == null) continue;
                m_Audio[i].volume = m_AudioGains[i] * m_Volume;
                m_Audio[i].mute = m_Volume <= 0.005f;
                if (m_SpatialBlend.HasValue) m_Audio[i].spatialBlend = m_SpatialBlend.Value;
                if (m_Paused && m_Audio[i].isPlaying) m_Audio[i].Pause();
            }
        }
        private void OnDisable()
        {
            if (m_InitialState == null && Initialized) m_InitialState = GetSaveState();
            StopAllCoroutines();
            m_Operation = null;
        }
        internal void PrepareNativeForRestore(ImmDocument document)
        {
            // Keep asynchronously restored references silent until their saved pose/time is ready.
            if (m_InitialState != null) document.SetVolume(0);
        }
        public void Continue()
        {
            RequireReady();
            if (Document == null)
            {
                SetPlayingInternal(true);
                MarkPlaybackChanged();
                return;
            }
            m_InitialState = GetSaveState();
            m_InitialState.Paused = false;
            StartNative(ContinueNative);
            MarkPlaybackChanged();
        }
        public void Play()
        {
            RequireReady();
            if (Document != null && Document.GetStateInfo().Playback != ImmDocument.PlaybackState.Finished) Continue();
            else
            {
                SetPlayingInternal(true);
                MarkPlaybackChanged();
            }
        }
        public void Pause() { RequireReady(); SetPlayingInternal(false); MarkPlaybackChanged(); }
        private void SetPlayingInternal(bool playing)
        {
            m_Paused = !playing;
            if (Document != null)
            {
                m_PlayingChangePending = true;
                if (!playing) Document.PauseAt(0);
                else if (Document.GetStateInfo().Playback == ImmDocument.PlaybackState.Waiting) Document.Continue();
                else if (Document.GetStateInfo().Playback == ImmDocument.PlaybackState.Finished) Document.Restart();
                else ResumeNative(Document);
            }
            else
            {
                if (m_Gltf != null && m_Gltf.Initialized) m_Gltf.Playing = playing;
                foreach (var source in m_Audio)
                {
                    if (source == null || source.clip == null) continue;
                    if (!playing) source.Pause();
                    else { source.UnPause(); if (!source.isPlaying) source.Play(); }
                }
            }
        }
        public void SetVolume(float volume)
        {
            RequireReady(); ValidateVolume(volume); SetVolumeInternal(volume); MarkPlaybackChanged();
        }
        public void SetSpatialBlend(float spatialBlend)
        {
            RequireReady();
            if (!CanSpatializeAudio) throw new InvalidOperationException("Model has no controllable spatial audio");
            ValidateSpatialBlend(spatialBlend);
            m_SpatialBlend = spatialBlend;
            foreach (var source in m_Audio)
                if (source != null) source.spatialBlend = spatialBlend;
            MarkPlaybackChanged();
        }
        private static void ValidateSpatialBlend(float spatialBlend)
        {
            if (float.IsNaN(spatialBlend) || float.IsInfinity(spatialBlend) || spatialBlend < 0 || spatialBlend > 1)
                throw new ArgumentOutOfRangeException(nameof(spatialBlend));
        }
        private void SetVolumeInternal(float volume)
        {
            m_Volume = volume <= 0.005f ? 0 : volume;
            m_VolumeSet = true;
            if (Document != null) Document.SetVolume(m_Volume);
        }
        public void Seek(double time)
        {
            RequireReady(); ValidateTime(time);
            if (time > Length) throw new ArgumentOutOfRangeException(nameof(time));
            if (Document != null)
            {
                m_InitialState = GetSaveState();
                m_InitialState.Time = time;
                m_InitialState.StopTime = 0;
                StartNative(document => WaitForEdit(document,
                    () => document.RequestSetTime(ToTicks(time), 0), m_InitialState.Paused));
            }
            else SeekInternal(time);
            MarkPlaybackChanged();
        }
        private void SeekInternal(double time)
        {
            if (m_Gltf != null && m_Gltf.Initialized) m_Gltf.Time = Math.Min(time, m_Gltf.Length);
            foreach (var source in m_Audio)
                if (source != null && source.clip != null)
                    source.time = (float)Math.Min(time, Math.Max(0, source.clip.length - 1.0 / source.clip.frequency));
        }
        public void SelectClip(string clip)
        {
            RequireReady();
            if (m_Gltf == null) throw new InvalidOperationException("This model has no animation clips");
            bool playing = Playing;
            m_Gltf.SelectClip(clip);
            SetPlayingInternal(playing);
            MarkPlaybackChanged();
        }
        public void SetLoop(bool loop)
        {
            RequireReady();
            if (m_Gltf == null) throw new InvalidOperationException("Loop control is supported for glTF animation clips");
            m_Gltf.Loop = loop;
            MarkPlaybackChanged();
        }
        public void SetChapter(int chapter)
        {
            RequireReady();
            if (Document == null || chapter < 0 || chapter >= ChapterCount) throw new ArgumentOutOfRangeException(nameof(chapter));
            m_InitialState = GetSaveState();
            m_InitialState.Chapter = chapter;
            m_InitialState.Time = GetChapterStartTime(Document, chapter);
            m_InitialState.StopTime = 0;
            StartNative(document => WaitForEdit(document, () => document.RequestSetChapter(chapter), m_InitialState.Paused, m_InitialState.Time));
            MarkPlaybackChanged();
        }

        private static void MarkPlaybackChanged()
        {
            SketchMemoryScript.m_Instance.MarkUnrecordedChange();
        }
        public void GetViewpointNavigationState(out int index, out int count)
        {
            index = -1;
            count = 0;
            if (Document == null) return;
            var snapshot = ReadSnapshot(Document);
            count = snapshot.availableCount;
            index = Array.IndexOf(m_ViewpointIds, m_SelectedViewpointId, 0, count);
        }

        public void CycleViewpoint(int direction)
        {
            RequireReady();
            if (Document == null) throw new InvalidOperationException("This model has no IMM viewpoints");
            var snapshot = ReadSnapshot(Document);
            int count = snapshot.availableCount;
            if (count == 0) return;
            int index = Array.IndexOf(m_ViewpointIds, m_SelectedViewpointId, 0, count);
            int next = index < 0 ? (direction > 0 ? 0 : count - 1) : (index + direction + count) % count;
            m_SelectedViewpointId = m_ViewpointIds[next];
        }

        public void SelectViewpoint(int id)
        {
            RequireReady();
            if (Document == null) throw new InvalidOperationException("This model has no IMM viewpoints");
            var snapshot = ReadSnapshot(Document);
            if (Array.IndexOf(m_ViewpointIds, id, 0, snapshot.availableCount) < 0)
                throw new InvalidOperationException("Viewpoint is unavailable");
            m_SelectedViewpointId = id;
        }

        public void NavigateToSelectedViewpoint() => NavigateToViewpoint(m_SelectedViewpointId);

        public ModelViewpointInfo[] GetViewpoints()
        {
            RequireReady();
            if (Document == null) throw new InvalidOperationException("This model has no IMM viewpoints");
            var snapshot = ReadSnapshot(Document);
            return Document.GetSpawnAreas().Select(area => new ModelViewpointInfo
            {
                id = area.Id, name = area.Name,
                available = Array.IndexOf(m_ViewpointIds, area.Id, 0, snapshot.availableCount) >= 0,
                active = m_SelectedViewpointId == area.Id
            }).ToArray();
        }
        public void NavigateToViewpoint(int id)
        {
            RequireReady();
            if (Document == null) throw new InvalidOperationException("This model has no IMM viewpoints");
            var snapshot = ReadSnapshot(Document);
            if (!TryNavigateToViewpoint(id, snapshot))
                throw new InvalidOperationException("Viewpoint is unavailable or evaluated state changed");
            m_SelectedViewpointId = id;
        }
        private bool TryNavigateToViewpoint(int id, SpawnAreaSnapshot snapshot)
        {
            if (Array.IndexOf(m_ViewpointIds, id, 0, snapshot.availableCount) < 0) return false;
            var widget = GetComponent<ModelWidget>();
            if (widget == null) throw new InvalidOperationException("This viewpoint has no model instance");
            if (widget.ShouldPreserveCustomSizePublic())
                throw new InvalidOperationException("This model's size is locked");
            if (!Document.TrySetActiveSpawnAreaId(id, out var pose, snapshot.evaluationRevision)) return false;
            var root = m_Native.transform;
            var flip = Matrix4x4.Scale(new Vector3(1, 1, -1));
            var converted = flip * Matrix4x4.TRS(pose.GetPosition(), pose.GetRotation(), Vector3.one * pose.sca) * flip;
            Vector3 position = root.TransformPoint(converted.GetColumn(3));
            Quaternion rotation = root.rotation * Quaternion.LookRotation(converted.GetColumn(2), converted.GetColumn(1));
            float scale = Mathf.Abs(root.lossyScale.x * pose.sca);
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale < 1e-6f)
                throw new InvalidOperationException("Viewpoint has no usable world scale");
            float scaleFactor = App.METERS_TO_UNITS / scale;
            var modelPose = ScaleModelAboutViewpoint(widget.LocalTransform,
                widget.transform.parent.InverseTransformPoint(position), scaleFactor);
            var sizeRange = widget.GetWidgetSizeRange();
            if (Mathf.Abs(modelPose.scale) < sizeRange.x || Mathf.Abs(modelPose.scale) > sizeRange.y)
                throw new InvalidOperationException("Authored viewpoint scale exceeds this model's size limits");
            var camera = App.VrSdk.GetVrCamera();
            var origin = camera.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
            Vector3 target = camera.transform.position;
            if (pose.isFloorLevel != 0) target.y = origin != null ? origin.transform.position.y : camera.transform.position.y;
            Quaternion yawDelta = Yaw(camera.transform.rotation) * Quaternion.Inverse(Yaw(rotation));
            TrTransform scenePose = TrTransform.TR(target, yawDelta) *
                TrTransform.T(-position) * App.Scene.Pose;
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new NavigateModelViewpointCommand(widget, modelPose, scenePose));
            // Apply the evaluated pose once. Model playback never follows viewpoint animation.
            Document.SetSpawnAreaNeedsUpdate(false);
            return true;
        }
        private sealed class NavigateModelViewpointCommand : BaseCommand
        {
            private readonly TrTransform m_BeforeScenePose;
            private readonly TrTransform m_AfterScenePose;

            internal NavigateModelViewpointCommand(
                ModelWidget widget, TrTransform modelPose, TrTransform scenePose)
            {
                m_BeforeScenePose = App.Scene.Pose;
                m_AfterScenePose = scenePose;
                new MoveWidgetCommand(widget, modelPose, widget.CustomDimension, true, this);
            }

            protected override void OnRedo() { App.Scene.Pose = m_AfterScenePose; }
            protected override void OnUndo() { App.Scene.Pose = m_BeforeScenePose; }
        }

        internal static TrTransform ScaleModelAboutViewpoint(TrTransform modelPose, Vector3 viewpoint, float factor)
        {
            modelPose.translation = viewpoint + (modelPose.translation - viewpoint) * factor;
            modelPose.scale *= factor;
            return modelPose;
        }
        private static Quaternion Yaw(Quaternion rotation)
        {
            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.Cross(rotation * Vector3.right, Vector3.up);
            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private IEnumerator RestoreNative(ImmDocument document, ModelPlaybackState state)
        {
            document.SetVolume(0);
            document.PauseAt(0);
            yield return WaitForPauseState(document, true);
            if (state.Chapter.HasValue)
            {
                if (state.Chapter.Value < 0 || state.Chapter.Value >= document.GetChapterCount())
                    throw new ArgumentOutOfRangeException(nameof(state.Chapter));
                if (document.GetCurrentChapter() != state.Chapter.Value)
                {
                    yield return WaitForRequest(document, () => document.RequestSetChapter(state.Chapter.Value));
                    document.PauseAt(0);
                    yield return WaitForPauseState(document, true);
                }
            }
            if (m_RestartAtChapter)
            {
                state.Time = GetChapterStartTime(document, state.Chapter.Value);
                state.StopTime = 0;
                m_RestartAtChapter = false;
            }
            yield return WaitForRequest(document, () => document.RequestSetTime(
                ToTicks(state.Time), ToTicks(state.StopTime ?? 0)));
            SetVolumeInternal(state.Volume);
            SetPlayingInternal(!state.Paused);
            yield return WaitForPauseState(document, state.Paused);
            m_InitialState = null;
        }
        private static double GetChapterStartTime(ImmDocument document, int chapter)
        {
            if (chapter == 0) return 0;
            if (!document.TryGetChapterInfo(out var info))
                throw new InvalidOperationException("IMM chapter layout is not ready");
            // Stop-defined chapters begin one tick after the marker; Play-defined
            // chapters begin on it, matching the native SetChapter implementation.
            return ModelPlaybackState.GetChapterStartTime(info.Lengths, info.HasPlays, chapter);
        }
        private static void ResumeNative(ImmDocument document)
        {
            // Zero means "now" and clears any timestamp left by a previous command.
            document.ResumeAt(0);
        }
        private IEnumerator ContinueNative(ImmDocument document)
        {
            ResumeNative(document);
            yield return WaitForPauseState(document, false);
            document.Continue();
            yield return null;
            yield return null;
        }
        private IEnumerator WaitForPauseState(ImmDocument document, bool paused)
        {
            float started = UnityEngine.Time.realtimeSinceStartup;
            yield return null;
            while ((document.GetStateInfo().Playback == ImmDocument.PlaybackState.Paused) != paused)
            {
                if (UnityEngine.Time.realtimeSinceStartup - started > 120)
                    throw new InvalidOperationException("IMM pause/resume timed out");
                yield return null;
            }
        }
        private IEnumerator WaitForEdit(ImmDocument document, Func<ulong> request, bool paused, double? chapterStart = null)
        {
            float volume = m_InitialState.Volume;
            if (paused) document.SetVolume(0);
            yield return WaitForRequest(document, request);
            if (paused)
            {
                document.PauseAt(0);
                yield return WaitForPauseState(document, true);
                if (chapterStart.HasValue)
                {
                    // Freeze on the first tick inside the selected chapter. The native
                    // chapter command resumes playback and marker lookup uses strict <.
                    yield return WaitForRequest(document,
                        () => document.RequestSetTime(ToTicks(chapterStart.Value) + 1, 0));
                }
                document.SetVolume(volume);
            }
        }
        private IEnumerator WaitForRequest(ImmDocument document, Func<ulong> request)
        {
            ulong token = 0;
            float started = UnityEngine.Time.realtimeSinceStartup;
            while (token == 0)
            {
                token = request();
                if (UnityEngine.Time.realtimeSinceStartup - started > 120) throw new InvalidOperationException("IMM command was rejected or remained busy");
                yield return null;
            }
            while (true)
            {
                var snapshot = ReadSnapshot(document);
                if (snapshot.IsComplete(token)) yield break;
                if (snapshot.requestedCommand != token || snapshot.commandStatus == PlaybackRequestStatus.Rejected)
                    throw new InvalidOperationException("IMM command was rejected or superseded");
                if (UnityEngine.Time.realtimeSinceStartup - started > 120) throw new InvalidOperationException("IMM command timed out");
                yield return null;
            }
        }
        private SpawnAreaSnapshot ReadSnapshot(ImmDocument document)
        {
            if (document.TryGetSpawnAreaSnapshot(m_ViewpointIds, out var snapshot))
                return snapshot;
            if (snapshot.availableCount <= m_ViewpointIds.Length) return default;
            m_ViewpointIds = new int[snapshot.availableCount];
            return document.TryGetSpawnAreaSnapshot(m_ViewpointIds, out snapshot)
                ? snapshot : default;
        }
        private void StartNative(Func<ImmDocument, IEnumerator> operation)
        {
            LastError = null;
            m_RestoreFailed = false;
            m_Operation = StartCoroutine(RunNative(operation));
        }
        private IEnumerator RunNative(Func<ImmDocument, IEnumerator> operation)
        {
            yield return null;
            try
            {
                var enumerator = Flatten(operation(Document));
                try
                {
                    while (true)
                    {
                        bool next;
                        try { next = enumerator.MoveNext(); }
                        catch (Exception error) { ReportError(error); break; }
                        if (!next) break;
                        yield return enumerator.Current;
                    }
                    if (!m_RestoreFailed) m_InitialState = null;
                }
                finally { (enumerator as IDisposable)?.Dispose(); }
            }
            finally { m_Operation = null; }
        }
        private static IEnumerator Flatten(IEnumerator routine)
        {
            try
            {
                while (routine.MoveNext())
                {
                    if (routine.Current is IEnumerator child)
                    {
                        var nested = Flatten(child);
                        while (nested.MoveNext()) yield return nested.Current;
                    }
                    else yield return routine.Current;
                }
            }
            finally { (routine as IDisposable)?.Dispose(); }
        }
        private void ReportError(Exception error)
        {
            LastError = error.Message;
            m_RestoreFailed = true;
            Debug.LogWarning($"[OB_MODEL_PLAYBACK_20261016] {error.Message}");
            OutputWindowScript.Error("Model playback failed", error.Message);
        }
        private void RequireReady()
        {
            if (!Initialized) throw new InvalidOperationException("Model playback is not ready or not supported");
            if (Busy || m_InitialState != null && !m_RestoreFailed) throw new InvalidOperationException("Model playback is busy");
            if (m_RestoreFailed) { m_InitialState = null; m_RestoreFailed = false; LastError = null; }
        }
        private static void ValidateTime(double time)
        {
            if (double.IsNaN(time) || double.IsInfinity(time) || time < 0 || time > long.MaxValue / ImmTicksPerSecond)
                throw new ArgumentOutOfRangeException(nameof(time));
        }
        private static long ToTicks(double time) { ValidateTime(time); return checked((long)Math.Round(time * ImmTicksPerSecond)); }
        private static void ValidateVolume(float volume)
        {
            if (float.IsNaN(volume) || float.IsInfinity(volume) || volume < 0 || volume > 1)
                throw new ArgumentOutOfRangeException(nameof(volume));
        }
    }
}
