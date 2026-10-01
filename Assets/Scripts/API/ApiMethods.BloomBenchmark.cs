// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0.
namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint("bloom.benchmark.configure", "Persists benchmark profile, MSAA and eye scale for next launch", "encoded-both,4,1,configure1")]
        public static void ConfigureBloomBenchmark(string profile, int msaa, float eyeScale, string token)
        {
            BloomBenchmark.Configure(profile, msaa, eyeScale, token);
        }

        [ApiEndpoint("bloom.benchmark.tune", "Sets levels, downsample, amount, threshold, native iterations, quarter, HQ, scatter, intensity, token", "3,2,1,0,2,true,false,0.35,1,tune1")]
        public static void TuneBloomBenchmark(int levels, int downsample, float amount, float threshold,
            int iterations, bool quarter, bool highQuality, float scatter, float intensity, string token)
        {
            BloomBenchmark.Instance?.Tune(levels, downsample, amount, threshold, iterations,
                quarter, highQuality, scatter, intensity, token);
        }

        [ApiEndpoint("bloom.benchmark.start", "Starts token,warmupSeconds,sampleSeconds,fixture,qualityLevel", "run1,20,30,sparse,0")]
        public static void StartBloomBenchmark(string token, float warmup, float duration, string fixture, int quality)
        {
            BloomBenchmark.Instance?.StartCapture(token, warmup, duration, fixture, quality);
        }

        [ApiEndpoint("bloom.benchmark.stop", "Stops capture and restores fixture cameras and quality", "stop1")]
        public static void StopBloomBenchmark(string token)
        {
            BloomBenchmark.Instance?.StopCapture(token);
        }
    }
}
