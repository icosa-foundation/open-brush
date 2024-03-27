using MoonSharp.Interpreter;
namespace TiltBrush
{
    [LuaDocsDescription("Functions to generate a variety of waveforms")]
    [MoonSharpUserData]
    public static class WaveformApiWrapper
    {
        [LuaDocsDescription("Returns the value of a sine wave at the given time")]
        [LuaDocsExample("value = Waveform.Sine(0, 6)")]
        [LuaDocsParameter("time", "The time to sample the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsReturnValue("The value of the wave sampled at the given time")]
        public static float Sine(float time, float frequency) => WaveGenerator.SineWave(time, frequency);


        [LuaDocsDescription("Returns the value of a cosine wave at the given time")]
        [LuaDocsExample("value = Waveform.Cosine(0, 6)")]
        [LuaDocsParameter("time", "The time to sample the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsReturnValue("The value of the wave sampled at the given time")]
        public static float Cosine(float time, float frequency) => WaveGenerator.CosineWave(time, frequency);


        [LuaDocsDescription("Returns the value of a triangle wave at the given time")]
        [LuaDocsExample("value = Waveform.Triangle(0, 6)")]
        [LuaDocsParameter("time", "The time to sample the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsReturnValue("The value of the wave sampled at the given time")]
        public static float Triangle(float time, float frequency) => WaveGenerator.TriangleWave(time, frequency);


        [LuaDocsDescription("Returns the value of a sawtooth wave at the given time")]
        [LuaDocsExample("value = Waveform.Sawtooth(0, 6)")]
        [LuaDocsParameter("time", "The time to sample the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsReturnValue("The value of the wave sampled at the given time")]
        public static float Sawtooth(float time, float frequency) => WaveGenerator.SawtoothWave(time, frequency);


        [LuaDocsDescription("Returns the value of a square wave at the given time")]
        [LuaDocsExample("value = Waveform.Square(0, 6)")]
        [LuaDocsParameter("time", "The time to sample the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsReturnValue("The value of the wave sampled at the given time")]
        public static float Square(float time, float frequency) => WaveGenerator.SquareWave(time, frequency);


        [LuaDocsDescription("Returns the value of a pulse wave with a specified pulse width at the given time, frequency")]
        [LuaDocsExample("value = Waveform.Pulse(0, 6, 0.2)")]
        [LuaDocsParameter("time", "The time to sample the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsParameter("pulseWidth", "The width of the pulse")]
        [LuaDocsReturnValue("The value of the wave sampled at the given time")]
        public static float Pulse(float time, float frequency, float pulseWidth) => WaveGenerator.PulseWave(time, frequency, pulseWidth);

        [LuaDocsDescription("Returns the value of an exponential wave at the given time")]
        [LuaDocsExample("value = Waveform.Exponent(0, 6)")]
        [LuaDocsParameter("time", "The time to sample the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsReturnValue("The value of the wave sampled at the given time")]
        public static float Exponent(float time, float frequency) => WaveGenerator.ExponentWave(time, frequency);

        [LuaDocsDescription("Returns the value of a power wave at the given time, frequency, and power")]
        [LuaDocsExample("value = Waveform.Power(0, 6, 2)")]
        [LuaDocsParameter("time", "The time to sample the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsParameter("power", "The power exponent of the wave")]
        [LuaDocsReturnValue("The value of the wave sampled at the given time")]
        public static float Power(float time, float frequency, float power) => WaveGenerator.PowerWave(time, frequency, power);

        [LuaDocsDescription("Returns the value of a parabolic wave at the given time")]
        [LuaDocsExample("value = Waveform.Parabolic(0, 6)")]
        [LuaDocsParameter("time", "The time to sample the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsReturnValue("The value of the wave sampled at the given time")]
        public static float Parabolic(float time, float frequency) => WaveGenerator.ParabolicWave(time, frequency);

        [LuaDocsDescription("Returns the value of an exponential sawtooth wave with the specified exponent at the given time, frequency")]
        [LuaDocsExample("value = Waveform.ExponentialSawtooth(0, 6, 2)")]
        [LuaDocsParameter("time", "The time to sample the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsParameter("exponent", "The exponent of the wave")]
        [LuaDocsReturnValue("The value of the wave sampled at the given time")]
        public static float ExponentialSawtooth(float time, float frequency, float exponent) => WaveGenerator.ExponentialSawtoothWave(time, frequency, exponent);


        [LuaDocsDescription("Returns the value of a perlin noise function at the given time")]
        [LuaDocsExample("value = Waveform.PerlinNoise(0, 6)")]
        [LuaDocsParameter("time", "The time to sample the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsReturnValue("The value of the wave sampled at the given time")]
        public static float PerlinNoise(float time, float frequency) => WaveGenerator.PerlinNoise(time, frequency);

        [LuaDocsDescription("Returns the value of a white noise function")]
        [LuaDocsExample("value = Waveform.WhiteNoise()")]
        [LuaDocsReturnValue("The value of the wave sampled at the given time")]
        public static float WhiteNoise() => WaveGenerator.WhiteNoise();

        [LuaDocsDescription("Returns the value of a brown noise function")]
        [LuaDocsExample("value = Waveform.BrownNoise(previousValue)")]
        [LuaDocsParameter("previous", "The previous calculated value to feed back into the function")]
        [LuaDocsReturnValue("The value of the wave sampled at the given time")]
        public static float BrownNoise(float previous) => WaveGenerator.BrownNoise(previous);

        [LuaDocsDescription("Returns the value of a blue noise function")]
        [LuaDocsExample("value = Waveform.BlueNoise(previousValue)")]
        [LuaDocsParameter("previous", "The previous calculated value to feed back into the function")]
        [LuaDocsReturnValue("The value of the wave sampled at the given time")]
        public static float BlueNoise(float previous) => WaveGenerator.BlueNoise(previous);

        // Bulk methods

        [LuaDocsDescription("Returns a sine wave with the given frequency, duration, and sample rate")]
        [LuaDocsExample("wave = Waveform:Sine(0, 440, 1, 44100, 0.5)")]
        [LuaDocsParameter("time", "The time to start sampling the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsParameter("duration", "The duration of samples to generate")]
        [LuaDocsParameter("sampleRate", "The sample rate of the generated waveform")]
        [LuaDocsParameter("amplitude", "The amplitude of the generated waveform")]
        [LuaDocsReturnValue("An array of float values")]
        public static float[] Sine(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.SineWave, frequency, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a cosine wave with the given frequency, duration, and sample rate")]
        [LuaDocsExample("wave = Waveform:Cosine(0, 440, 1, 44100, 0.5)")]
        [LuaDocsParameter("time", "The time to start sampling the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsParameter("duration", "The duration of samples to generate")]
        [LuaDocsParameter("sampleRate", "The sample rate of the generated waveform")]
        [LuaDocsParameter("amplitude", "The amplitude of the generated waveform")]
        [LuaDocsReturnValue("An array of float values")]
        public static float[] Cosine(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.CosineWave, frequency, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a triangle wave with the given frequency, duration, and sample rate")]
        [LuaDocsExample("wave = Waveform:Triangle(0, 440, 1, 44100, 0.5)")]
        [LuaDocsParameter("time", "The time to start sampling the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsParameter("duration", "The duration of samples to generate")]
        [LuaDocsParameter("sampleRate", "The sample rate of the generated waveform")]
        [LuaDocsParameter("amplitude", "The amplitude of the generated waveform")]
        [LuaDocsReturnValue("An array of float values")]
        public static float[] Triangle(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.TriangleWave, frequency, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a sawtooth wave with the given frequency, duration, and sample rate")]
        [LuaDocsExample("wave = Waveform:Sawtooth(0, 440, 1, 44100, 0.5)")]
        [LuaDocsParameter("time", "The time to start sampling the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsParameter("duration", "The duration of samples to generate")]
        [LuaDocsParameter("sampleRate", "The sample rate of the generated waveform")]
        [LuaDocsParameter("amplitude", "The amplitude of the generated waveform")]
        [LuaDocsReturnValue("An array of float values")]
        public static float[] Sawtooth(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.SawtoothWave, frequency, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a square wave with the given frequency, duration, and sample rate")]
        [LuaDocsExample("wave = Waveform:Square(0, 440, 1, 44100, 0.5)")]
        [LuaDocsParameter("time", "The time to start sampling the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsParameter("duration", "The duration of samples to generate")]
        [LuaDocsParameter("sampleRate", "The sample rate of the generated waveform")]
        [LuaDocsParameter("amplitude", "The amplitude of the generated waveform")]
        [LuaDocsReturnValue("An array of float values")]
        public static float[] Square(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.SquareWave, frequency, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns an exponential wave with the given frequency, duration, and sample rate")]
        [LuaDocsExample("wave = Waveform:Exponent(0, 440, 1, 44100, 0.5)")]
        [LuaDocsParameter("time", "The time to start sampling the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsParameter("duration", "The duration of samples to generate")]
        [LuaDocsParameter("sampleRate", "The sample rate of the generated waveform")]
        [LuaDocsParameter("amplitude", "The amplitude of the generated waveform")]
        [LuaDocsReturnValue("An array of float values")]
        public static float[] Exponent(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.ExponentWave, frequency, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a parabolic wave with the given frequency, duration, and sample rate")]
        [LuaDocsExample("wave = Waveform:Parabolic(0, 440, 1, 44100, 0.5)")]
        [LuaDocsParameter("time", "The time to start sampling the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsParameter("duration", "The duration of samples to generate")]
        [LuaDocsParameter("sampleRate", "The sample rate of the generated waveform")]
        [LuaDocsParameter("amplitude", "The amplitude of the generated waveform")]
        [LuaDocsReturnValue("An array of float values")]
        public static float[] Parabolic(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.ParabolicWave, frequency, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a pulse wave with the given frequency, pulse width, duration, and sample rate")]
        [LuaDocsExample("wave = Waveform:Pulse(0, 440, 0.5, 1, 44100, 0.5)")]
        [LuaDocsParameter("time", "The time to start sampling the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsParameter("pulseWidth", "The width of the pulse")]
        [LuaDocsParameter("duration", "The duration of samples to generate")]
        [LuaDocsParameter("sampleRate", "The sample rate of the generated waveform")]
        [LuaDocsParameter("amplitude", "The amplitude of the generated waveform")]
        [LuaDocsReturnValue("An array of float values")]
        public static float[] Pulse(float time, float frequency, float pulseWidth, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.PulseWave, frequency, pulseWidth, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a power wave with the given frequency, power, duration, and sample rate")]
        [LuaDocsExample("wave = Waveform:Power(0, 440, 0.5, 1, 44100, 0.5)")]
        [LuaDocsParameter("time", "The time to start sampling the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsParameter("power", "The power exponent of the wave")]
        [LuaDocsParameter("duration", "The duration of samples to generate")]
        [LuaDocsParameter("sampleRate", "The sample rate of the generated waveform")]
        [LuaDocsParameter("amplitude", "The amplitude of the generated waveform")]
        [LuaDocsReturnValue("An array of float values")]
        public static float[] Power(float time, float frequency, float power, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.PowerWave, frequency, power, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns an exponential sawtooth wave with the given frequency, exponent, duration, and sample rate")]
        [LuaDocsExample("wave = Waveform:ExponentialSawtooth(0, 440, 0.5, 1, 44100, 0.5)")]
        [LuaDocsParameter("time", "The time to start sampling the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsParameter("exponent", "The exponent of the wave")]
        [LuaDocsParameter("duration", "The duration of samples to generate")]
        [LuaDocsParameter("sampleRate", "The sample rate of the generated waveform")]
        [LuaDocsParameter("amplitude", "The amplitude of the generated waveform")]
        [LuaDocsReturnValue("An array of float values")]
        public static float[] ExponentialSawtoothWave(float time, float frequency, float exponent, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.ExponentialSawtoothWave, frequency, exponent, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a perlin noise wave with the given frequency, duration, and sample rate")]
        [LuaDocsExample("wave = Waveform:PerlinNoise(0, 440, 1, 44100, 0.5)")]
        [LuaDocsParameter("time", "The time to start sampling the waveform at")]
        [LuaDocsParameter("frequency", "The frequency of the wave")]
        [LuaDocsParameter("duration", "The duration of samples to generate")]
        [LuaDocsParameter("sampleRate", "The sample rate of the generated waveform")]
        [LuaDocsParameter("amplitude", "The amplitude of the generated waveform")]
        [LuaDocsReturnValue("An array of float values")]
        public static float[] PerlinNoise(float time, float frequency, float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(
            WaveGenerator.PerlinNoise, frequency, time, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a white noise wave with the given duration and sample rate")]
        [LuaDocsExample("wave = Waveform:WhiteNoise(1, 44100, 0.5)")]
        [LuaDocsParameter("duration", "The duration of samples to generate")]
        [LuaDocsParameter("sampleRate", "The sample rate of the generated waveform")]
        [LuaDocsParameter("amplitude", "The amplitude of the generated waveform")]
        [LuaDocsReturnValue("An array of float values")]
        public static float[] WhiteNoise(float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(WaveGenerator.WhiteNoise, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a brown noise wave with the given duration and sample rate")]
        [LuaDocsExample("wave = Waveform:BrownNoise(1, 44100, 0.5)")]
        [LuaDocsParameter("duration", "The duration of samples to generate")]
        [LuaDocsParameter("sampleRate", "The sample rate of the generated waveform")]
        [LuaDocsParameter("amplitude", "The amplitude of the generated waveform")]
        [LuaDocsReturnValue("An array of float values")]
        public static float[] BrownNoise(float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(WaveGenerator.BrownNoise, duration, sampleRate, amplitude);

        [LuaDocsDescription("Returns a blue noise wave with the given duration and sample rate")]
        [LuaDocsExample("wave = Waveform:BlueNoise(1, 44100, 0.5)")]
        [LuaDocsParameter("duration", "The duration of samples to generate")]
        [LuaDocsParameter("sampleRate", "The sample rate of the generated waveform")]
        [LuaDocsParameter("amplitude", "The amplitude of the generated waveform")]
        [LuaDocsReturnValue("An array of float values")]
        public static float[] BlueNoise(float duration, int sampleRate, float amplitude = 1) => WaveGenerator.Generate(WaveGenerator.BlueNoise, duration, sampleRate, amplitude);
    }
}
