using System.Diagnostics;
using System.IO;
using System.Reflection;
using Dalamud.Plugin;

namespace TextToTalk
{
    public static class VoiceUnlockerRunner
    {
        public static bool Execute()
        {
            var assemblyPath = Path.Combine(Assembly.GetExecutingAssembly().Location, "..");
            var applicationPath = Path.Combine(assemblyPath, "VoiceUnlocker.exe");
            using var application = Process.Start(new ProcessStartInfo(applicationPath)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            if (application == null)
            {
                // Failed to start
                return false;
            }

            application.ErrorDataReceived += OnErrorReceived;
            application.OutputDataReceived += OnOutputReceived;

            application.WaitForExit();

            return true;
        }

        private static void OnErrorReceived(object sender, DataReceivedEventArgs e)
        {
            PluginLog.LogError(e.Data);
        }

        private static void OnOutputReceived(object sender, DataReceivedEventArgs e)
        {
            PluginLog.Log(e.Data);
        }
    }
}