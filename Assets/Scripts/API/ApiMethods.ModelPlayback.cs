// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.
using Newtonsoft.Json;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint("model.play", "Play or continue one model instance. Index is zero-based; negative indices count from the end.")]
        public static void PlayModel(int index) => _GetActiveModel(index).Playback.Play();
        [ApiEndpoint("model.continue", "Continue a paused model or an IMM authored stop point.")]
        public static void ContinueModel(int index) => _GetActiveModel(index).Playback.Continue();
        [ApiEndpoint("model.pause", "Pause one animated model instance.")]
        public static void PauseModel(int index) => _GetActiveModel(index).Playback.Pause();
        [ApiEndpoint("model.seek", "Seek one model instance to time in seconds. IMM seeks are asynchronous; inspect model.playback.")]
        public static void SeekModel(int index, float seconds) => _GetActiveModel(index).Playback.Seek(seconds);
        [ApiEndpoint("model.volume", "Set one model's audio volume from 0 to 1. Zero means muted.")]
        public static void SetModelVolume(int index, float volume) => _GetActiveModel(index).Playback.SetVolume(volume);
        [ApiEndpoint("model.clip", "Select a glTF animation clip by name.")]
        public static void SetModelClip(int index, string clip) => _GetActiveModel(index).Playback.SelectClip(clip);
        [ApiEndpoint("model.loop", "Set looping for one glTF animation instance.")]
        public static void SetModelLoop(int index, bool loop) => _GetActiveModel(index).Playback.SetLoop(loop);
        [ApiEndpoint("model.chapter", "Select an IMM chapter by zero-based index without moving the user. Asynchronous; inspect model.playback.")]
        public static void SetModelChapter(int index, int chapter) => _GetActiveModel(index).Playback.SetChapter(chapter);
        [ApiEndpoint("model.viewpoints", "List IMM viewpoint IDs, names, current availability and active selection as JSON. HTTP returns a handle; retrieve the result with query.command=handle.")]
        public static string GetModelViewpoints(int index) => JsonConvert.SerializeObject(_GetActiveModel(index).Playback.GetViewpoints());
        [ApiEndpoint("model.viewpoint", "Explicitly move the user to an eligible IMM viewpoint ID, including authored scale and floor/eye height.")]
        public static void NavigateModelViewpoint(int index, int viewpoint) => _GetActiveModel(index).Playback.NavigateToViewpoint(viewpoint);
        [ApiEndpoint("model.playback", "Return model playback state, readiness, pending operation, audio and chapter/clip capabilities as JSON. HTTP returns a handle; retrieve the result with query.command=handle.")]
        public static string GetModelPlayback(int index)
        {
            var playback = _GetActiveModel(index).Playback;
            return JsonConvert.SerializeObject(new
            {
                initialized = playback.Initialized, busy = playback.Busy || playback.IsRestoring,
                error = playback.LastError, playing = playback.Playing, time = playback.Time,
                length = playback.Length, volume = playback.Volume, hasAudio = playback.HasAudio,
                chapter = playback.Chapter, chapterCount = playback.ChapterCount, clips = playback.Clips,
                state = playback.GetSaveState()
            });
        }
    }
}
