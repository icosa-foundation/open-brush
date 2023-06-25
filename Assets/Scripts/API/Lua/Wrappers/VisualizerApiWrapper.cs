using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [LuaDocsDescription("Settings and controls for audio visualization mode")]
    [MoonSharpUserData]
    public static class VisualizerApiWrapper
    {
        public static float sampleRate => LuaManager.Instance.ScriptedWaveformSampleRate;
        public static float duration => (1f / sampleRate) * VisualizerManager.m_Instance.FFTSize;
        public static void EnableScripting(string name) => AudioCaptureManager.m_Instance.EnableScripting();
        public static void DisableScripting() => AudioCaptureManager.m_Instance.DisableScripting();
        public static void SetWaveform(float[] data) => VisualizerManager.m_Instance.ProcessAudio(data, LuaManager.Instance.ScriptedWaveformSampleRate);
        public static void SetFft(float[] data1, float[] data2, float[] data3, float[] data4) => VisualizerManager.m_Instance.InjectScriptedFft(data1, data2, data3, data4);
        public static void SetBeats(float x, float y, float z, float w) => VisualizerManager.m_Instance.InjectScriptedBeats(new Vector4(x, y, z, w));
        public static void SetBeatAccumulators(float x, float y, float z, float w) => VisualizerManager.m_Instance.InjectScriptedBeatAccumulator(new Vector4(x, y, z, w));
        public static void SetBandPeak(float peak) => VisualizerManager.m_Instance.InjectBandPeaks(new Vector4(0, peak, 0, 0));
    }
}
