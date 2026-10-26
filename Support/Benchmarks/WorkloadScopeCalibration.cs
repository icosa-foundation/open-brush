using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
namespace TiltBrush.Benchmarks
{
    public static class WorkloadScopeCalibration
    {
        public static string Run(string output)
        {
            if (UnityEditor.SessionState.GetBool("BatchWorkloadRunning", false)) throw new InvalidOperationException("Workload still running");
            const int count = 1000000;
            var plain = new double[7]; var scoped = new double[7]; long checksum = 0;
            bool enabled = BatchWorkloadCounters.Enabled;
            try
            {
                BatchWorkloadCounters.Enabled = true;
                for (int sample = -2; sample < 7; sample++)
                {
                    long start = Stopwatch.GetTimestamp();
                    for (int i = 0; i < count; i++) checksum += i;
                    double p = (Stopwatch.GetTimestamp() - start) * 1e9 / Stopwatch.Frequency / count;
                    start = Stopwatch.GetTimestamp();
                    for (int i = 0; i < count; i++) using (BatchWorkloadCounters.Measure(0)) checksum += i;
                    double s = (Stopwatch.GetTimestamp() - start) * 1e9 / Stopwatch.Frequency / count;
                    if (sample >= 0) { plain[sample] = p; scoped[sample] = s; }
                }
            }
            finally { BatchWorkloadCounters.Enabled = enabled; BatchWorkloadCounters.Reset(); }
            var result = new { utc = DateTime.UtcNow.ToString("o"), count, plainNanoseconds = plain, scopedNanoseconds = scoped, estimatedAddedNanoseconds = scoped.Zip(plain, (s, p) => s - p).ToArray(), checksum, scope = "Empty diagnostic scope vs same checksum loop; indicative calibration, not subtracted from workload results." };
            var serializer = Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject", new[] { typeof(object) });
            File.WriteAllText(output, (string)serializer.Invoke(null, new object[] { result }));
            return $"Median added scope cost: {result.estimatedAddedNanoseconds.OrderBy(x => x).ElementAt(3):F1} ns";
        }
    }
}
