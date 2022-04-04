using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Logging;
using ImGuiNET;
using TextToTalk.Lexicons;
using TextToTalk.Lexicons.Updater;

namespace TextToTalk.UI.Dalamud.Lexicons;

public class LexiconRepositorySubwindow
{
    private readonly LexiconManager lexiconManager;
    private readonly LexiconRepository lexiconRepository;

    public LexiconRepositorySubwindow(LexiconManager lm, LexiconRepository lr)
    {
        this.lexiconManager = lm;
        this.lexiconRepository = lr;
    }

    public void Draw(ref bool visible)
    {
        ImGui.SetNextWindowSize(new Vector2(520, 480), ImGuiCond.FirstUseEver);
        ImGui.Begin("Lexicon Repository", ref visible);
        {
        }
        ImGui.End();
    }

    private void LoadRemoteLexicons()
    {
        var packageFolders = Directory.EnumerateDirectories(this.lexiconRepository.CachePath);
        foreach (var lexiconDir in packageFolders)
        {
            // TODO: Read the local package metadata file instead of doing this
            var files = Directory.EnumerateFiles(lexiconDir);
            var packageName = Path.GetDirectoryName(lexiconDir);
            foreach (var file in files.Where(f => !f.EndsWith(".yml")))
            {
                var filename = Path.GetFileName(file);
                var lexiconId = GetLexiconId(packageName, filename);
                using var lexiconData = File.OpenRead(file);
                try
                {
                    PluginLog.Log($"Adding lexicon \"{lexiconId}\"");
                    this.lexiconManager.AddLexicon(lexiconData, lexiconId);
                }
                catch (Exception e)
                {
                    PluginLog.LogError(e, "Failed to load lexicon.");
                }
            }
        }
    }

    private async Task DownloadRemoteLexicon(string packageName)
    {
        // Fetch lexicon file list
        var package = this.lexiconRepository.GetPackage(packageName);
        var packageInfo = await package.GetPackageInfo();

        // Download each file and load it
        foreach (var file in packageInfo.Files)
        {
            var lexiconId = GetLexiconId(packageName, file);
            await using var lexiconData = await package.GetPackageFile(file);
            try
            {
                PluginLog.Log($"Adding lexicon \"{lexiconId}\"");
                this.lexiconManager.AddLexicon(lexiconData, lexiconId);
            }
            catch (Exception e)
            {
                PluginLog.LogError(e, "Failed to load lexicon.");
            }
        }
    }

    private async Task<IList<LexiconPackageInfo>> CheckRemoteLexiconUpdates()
    {
        var toUpdate = new List<LexiconPackageInfo>();
        var items = await this.lexiconRepository.FetchPackages();
        foreach (var item in items)
        {
            var packageName = LexiconPackage.GetInternalName(item.Path);
            var package = this.lexiconRepository.GetPackage(packageName);

            // Only check updates for installed packages
            if (!await package.IsInstalled()) continue;

            var info = await package.GetPackageInfo();
            foreach (var file in info.Files)
            {
                if (await package.HasUpdate(file))
                {
                    toUpdate.Add(info);
                    break;
                }
            }
        }

        return toUpdate;
    }

    private async Task UpdateRemoteLexicon(string packageName)
    {
        var package = this.lexiconRepository.GetPackage(packageName);
        var packageInfo = await package.GetPackageInfo();

        // Download each updated file and (re)load it
        foreach (var file in packageInfo.Files)
        {
            if (!await package.HasUpdate(file)) continue;

            var lexiconId = GetLexiconId(packageName, file);
            await using var lexiconData = await package.GetPackageFile(file);
            this.lexiconManager.RemoveLexicon(lexiconId);
            this.lexiconManager.AddLexicon(lexiconData, lexiconId);
        }
    }

    private void UninstallRemoteLexicon(string packageName)
    {
        // TODO: Read the local package metadata file instead of doing this
        var lexiconDir = Path.Combine(this.lexiconRepository.CachePath, packageName);
        var files = Directory.EnumerateFiles(lexiconDir);
        foreach (var file in files.Where(f => !f.EndsWith(".yml")))
        {
            var filename = Path.GetFileName(file);
            var lexiconId = GetLexiconId(packageName, filename);
            this.lexiconManager.RemoveLexicon(lexiconId);
        }

        this.lexiconRepository.RemovePackage(packageName);
    }

    private static string GetLexiconId(string packageName, string filename)
    {
        return $"{packageName}.{filename}";
    }
}