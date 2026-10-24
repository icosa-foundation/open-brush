// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.
using System;
using System.Linq;
using Newtonsoft.Json;

namespace TiltBrush
{
    [Serializable]
    public sealed class ModelPlaybackState
    {
        public bool Paused;
        public double Time;
        public float Volume = 1;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public double? StopTime;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public int? Chapter;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string Clip;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public bool? Loop;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public float? SpatialBlend;
        public ModelPlaybackState Copy() => (ModelPlaybackState)MemberwiseClone();
        public ModelPlaybackState ForRestore(bool restorePlayingPosition = false)
        {
            var state = Copy();
            if (!Paused && !restorePlayingPosition) { state.Time = 0; state.StopTime = 0; }
            return state;
        }
        internal static double GetChapterStartTime(long[] lengths, bool hasPlays, int chapter)
        {
            if (lengths == null || chapter < 0 || chapter >= lengths.Length)
                throw new ArgumentOutOfRangeException(nameof(chapter));
            if (chapter == 0) return 0;
            return (lengths.Take(chapter).Sum() + (hasPlays ? 0 : 1)) / 12600.0;
        }
    }
}
