// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
// ReSharper disable NotAccessedField.Local

namespace TiltBrush
{
    /// Responsible for starting and stopping profiling. Will summarize performance to the
    /// console at the end. Profiling options are set in the Tilt Brush.cfg file (App.UserConfig).
    public class ProfilingManager
    {

        [Serializable]
        public enum Mode
        {
            Standard, // Standard Unity Profiling + frame time stats.
            Light,    // Frame time stats only.
            Deep,     // Deep profiling.
        }

        // Stores information about a single frame's worth of profiling data for a function.
        private struct FrameData
        {
            public float elapsedNanoseconds;
            public int numCalls;
        }

        // Stores profiling information for a function.
        private struct Sample
        {
            public string name;
            public Recorder recorder;
            public List<FrameData> frameData;
        }

        private static ProfilingManager m_Instance;
        private const string kPerfLogPrefix = "[OB_PERF]";
        private bool m_Profiling;
        private Mode m_Mode;
        private List<float> m_FrameTimes;
        private List<float> m_GpuUtilizationPercentages;
        private string m_ActiveProfileName;
        private const int k_NumFrames = 75 * 6; // enough space for six seconds of samples.
        private Coroutine m_UpdateCoroutine;
        private int[] m_ValidFramerates = { 90, 75, 60, 40, 1 };
        private List<Sample> m_Samples = new List<Sample>();

        public static ProfilingManager Instance
        {
            get
            {
                if (m_Instance == null)
                {
                    m_Instance = new ProfilingManager();
                }
                return m_Instance;
            }
        }

        public bool IsProfiling
        {
            get { return m_Profiling; }
        }

        public void StartProfiling(Mode mode, string profileName = null)
        {
            Debug.Assert(m_Profiling == false);
            m_Mode = mode;
            m_ActiveProfileName = string.IsNullOrEmpty(profileName)
                ? App.UserConfig.Profiling.ProfileName
                : profileName;
            switch (m_Mode)
            {
                case Mode.Standard:
                case Mode.Deep:
                    string filename = GetProfilingFilename();
                    Debug.LogFormat("Writing Profile to {0}", filename);
                    Profiler.logFile = filename;
                    Profiler.enableBinaryLog = true;
                    Profiler.enabled = true;
                    break;
                default:
                    break;
            }
            // Fetch all the profile recorders for the functions we're interesting in profiling.
            if (App.UserConfig.Profiling.ProfilingFunctions != null)
            {
                m_Samples = new List<Sample>(App.UserConfig.Profiling.ProfilingFunctions.Length);
                foreach (string functionName in App.UserConfig.Profiling.ProfilingFunctions)
                {
                    Sample sample = new Sample();
                    sample.name = functionName;
                    sample.recorder = Recorder.Get(functionName);
                    if (!sample.recorder.isValid)
                    {
                        Debug.LogWarningFormat("Could not get recorder for {0} function.", functionName);
                        continue;
                    }
                    sample.recorder.enabled = true;
                    sample.frameData = new List<FrameData>(k_NumFrames);
                    m_Samples.Add(sample);
                }
            }
            else
            {
                m_Samples = new List<Sample>();
            }

            m_FrameTimes = new List<float>(k_NumFrames);
            m_GpuUtilizationPercentages = new List<float>(k_NumFrames);
            m_Profiling = true;
            m_UpdateCoroutine = App.Instance.StartCoroutine(Update());
        }

        public void StopProfiling()
        {
            Debug.Assert(m_Profiling);
            Debug.Log("Stopping Profiling.");
            switch (m_Mode)
            {
                case Mode.Standard:
                case Mode.Deep:
                    Profiler.enableBinaryLog = false;
                    Profiler.enabled = false;
                    Profiler.logFile = null;
                    break;
                default:
                    break;
            }
            foreach (var sample in m_Samples)
            {
                sample.recorder.enabled = false;
            }
            if (m_UpdateCoroutine != null)
            {
                App.Instance.StopCoroutine(m_UpdateCoroutine);
            }
            m_Profiling = false;
            OutputStats();
            m_Samples = new List<Sample>();
            m_ActiveProfileName = null;
        }

        private string GetProfilingFilename()
        {
            string filename = App.UserConfig.Profiling.ProfileFilename;
            if (string.IsNullOrEmpty(filename))
            {
                string dateTime = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                filename = string.Format("Profile_{0}.data", dateTime);
            }
            return filename;
        }

