using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TiltBrush
{
    public static class WaveGenerator
    {
        public enum Mode
        {
            SineWave,
            CosineWave,
            TriangleWave,
            SawtoothWave,
            SquareWave,
            PulseWave,
            ExponentWave,
            PowerWave,
            ParabolicWave,
            ExponentialSawtoothWave,
            PerlinNoise,
            WhiteNoise,
            BrownNoise,
            BlueNoise,
        }

        public static float SineWave(float t, float freq) => Mathf.Sin(t * freq * Mathf.PI * 2);
        public static float CosineWave(float t, float freq) => Mathf.Cos(t * freq * Mathf.PI * 2);
        public static float TriangleWave(float t, float freq) => Mathf.Abs(t * freq * 4 % 4 - 2) - 1;
        public static float SawtoothWave(float t, float freq) => (t * freq % 1 - 0.5f) * 2f;
        public static float SquareWave(float t, float freq) => t * freq % 1 < 0.5f ? -1 : 1;
        public static float PerlinNoise(float t, float freq) => Mathf.PerlinNoise(t * freq * 2, 0) * 3f - 1.5f;
        public static float ParabolicWave(float t, float freq)
        {
            float value = TriangleWave(t, freq);
            return value * value * Mathf.Sign(value);
        }
        public static float ExponentWave(float t, float freq) => Mathf.Exp(Mathf.Abs(Mathf.Abs(t * freq) * 4 % 4 - 2) - 1) * 0.85f - 1.3f;
        public static float PowerWave(float t, float freq, float power) =>
            // Squares and higher powers
            power >= 1 ? Mathf.Pow(Mathf.Abs(t * freq * 2 % 4 - 2) - 1, power) * 2 - 1 :
            // Square and higher roots
            Mathf.Pow(Mathf.Abs(t * freq * 2 % 2 - 1), power) * 2 - 1;
        public static float PulseWave(float t, float freq, float pulseWidth) => 2 * (t * freq % 1 < pulseWidth ? 0 : 1) - 1;
        public static float ExponentialSawtoothWave(float t, float freq, float exp) =>
            (1 - Mathf.Exp(SawtoothWave(t, freq) * exp)) / (1 - Mathf.Exp(exp)) * 2 - 1;
        public static float WhiteNoise() => 2 * Random.value - 1;
        public static float BrownNoise(float previous)
        {
            float val = previous + WhiteNoise() * 0.1f;
            return val is < 1 and > -1 ? val : previous;
        }
        public static float BlueNoise(float previous)
        {
            var val = previous + WhiteNoise() - previous * 0.1f;
            return val is < 1 and > -1 ? val : previous;
        }

        // Zero parameter waveforms
        public static float Sample(Mode mode) => mode switch
        {
            Mode.WhiteNoise => WhiteNoise(),
            _ => 0
        };

        // One parameter waveforms
        public static float Sample(Mode mode, float previous) => mode switch
        {
            Mode.BrownNoise => BrownNoise(previous),
            Mode.BlueNoise => BlueNoise(previous),
            _ => 0
        };

        // Two parameter waveforms
        public static float Sample(Mode mode, float t, float freq) => mode switch
        {
            Mode.SineWave => SineWave(t, freq),
            Mode.CosineWave => CosineWave(t, freq),
            Mode.TriangleWave => TriangleWave(t, freq),
            Mode.SawtoothWave => SawtoothWave(t, freq),
            Mode.SquareWave => SquareWave(t, freq),
            Mode.ParabolicWave => PerlinNoise(t, freq),
            Mode.ExponentWave => ExponentWave(t, freq),
            Mode.PerlinNoise => PerlinNoise(t, freq),
            _ => 0
        };

        // Three parameter waveforms
        public static float Sample(Mode mode, float t, float freq, float x) => mode switch
        {
            Mode.PulseWave => PulseWave(t, freq, x),
            Mode.ExponentialSawtoothWave => ExponentialSawtoothWave(t, freq, x),
            Mode.PowerWave => PowerWave(t, freq, x),
            _ => 0
        };

        // Bulk generators

        // Zero parameter waveforms
        public static float[] Generate(Func<float> func, float duration, int sampleRate, float amplitude = 1)
        {
            int numSamples = Mathf.FloorToInt(duration * sampleRate);
            return Generate(func, numSamples, amplitude);
        }
        public static float[] Generate(Func<float> func, int numSamples, float amplitude = 1)
        {
            var samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                samples[i] = func() * amplitude;
            }
            return samples;
        }

        // One parameter waveforms
        public static float[] Generate(Func<float, float> func, float duration, int sampleRate, float amplitude = 1)
        {
            int numSamples = Mathf.FloorToInt(duration * sampleRate);
            return Generate(func, numSamples, amplitude);
        }
        public static float[] Generate(Func<float, float> func, int numSamples, float amplitude = 1)
        {
            float previous = 0;
            var samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                samples[i] = func(previous) * amplitude;
            }
            return samples;
        }


        // Two parameter waveforms

        public static float[] Generate(Func<float, float, float> func, float freq, float startTime, float duration, int sampleRate, float amplitude = 1)
        {
            int numSamples = Mathf.FloorToInt(duration * sampleRate);
            return Generate(func, freq, startTime, numSamples, 1f / sampleRate, amplitude);
        }
        public static float[] Generate(Func<float, float, float> func, float freq, float startTime, int numSamples, float timeDelta, float amplitude = 1)
        {
            var samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                samples[i] = func(startTime + timeDelta * i, freq) * amplitude;
            }
            return samples;
        }

        // Three parameter waveforms

        public static float[] Generate(Func<float, float, float, float> func, float freq, float param2, float startTime, float duration, int sampleRate, float amplitude = 1)
        {
            int numSamples = Mathf.FloorToInt(duration * sampleRate);
            return Generate(func, freq, param2, startTime, numSamples, 1f / sampleRate, amplitude);
        }
        public static float[] Generate(Func<float, float, float, float> func, float freq, float param2, float startTime, int numSamples, float timeDelta, float amplitude = 1)
        {
            var samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                samples[i] = func(startTime + timeDelta * i, freq, param2) * amplitude;
            }
            return samples;
        }
    }
}
