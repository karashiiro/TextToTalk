using System;
using Dalamud.Interface.ImGuiFileDialog;
using ImGuiNET;
using TextToTalk.Backends.GoogleCloud;

namespace TextToTalk.UI.GoogleCloud;

public class CredentialsComponent
{
    private readonly GoogleCloudClient client;
    private readonly PluginConfiguration config;
    private FileDialog? fileDialog;

    public CredentialsComponent(GoogleCloudClient client, PluginConfiguration config)
    {
        this.client = client;
        this.config = config;
    }

    public void Draw()
    {
        // Draw the file selection dialog
        if (this.fileDialog != null)
        {
            this.fileDialog.Draw();

            if (this.fileDialog.GetIsOk())
            {
                this.fileDialog.Hide();

                var filePath = this.fileDialog.GetResults()[0];
                if (string.IsNullOrEmpty(filePath)) return;

                this.config.GoogleCreds = filePath;
                this.client.Init(filePath);
                this.config.Save();

                this.fileDialog = null;
            }
        }

        SelectPathButton();
    }

    private void SelectPathButton()
    {
        if (ImGui.Button($"Open Google Credentials"))
        {
            this.fileDialog = new FileDialog("SelectGoogleCredentialsFileDialog", "Open a file...",
                "JSON files{.json},.*",
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "", "", 1, false,
                ImGuiFileDialogFlags.None);
            this.fileDialog.Show();
        }
    }
}