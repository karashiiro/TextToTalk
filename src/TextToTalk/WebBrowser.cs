using System.Diagnostics;

namespace TextToTalk
{
    public class WebBrowser
    {
        public static void Open(string url)
        {
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true,
            });
        }
    }
}