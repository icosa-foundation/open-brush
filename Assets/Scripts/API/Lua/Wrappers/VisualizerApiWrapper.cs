using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [LuaDocsDescription("Settings and controls for audio visualization mode")]
    [MoonSharpUserData]
    public static class VisualizerApiWrapper
    {
        [LuaDocsDescription("The current audio sample rate")]
        public static float sampleRate => LuaManager.Instance.ScriptedWaveformSampleRate;

        [LuaDocsDescription("The current duration of the audio buffer")]
        public static float duration => (1f / sampleRate) * VisualizerManager.m_Instance.FFTSize;

        [LuaDocsDescription("Enables scripted access to the audio visualization buffer")]
        [LuaDocsExample("Visualizer.EnableScripting()")]
        public static void EnableScripting() => AudioCaptureManager.m_Instance.EnableScripting();


        [LuaDocsDescription("Disables scripted access to the audio visualization buffer")]
        [LuaDocsExample("Visualizer.DisableScripting()")]
        public static void DisableScripting() => AudioCaptureManager.m_Instance.DisableScripting();

        [LuaDocsDescription("Passes the given waveform data to the audio visualizer")]
        [LuaDocsExample("Visualizer.SetWaveform(data)")]
        [LuaDocsParameter("data", "An array of numbers representing the waveform")]
        public static void SetWaveform(float[] data) => VisualizerManager.m_Instance.ProcessAudio(data, LuaManager.Instance.ScriptedWaveformSampleRate);

        [LuaDocsDescription("Passes the given FFT data to the audio visualizer")]
        [LuaDocsExample("Visualizer.SetFft(data1, data2, data3, data4)")]
        [LuaDocsParameter("data1", "An array of numbers representing first FFT band")]
        [LuaDocsParameter("data2", "An array of numbers representing second FFT band")]
        [LuaDocsParameter("data3", "An array of numbers representing third FFT band")]
        [LuaDocsParameter("data4", "An array of numbers representing fourth FFT band")]
        public static void SetFft(float[] data1, float[] data2, float[] data3, float[] data4) => VisualizerManager.m_Instance.InjectScriptedFft(data1, data2, data3, data4);

        [LuaDocsDescription("Passes the given beat data to the audio visualizer")]
        [LuaDocsExample("Visualizer.SetBeats(x, y, z, w)")]
        [LuaDocsParameter("x", "The first beat value")]
        [LuaDocsParameter("y", "The second beat value")]
        [LuaDocsParameter("z", "The third beat value")]
        [LuaDocsParameter("w", "The fourth beat value")]
        public static void SetBeats(float x, float y, float z, float w) => VisualizerManager.m_Instance.InjectScriptedBeats(new Vector4(x, y, z, w));

        [LuaDocsDescription("Passes the given beat accumulator data to the audio visualizer")]
        [LuaDocsExample("Visualizer.SetBeatAccumulators(x, y, z, w)")]
        [LuaDocsParameter("x", "The first beat accumulator value")]
        [LuaDocsParameter("y", "The second beat accumulator value")]
        [LuaDocsParameter("z", "The third beat accumulator value")]
        [LuaDocsParameter("w", "The fourth beat accumulator value")]
        public static void SetBeatAccumulators(float x, float y, float z, float w) => VisualizerManager.m_Instance.InjectScriptedBeatAccumulator(new Vector4(x, y, z, w));

        [LuaDocsDescription("Passes the given band peak data to the audio visualizer")]
        [LuaDocsExample("Visualizer.SetBandPeak(0.5)")]
        [LuaDocsParameter("peak", "The peak value")]
        public static void SetBandPeak(float peak) => VisualizerManager.m_Instance.InjectBandPeaks(new Vector4(0, peak, 0, 0));
    }
}
