using System;
using System.Windows.Forms;
using Dalamud.Plugin;

namespace TextToTalk
{
    public class FileDialog
    {
        public string SelectedFile { get; private set; }

        private bool dialogActive;

        public void ClearSelectedFile()
        {
            SelectedFile = "";
        }

        public void StartFileSelect()
        {
            if (this.dialogActive)
                return;
            this.dialogActive = true;

            using var saveFileDialogue = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                AddExtension = true,
                AutoUpgradeEnabled = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                RestoreDirectory = true,
                ShowHelp = true,
            };

            try
            {
                if (saveFileDialogue.ShowDialog(null) != DialogResult.OK)
                {
                    this.dialogActive = false;
                    return;
                }
            }
            catch (Exception e)
            {
                PluginLog.LogError(e, e.Message);
                this.dialogActive = false;
                return;
            }

            SelectedFile = saveFileDialogue.FileName;

            this.dialogActive = false;
        }
    }
}