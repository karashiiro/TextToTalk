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
                using var application = Process.Start(new ProcessStartInfo(GetVoiceUnlockerPath())
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

        private string GetVoiceUnlockerPath()
        {
            var assemblyPath = Path.Combine(this.assemblyLocation, "..");

            var applicationPath1 = Path.Combine(assemblyPath, "VoiceUnlocker", "VoiceUnlocker.exe");
            if (File.Exists(applicationPath1)) return applicationPath1;

            var applicationPath2 = Path.Combine(assemblyPath, "VoiceUnlocker.exe");
            if (File.Exists(applicationPath2)) return applicationPath2;

            throw new FileNotFoundException("The VoiceUnlocker executable could not be found.", applicationPath1);
        }
    }
}