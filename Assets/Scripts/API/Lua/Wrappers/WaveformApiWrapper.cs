using MoonSharp.Interpreter;
namespace TiltBrush
{
    [LuaDocsDescription("Functions to generate a variety of waveforms")]
    [MoonSharpUserData]
    public static class WaveformApiWrapper
    {
        [LuaDocsDescription("Returns the value of a sine wave at the given time and frequency")]
        public static float Sine(float time, float frequency) => WaveGenerator.SineWave(time, frequency);


        [LuaDocsDescription("Returns the value of a cosine wave at the given time and frequency")]
        public static float Cosine(float time, float frequency) => WaveGenerator.CosineWave(time, frequency);


        [LuaDocsDescription("Returns the value of a triangle wave at the given time and frequency")]
        public static float Triangle(float time, float frequency) => WaveGenerator.TriangleWave(time, frequency);


        [LuaDocsDescription("Returns the value of a sawtooth wave at the given time and frequency")]
        public static float Sawtooth(float time, float frequency) => WaveGenerator.SawtoothWave(time, frequency);


        [LuaDocsDescription("Returns the value of a square wave at the given time and frequency")]
        public static float Square(float time, float frequency) => WaveGenerator.SquareWave(time, frequency);


        [LuaDocsDescription("Returns the value of a pulse wave with a specified pulse width at the given time, frequency")]
        public static float Pulse(float time, float frequency, float pulseWidth) => WaveGenerator.PulseWave(time, frequency, pulseWidth);

        [LuaDocsDescription("Returns the value of an exponential wave at the given time and frequency")]
        public static float Exponent(float time, float frequency) => WaveGenerator.ExponentWave(time, frequency);

        [LuaDocsDescription("Returns the value of a power wave at the given time, frequency, and power")]
        public static float Power(float time, float frequency, float power) => WaveGenerator.PowerWave(time, frequency, power);

        [LuaDocsDescription("Returns the value of a parabolic wave at the given time and frequency")]
        public static float Parabolic(float time, float frequency) => WaveGenerator.ParabolicWave(time, frequency);

        [LuaDocsDescription("Returns the value of an exponential sawtooth wave with the specified exponent at the given time, frequency")]
        public static float ExponentialSawtooth(float time, float frequency, float exponent) => WaveGenerator.ExponentialSawtoothWave(time, frequency, exponent);


        [LuaDocsDescription("Returns the value of a perlin noise function at the given time and frequency")]
        public static float PerlinNoise(float time, float frequency) => WaveGenerator.PerlinNoise(time, frequency);

        [LuaDocsDescription("Returns the value of a white noise function")]
        public static float WhiteNoise() => WaveGenerator.WhiteNoise();

        [LuaDocsDescription("Returns the value of a brown noise function")]
        public static float BrownNoise(float previous) => WaveGenerator.BrownNoise(previous);

        [LuaDocsDescription("Returns the value of a blue noise function")]
        public static float BlueNoise(float previous) => WaveGenerator.BlueNoise(previous);

        // Bulk methods

        [LuaDocsDescription("Returns a sine wave with the given frequency, duration, and sample rate")]
        public static float[] Sine(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.SineWave, frequency, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a cosine wave with the given frequency, duration, and sample rate")]
        public static float[] Cosine(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.CosineWave, frequency, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a triangle wave with the given frequency, duration, and sample rate")]
        public static float[] Triangle(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.TriangleWave, frequency, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a sawtooth wave with the given frequency, duration, and sample rate")]
        public static float[] Sawtooth(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.SawtoothWave, frequency, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a square wave with the given frequency, duration, and sample rate")]
        public static float[] Square(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.SquareWave, frequency, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns an exponential wave with the given frequency, duration, and sample rate")]
        public static float[] Exponent(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.ExponentWave, frequency, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a parabolic wave with the given frequency, duration, and sample rate")]
        public static float[] Parabolic(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.ParabolicWave, frequency, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a pulse wave with the given frequency, pulse width, duration, and sample rate")]
        public static float[] Pulse(float time, float frequency, float pulseWidth, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.PulseWave, frequency, pulseWidth, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a power wave with the given frequency, power, duration, and sample rate")]
        public static float[] Power(float time, float frequency, float power, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.PowerWave, frequency, power, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns an exponential sawtooth wave with the given frequency, exponent, duration, and sample rate")]
        public static float[] ExponentialSawtoothWave(float time, float frequency, float exponent, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.ExponentialSawtoothWave, frequency, exponent, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a perlin noise wave with the given frequency, duration, and sample rate")]
        public static float[] PerlinNoise(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.PerlinNoise, frequency, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a white noise wave with the given duration and sample rate")]
        public static float[] WhiteNoise(float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(WaveGenerator.WhiteNoise, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a brown noise wave with the given duration and sample rate")]
        public static float[] BrownNoise(float previous, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(WaveGenerator.BrownNoise, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a blue noise wave with the given duration and sample rate")]
        public static float[] BlueNoise(float previous, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(WaveGenerator.BlueNoise, duration, sampleRate, amplitude);
    }
}