        // Grabs the frame times and any functions being profiled.
        [SuppressMessage("ReSharper", "IteratorNeverReturns")]
        private IEnumerator Update()
        {
            while (true)
            {
                m_FrameTimes.Add(Time.unscaledDeltaTime * 1000f);
                if (App.VrSdk != null)
                {
                    float gpuUtilization = App.VrSdk.GetGpuUtilization() * 100f;
                    if (gpuUtilization > 0f)
                    {
                        m_GpuUtilizationPercentages.Add(gpuUtilization);
                    }
                }
                yield return null;
                foreach (var sample in m_Samples)
                {
                    FrameData frameData = new FrameData();
                    frameData.elapsedNanoseconds = sample.recorder.elapsedNanoseconds;
                    frameData.numCalls = sample.recorder.sampleBlockCount;
                    sample.frameData.Add(frameData);
                }
            }
        }

        // Calculates statistics from the profiling data and outputs it to the Unity log.
        private void OutputStats()
        {
            Statistics.Summary stats = new Statistics.Summary(m_FrameTimes.ToArray());
            int numBatches = App.ActiveCanvas.BatchManager.CountBatches();
            int numTriangles = App.ActiveCanvas.BatchManager.CountAllBatchTriangles();

            // Timing isn't always exact, which is why the 0.5ms extra is added on.
            float[] frameLimits = m_ValidFramerates.Select(x => 1000f / x + 0.5f).ToArray();
            int[] frameRateBuckets = new int[m_ValidFramerates.Length];
            foreach (float frameTime in m_FrameTimes)
            {
                for (int i = 0; i < frameRateBuckets.Length; ++i)
                {
                    if (frameTime < frameLimits[i])
                    {
                        frameRateBuckets[i]++;
                        break;
                    }
                }
            }

            float percentScale = 100f / m_FrameTimes.Count;
            float[] frameRatePercentages = frameRateBuckets.Select(x => x * percentScale).ToArray();

            var profileName = ActiveProfileName;
            var humanName = SaveLoadScript.m_Instance.GetLastFileHumanName();
            var fileName = System.IO.Path.GetFileNameWithoutExtension(
                SaveLoadScript.m_Instance.SceneFile.FullPath);

            StringBuilder message = new StringBuilder();
            string file = string.IsNullOrEmpty(profileName)
                ? SaveLoadScript.m_Instance.GetLastFileHumanName()
                : profileName;
            message.AppendLine("TBProfile: START");
#if UNITY_EDITOR
            string branch = GitUtils.GetGitBranchName();
            message.AppendLine($"Git branch: {branch}");
#endif
            message.AppendLine($"Build: {App.GetStartupString()}");
            message.AppendLine($"Profile name: {profileName} Filename: {fileName} Human name: {humanName}");
            message.AppendLine(BuildComparisonLine(stats, numBatches, numTriangles));

            if (App.UserConfig.Profiling.PerfgateOutput)
            {
                PerfgateOutput(message, m_FrameTimes.ToArray(), numBatches, numTriangles, file);
            }
            else
            {
                if (App.UserConfig.Profiling.Csv)
                {
                    CsvOutput(message, stats, numBatches, numTriangles, frameRatePercentages, file);
                }
                else
                {
                    HumanReadableOutput(message, stats, numBatches, numTriangles, frameRatePercentages);
                }
            }
            message.AppendLine("TBProfile: END");

            Debug.Log(message.ToString());

            string path = Path.Join(
                App.UserPath(),
                $"{GetProfilingFilename()}_summary.txt");
            File.WriteAllText(path, message.ToString());
        }

        private string BuildComparisonLine(Statistics.Summary stats, int numBatches, int numTriangles)
        {
            float[] frameTimes = m_FrameTimes.ToArray();
            float meanFps = stats.Mean > 0f ? 1000f / stats.Mean : 0f;
            float medianFps = stats.Median > 0f ? 1000f / stats.Median : 0f;
            string gpuSummary = BuildGpuSummary();
            string profileName = ActiveProfileName;

            return string.Format(
                "{0} summary profile=\"{1}\" build=\"{2}\" platform=\"{3}\" mobile={4} quality={5} frames={6} mean_ms={7:F2} median_ms={8:F2} p90_ms={9:F2} p95_ms={10:F2} p99_ms={11:F2} max_ms={12:F2} mean_fps={13:F1} median_fps={14:F1} at_or_above_90fps_pct={15:F1} at_or_above_75fps_pct={16:F1} at_or_above_72fps_pct={17:F1} at_or_above_60fps_pct={18:F1} batches={19} tris={20}{21}",
                kPerfLogPrefix,
                EscapeMetricValue(profileName),
                EscapeMetricValue(App.GetStartupString()),
                Application.platform,
                App.Config.IsMobileHardware,
                QualitySettings.GetQualityLevel(),
                m_FrameTimes.Count,
                stats.Mean,
                stats.Median,
                Percentile(frameTimes, 0.90f),
                Percentile(frameTimes, 0.95f),
                Percentile(frameTimes, 0.99f),
                stats.Max,
                meanFps,
                medianFps,
                PercentageAtOrAboveFrameRate(frameTimes, 90),
                PercentageAtOrAboveFrameRate(frameTimes, 75),
                PercentageAtOrAboveFrameRate(frameTimes, 72),
                PercentageAtOrAboveFrameRate(frameTimes, 60),
                numBatches,
                numTriangles,
                gpuSummary);
        }

