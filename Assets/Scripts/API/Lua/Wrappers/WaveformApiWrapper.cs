using MoonSharp.Interpreter;
namespace TiltBrush
{
    [LuaDocsDescription("Functions to generate a variety of waveforms")]
    [MoonSharpUserData]
    public static class WaveformApiWrapper
    {
        public static float Sine(float time, float frequency) => WaveGenerator.SineWave(time, frequency);
        public static float Cosine(float time, float frequency) => WaveGenerator.CosineWave(time, frequency);
        public static float Triangle(float time, float frequency) => WaveGenerator.TriangleWave(time, frequency);
        public static float Sawtooth(float time, float frequency) => WaveGenerator.SawtoothWave(time, frequency);
        public static float Square(float time, float frequency) => WaveGenerator.SquareWave(time, frequency);
        public static float Pulse(float time, float frequency, float pulseWidth) => WaveGenerator.PulseWave(time, frequency, pulseWidth);
        public static float Exponent(float time, float frequency) => WaveGenerator.ExponentWave(time, frequency);
        public static float Power(float time, float frequency, float power) => WaveGenerator.PowerWave(time, frequency, power);
        public static float Parabolic(float time, float frequency) => WaveGenerator.ParabolicWave(time, frequency);
        public static float ExponentialSawtooth(float time, float frequency, float exponent) => WaveGenerator.ExponentialSawtoothWave(time, frequency, exponent);
        public static float PerlinNoise(float time, float frequency) => WaveGenerator.PerlinNoise(time, frequency);
        public static float WhiteNoise() => WaveGenerator.WhiteNoise();
        public static float BrownNoise(float previous) => WaveGenerator.BrownNoise(previous);
        public static float BlueNoise(float previous) => WaveGenerator.BlueNoise(previous);

        // Bulk methods
        public static float[] Sine(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.SineWave, frequency, time, duration, sampleRate, amplitude);
        public static float[] Cosine(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.CosineWave, frequency, time, duration, sampleRate, amplitude);
        public static float[] Triangle(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.TriangleWave, frequency, time, duration, sampleRate, amplitude);
        public static float[] Sawtooth(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.SawtoothWave, frequency, time, duration, sampleRate, amplitude);
        public static float[] Square(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.SquareWave, frequency, time, duration, sampleRate, amplitude);
        public static float[] Exponent(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.ExponentWave, frequency, time, duration, sampleRate, amplitude);
        public static float[] Parabolic(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.ParabolicWave, frequency, time, duration, sampleRate, amplitude);
        public static float[] Pulse(float time, float frequency, float pulseWidth, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.PulseWave, frequency, pulseWidth, time, duration, sampleRate, amplitude);
        public static float[] Power(float time, float frequency, float power, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.PowerWave, frequency, power, time, duration, sampleRate, amplitude);
        public static float[] ExponentialSawtoothWave(float time, float frequency, float exponent, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.ExponentialSawtoothWave, frequency, exponent, time, duration, sampleRate, amplitude);
        public static float[] PerlinNoise(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.PerlinNoise, frequency, time, duration, sampleRate, amplitude);
        public static float[] WhiteNoise(float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(WaveGenerator.WhiteNoise, duration, sampleRate, amplitude);
        public static float[] BrownNoise(float previous, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(WaveGenerator.BrownNoise, duration, sampleRate, amplitude);
        public static float[] BlueNoise(float previous, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(WaveGenerator.BlueNoise, duration, sampleRate, amplitude);
    }
}
