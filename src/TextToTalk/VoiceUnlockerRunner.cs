using System;
using System.Diagnostics;
using System.IO;

namespace TextToTalk
{
    public class VoiceUnlockerRunner
    {
        private readonly string assemblyLocation;

        public VoiceUnlockerRunner(string assemblyLocation)
        {
            this.assemblyLocation = assemblyLocation;
        }

        public bool Execute()
        {
            try
            {
                var assemblyPath = Path.Combine(this.assemblyLocation, "..");
                var applicationPath = Path.Combine(assemblyPath, "VoiceUnlocker", "VoiceUnlocker.exe");
                using var application = Process.Start(new ProcessStartInfo(applicationPath)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                });

                if (application == null)
                {
                    // Failed to start
                    return false;
                }

                application.WaitForExit();
            }
            catch (Exception e)
            {
                DetailedLog.Error(e, "VoiceUnlocker failed to start.");
                return false;
            }

            return true;
        }
    }
}