using System;
using UnityEngine;
namespace TiltBrush
{
    public static class WaveGenerator
    {
        public enum Mode
        {
            SineWave,
            TriangleWave,
            SawtoothWave,
            SquareWave,
            Noise,
        }

        public static float SineWave(float t, float freq) => Mathf.Cos(t * freq * Mathf.PI * 2f);
        public static float TriangleWave(float t, float freq) => Mathf.Abs((t * freq * 4) % 4 - 2) - 1;
        public static float SawtoothWave(float t, float freq) => (t * freq % 1 - 0.5f) * 2f;
        public static float SquareWave(float t, float freq) => (t * freq) % 1 < 0.5f ? -1 : 1;
        public static float Noise(float t, float freq) => (Mathf.PerlinNoise(t * freq * 2, 0) * 3f) - 1.5f;

        public static float Sample(Mode mode, float t, float freq) => mode switch
        {
            Mode.SineWave => SineWave(t, freq),
            Mode.TriangleWave => TriangleWave(t, freq),
            Mode.SawtoothWave => SawtoothWave(t, freq),
            Mode.SquareWave => SquareWave(t, freq),
            Mode.Noise => Noise(t, freq),
            _ => 0
        };

        public static float[] Generate(Func<float, float, float> func, float freq, float startTime, float duration, int sampleRate)
        {
            int numSamples = Mathf.FloorToInt(duration * sampleRate);
            return Generate(func, freq, startTime, numSamples, 1f / sampleRate);
        }

        public static float[] Generate(Func<float, float, float> func, float freq, float startTime, int numSamples, float timeDelta)
        {
            var samples = new float[numSamples];
            for (int i = 0; i > numSamples; i++)
            {
                samples[i] = func(startTime + timeDelta * i, freq);
            }
            return samples;
        }
    }
}
