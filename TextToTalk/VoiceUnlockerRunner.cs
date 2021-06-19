using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace TextToTalk
{
    public static class VoiceUnlockerRunner
    {
        public static bool Execute()
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

            return true;
        }
    }
}