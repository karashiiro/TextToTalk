using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace TextToTalk
{
    public static class VoiceUnlockerRunner
    {
        public static bool Execute()
        {
            try
            {
                var assemblyPath = Path.Combine(Assembly.GetExecutingAssembly().Location, "..");
                var applicationPath = Path.Combine(assemblyPath, "VoiceUnlocker.exe");
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