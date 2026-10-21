// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.
using System;
using Newtonsoft.Json;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint("model.play", "Play or continue one model instance. Index is zero-based; negative indices count from the end.")]
        public static string PlayModel(int index) => InvokeModelPlayback(() => _GetModelPlayback(index).Play());
        [ApiEndpoint("model.continue", "Continue a paused model or an IMM authored stop point.")]
        public static string ContinueModel(int index) => InvokeModelPlayback(() => _GetModelPlayback(index).Continue());
        [ApiEndpoint("model.pause", "Pause one animated model instance.")]
        public static string PauseModel(int index) => InvokeModelPlayback(() => _GetModelPlayback(index).Pause());
        [ApiEndpoint("model.seek", "Seek one model instance to time in seconds. IMM seeks are asynchronous; inspect model.playback.")]
        public static string SeekModel(int index, float seconds) => InvokeModelPlayback(() => _GetModelPlayback(index).Seek(seconds));
        [ApiEndpoint("model.volume", "Set one model's audio volume from 0 to 1. Zero means muted.")]
        public static string SetModelVolume(int index, float volume) => InvokeModelPlayback(() => _GetModelPlayback(index).SetVolume(volume));
        [ApiEndpoint("model.clip", "Select a glTF animation clip by name.")]
        public static string SetModelClip(int index, string clip) => InvokeModelPlayback(() => _GetModelPlayback(index).SelectClip(clip));
        [ApiEndpoint("model.loop", "Set looping for one glTF animation instance.")]
        public static string SetModelLoop(int index, bool loop) => InvokeModelPlayback(() => _GetModelPlayback(index).SetLoop(loop));
        [ApiEndpoint("model.chapter", "Select an IMM chapter by zero-based index without moving the user. Asynchronous; inspect model.playback.")]
        public static string SetModelChapter(int index, int chapter) => InvokeModelPlayback(() => _GetModelPlayback(index).SetChapter(chapter));
        [ApiEndpoint("model.viewpoints", "List IMM viewpoint IDs, names, current availability and active selection as JSON. HTTP returns a handle; retrieve the result with query.command=handle.")]
        public static string GetModelViewpoints(int index) => InvokeModelPlayback(() => JsonConvert.SerializeObject(_GetModelPlayback(index).GetViewpoints()));
        [ApiEndpoint("model.viewpoint.select", "Select an eligible IMM viewpoint ID on one model instance without moving or rescaling anything.")]
        public static string SelectModelViewpoint(int index, int viewpoint) => InvokeModelPlayback(() => _GetModelPlayback(index).SelectViewpoint(viewpoint));
        [ApiEndpoint("model.viewpoint", "Explicitly move the user to an eligible IMM viewpoint ID, including authored scale and floor/eye height.")]
        public static string NavigateModelViewpoint(int index, int viewpoint) => InvokeModelPlayback(() => _GetModelPlayback(index).NavigateToViewpoint(viewpoint));
        [ApiEndpoint("model.playback", "Return model playback state, readiness, pending operation, audio and chapter/clip capabilities as JSON. HTTP returns a handle; retrieve the result with query.command=handle.")]
        public static string GetModelPlayback(int index) => InvokeModelPlayback(() =>
        {
            var playback = _GetModelPlayback(index);
            return JsonConvert.SerializeObject(new
            {
                initialized = playback.Initialized,
                busy = playback.Busy || playback.IsRestoring,
                error = playback.LastError,
                playing = playback.Playing,
                time = playback.Time,
                length = playback.Length,
                volume = playback.Volume,
                hasAudio = playback.HasAudio,
                chapter = playback.Chapter,
                chapterCount = playback.ChapterCount,
                clips = playback.Clips,
                state = playback.GetSaveState()
            });
        });

        private static ModelPlaybackController _GetModelPlayback(int index)
        {
            int count = WidgetManager.m_Instance.ActiveModelWidgets.Count;
            return _GetActiveModel(NormalizeModelPlaybackIndex(index, count)).Playback;
        }
        internal static int NormalizeModelPlaybackIndex(int index, int count)
        {
            long normalized = index < 0 ? (long)count + index : index;
            if (normalized < 0 || normalized >= count)
                throw new ArgumentOutOfRangeException(nameof(index), $"Model index {index} is out of range ({count} active models)");
            return (int)normalized;
        }
        private static string InvokeModelPlayback(Action action) => InvokeModelPlayback(() =>
        {
            action();
            return "ok";
        });
        internal static string InvokeModelPlayback(Func<string> action)
        {
            try { return action(); }
            catch (ArgumentException error) { return $"error: {error.Message}"; }
            catch (InvalidOperationException error) { return $"error: {error.Message}"; }
        }
    }
}
