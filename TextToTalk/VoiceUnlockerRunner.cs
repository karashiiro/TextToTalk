using Dalamud.Logging;
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
                using var application = Process.Start(applicationPath);

                if (application == null)
                {
                    // Failed to start
                    return false;
                }

                application.WaitForExit();
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "VoiceUnlocker failed to start.");
                return false;
            }

            return true;
        }
    }
}