        private string ActiveProfileName
        {
            get
            {
                return string.IsNullOrEmpty(m_ActiveProfileName)
                    ? App.UserConfig.Profiling.ProfileName
                    : m_ActiveProfileName;
            }
        }

        private string BuildGpuSummary()
        {
            if (m_GpuUtilizationPercentages == null || m_GpuUtilizationPercentages.Count == 0)
            {
                return " gpu_util_mean_pct=0.0 gpu_util_median_pct=0.0 gpu_util_samples=0";
            }

            Statistics.Summary gpuStats =
                new Statistics.Summary(m_GpuUtilizationPercentages.ToArray());
            return string.Format(
                " gpu_util_mean_pct={0:F1} gpu_util_median_pct={1:F1} gpu_util_samples={2}",
                gpuStats.Mean,
                gpuStats.Median,
                m_GpuUtilizationPercentages.Count);
        }

        private static float PercentageAtOrAboveFrameRate(float[] frameTimes, int frameRate)
        {
            if (frameTimes.Length == 0)
            {
                return 0f;
            }

            float frameLimit = 1000f / frameRate + 0.5f;
            int framesAtOrAbove = frameTimes.Count(x => x < frameLimit);
            return framesAtOrAbove * 100f / frameTimes.Length;
        }

        private static float Percentile(float[] data, float percentile)
        {
            if (data.Length == 0)
            {
                return 0f;
            }

            float[] sortedData = new float[data.Length];
            data.CopyTo(sortedData, 0);
            Array.Sort(sortedData);

            int index = Mathf.Clamp(
                Mathf.CeilToInt(percentile * sortedData.Length) - 1,
                0,
                sortedData.Length - 1);
            return sortedData[index];
        }

        private static string EscapeMetricValue(string value)
        {
            return string.IsNullOrEmpty(value) ? "" : value.Replace("\"", "'");
        }

        private void HumanReadableOutput(StringBuilder output, Statistics.Summary stats, int numBatches,
                                         int numTriangles, float[] frameRatePercentages)
        {
            output.AppendFormat(
                "TBProfile: Frames: {0}  Min: {1:F2}  Median: {2:F2}  Max:{3:F2}  StdDev:{4:F2} StdDev%:{5:F1} Batches:{6} Tris:{7}\n",
                m_FrameTimes.Count, stats.Min, stats.Median, stats.Max, stats.StandardDeviation,
                stats.StandardDeviationPcOfMedian, numBatches, numTriangles);

            IEnumerable<string> sections = Enumerable.Range(0, m_ValidFramerates.Length).Select(i =>
                string.Format("{0}fps: {1:F1}%", m_ValidFramerates[i], frameRatePercentages[i]));
            output.Append("TBProfile: ");
            output.AppendLine(string.Join("  ", sections.ToArray()));

            foreach (var sample in m_Samples)
            {
                var times = sample.frameData.Select(x => x.elapsedNanoseconds / 1000000f).Where(x => x > 0);
                stats = new Statistics.Summary(times.ToArray());
                output.AppendFormat(
                    "Profile: {0}: Frames: {1} Min: {2:F2}  Median: {3:F2}  Max:{4:F2}  StdDev:{5:F2}  StdDev%:{6:F1}\n",
                    sample.name, sample.frameData.Count, stats.Min, stats.Median, stats.Max,
                    stats.StandardDeviation, stats.StandardDeviationPcOfMedian);
            }
        }

        private void CsvOutput(StringBuilder output, Statistics.Summary stats, int numBatches,
                               int numTriangles, float[] frameRatePercentages, string filename)
        {
            output.AppendFormat(
                "TBProfile: {8}, {0}, {1:F2}, {2:F2}, {3:F2}, {4:F2}, {5:F1}, {6}, {7}, ",
                m_FrameTimes.Count, stats.Min, stats.Median, stats.Max, stats.StandardDeviation,
                stats.StandardDeviationPcOfMedian, numBatches, numTriangles, filename);

            IEnumerable<string> sections = frameRatePercentages.Select(x => x.ToString("F1"));
            output.AppendLine(string.Join(", ", sections.ToArray()));
        }

        private void PerfgateOutput(StringBuilder output, float[] frameTimes, int numBatches,
                                    int numTriangles, string filename)
        {
            output.AppendFormat("TBProfile: Filename {0} Batches {1} Triangles {2} Frametimes ",
                filename, numBatches, numTriangles);
            output.AppendLine(string.Join(", ", frameTimes.Select(x => x.ToString("F4")).ToArray()));
        }
    }
} // namespace TiltBrush
