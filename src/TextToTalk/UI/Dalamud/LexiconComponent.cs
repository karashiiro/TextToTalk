using Dalamud.Interface;
using Dalamud.Logging;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using TextToTalk.Lexicons;
using TextToTalk.Lexicons.Updater;
using TextToTalk.UI.Native;

namespace TextToTalk.UI.Dalamud;

public class LexiconComponent
{
    private static readonly Vector4 HintColor = new(0.7f, 0.7f, 0.7f, 1.0f);
    private static readonly Vector4 Red = new(1, 0, 0, 1);

    private readonly List<Exception> lexiconRemoveExceptions = new();
    private Exception lexiconAddException;
    private bool lexiconAddSucceeded;

    private readonly PluginConfiguration config;
    private readonly LexiconManager lexiconManager;
    private readonly LexiconRepository lexiconRepository;

    private readonly Func<IList<string>> getLexiconList;

    public LexiconComponent(LexiconManager lm, LexiconRepository lr, PluginConfiguration config, Func<IList<string>> getLexiconList)
    {
        this.lexiconManager = lm;
        this.lexiconRepository = lr;
        this.config = config;
        this.getLexiconList = getLexiconList;
    }

    public void Draw()
    {
        ImGui.Text("Lexicons");

        ImGui.TextColored(HintColor, "Looking for more lexicons? Have a look at our community lexicons list!");
        if (ImGui.Button("Wiki"))
        {
            WebBrowser.Open("https://github.com/karashiiro/TextToTalk/wiki/Community-lexicons");
        }

        ImGui.Spacing();

        var lexicons = this.getLexiconList.Invoke();
        for (var i = 0; i < lexicons.Count; i++)
        {
            // Remove if no longer existent
            if (!File.Exists(lexicons[i]))
            {
                lexicons[i] = "";
            }

            // Editing options
            var lexiconPath = lexicons[i];
            var lexiconPathBuf = Encoding.UTF8.GetBytes(lexiconPath);
            ImGui.InputText($"##TTTLexiconText{i}", lexiconPathBuf, (uint)lexiconPathBuf.Length, ImGuiInputTextFlags.ReadOnly);

            if (!string.IsNullOrEmpty(lexicons[i]))
            {
                ImGui.SameLine();
                var deferred = LexiconRemoveButton(i, lexiconPath);

                if (this.lexiconRemoveExceptions[i] != null)
                {
                    ImGui.TextColored(Red, this.lexiconRemoveExceptions[i].Message);
                }

                deferred?.Invoke();
            }
        }

        LexiconAddButton();

        if (this.lexiconAddSucceeded)
        {
            ImGui.SameLine();

            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.Text(FontAwesomeIcon.CheckCircle.ToIconString());
            ImGui.PopFont();
        }

        if (this.lexiconAddException != null)
        {
            ImGui.TextColored(Red, this.lexiconAddException.Message);
        }
    }

    /// <summary>
    /// Draws the remove lexicon button. Returns an action that should be called after all other operations
    /// on the provided index are done.
    /// </summary>
    private Action LexiconRemoveButton(int i, string lexiconPath)
    {
        var lexicons = this.getLexiconList.Invoke();

        if (this.lexiconRemoveExceptions.Count < lexicons.Count)
        {
            this.lexiconRemoveExceptions.Add(null);
        }

        Action deferred = null;

        ImGui.PushFont(UiBuilder.IconFont);
        if (ImGui.Button($"{FontAwesomeIcon.TimesCircle.ToIconString()}##TTTLexiconRemove{i}"))
        {
            try
            {
                this.lexiconRemoveExceptions[i] = null;
                this.lexiconManager.RemoveLexicon(lexiconPath);

                // This is ugly but it works
                deferred = () =>
                {
                    lexicons.RemoveAt(i);
                    this.lexiconRemoveExceptions.RemoveAt(i);

                    this.config.Save();
                };
            }
            catch (Exception e)
            {
                PluginLog.LogError(e, "Failed to remove lexicon.");
                this.lexiconRemoveExceptions[i] = e;
            }

            lexicons[i] = "";
        }
        ImGui.PopFont();

        return deferred;
    }

    private void LexiconAddButton()
    {
        if (ImGui.Button("Open Lexicon##TTTLexiconAdd"))
        {
            this.lexiconAddException = null;
            this.lexiconAddSucceeded = false;

            var lexicons = this.getLexiconList.Invoke();

            _ = Task.Run(() =>
            {
                var filePath = OpenFile.FileSelect();
                if (string.IsNullOrEmpty(filePath)) return;

                try
                {
                    PluginLog.Log($"Adding lexicon \"{filePath}\"");
                    this.lexiconManager.AddLexicon(filePath);
                    lexicons.Add(filePath);
                    this.config.Save();
                    this.lexiconAddSucceeded = true;
                }
                catch (Exception e)
                {
                    PluginLog.LogError(e, "Failed to load lexicon.");
                    this.lexiconAddException = e;
                }
            });
        }
    }

    private async Task DownloadRemoteLexicon(string packageName)
    {
        // Fetch lexicon file list
        var package = this.lexiconRepository.GetPackage(packageName);
        var packageInfo = await package.GetPackageInfo();

        // Download each file
    }
}