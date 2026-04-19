using System.IO;
using UnityEditor;

namespace AIInterrogation.Editor
{
    public static class WindowsBuildScript
    {
        private const string OutputDirectory = "Builds/AI Interrogation Windows";
        private const string OutputExecutable = "AI Interrogation.exe";

        public static void BuildWindows()
        {
            Directory.CreateDirectory(OutputDirectory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Interrogation.unity" },
                locationPathName = Path.Combine(OutputDirectory, OutputExecutable),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new System.Exception("Windows build failed: " + report.summary.result);
            }
        }
    }
}
