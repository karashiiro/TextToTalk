using Dalamud.Logging;
using System;
using System.Windows.Forms;

namespace TextToTalk
{
    public static class OpenFile
    {
        public static string FileSelect()
        {
            using var openFileDialog = new OpenFileDialog
            {
                Filter = "PLS files (*.pls)|*.pls|XML files (*.xml)|*.xml|All files (*.*)|*.*",
                AddExtension = true,
                AutoUpgradeEnabled = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                RestoreDirectory = true,
                ShowHelp = true,
            };

            try
            {
                if (openFileDialog.ShowDialog(null) != DialogResult.OK)
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                PluginLog.LogError(e, e.Message);
                return null;
            }

            return openFileDialog.FileName;
        }
    }
}