using System.Diagnostics;

namespace TiltBrush
{
    public static class GitUtils
    {
        public static string GetGitBranchName()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("git", "rev-parse --abbrev-ref HEAD")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit();
                string output = process.StandardOutput.ReadToEnd().Trim();
                return output;
            }
        }
    }
}
