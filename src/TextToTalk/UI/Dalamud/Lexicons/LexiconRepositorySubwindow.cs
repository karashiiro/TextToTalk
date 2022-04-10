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

    private (LexiconPackageInfo, LexiconPackageStatus) selectedPackage;

    private readonly object rpLock;
    private IList<(LexiconPackageInfo, LexiconPackageStatus)> remotePackages;
    private bool remotePackagesLoading;
    private bool remotePackagesLoaded;

    public LexiconRepositorySubwindow(LexiconManager lm, LexiconRepository lr)
    {
        this.lexiconManager = lm;
        this.lexiconRepository = lr;

        this.rpLock = true;

        Task.Factory.StartNew(LoadInstalledLexicons);
    }

    public void Draw(ref bool visible)
    {
        ImGui.SetNextWindowSize(new Vector2(520, 480), ImGuiCond.FirstUseEver);
        ImGui.Begin("Lexicon Repository##TextToTalkLexiconRepositorySubwindow", ref visible);
        {
            if (!this.remotePackagesLoaded && !this.remotePackagesLoading)
            {
                // Fetch the list of lexicon packages
                PluginLog.Log("Fetching lexicon package list...");
                _ = LoadPackageInfo();
            }
            else if (ImGui.BeginTable("##LexiconRepoList", 2, ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("Lexicon", ImGuiTableColumnFlags.None, 380f);
                ImGui.TableSetupColumn("Author", ImGuiTableColumnFlags.None, 120f);
                ImGui.TableHeadersRow();

                if (this.remotePackages != null)
                {
                    lock (this.rpLock)
                    {
                        foreach (var remotePackage in this.remotePackages)
                        {
                            var (selectedPackageData, selectedPackageStatus) = this.selectedPackage;
                            var (packageData, packageStatus) = remotePackage;

                            ImGui.TableNextRow();

                            ImGui.TableSetColumnIndex(0);
                            if (ImGui.Selectable($"##LexiconRepoList_{packageData.InternalName}", selectedPackageData == packageData, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap, Vector2.Zero))
                            {
                                this.selectedPackage = remotePackage;
                            }
                            ImGui.SameLine(0f, 0f);
                            ImGui.Text(packageData.Name); // TODO: Show something different if it's installed or has an update

                            ImGui.TableSetColumnIndex(1);
                            ImGui.Text(packageData.Author);
                        }
                    }
                }
                
                ImGui.EndTable();
            }

            lock (this.rpLock)
            {
                var (selectedPackageData, selectedPackageStatus) = this.selectedPackage;
                if (selectedPackageData != null)
                {
                    ImGui.Text($"{selectedPackageData.Name} by {selectedPackageData.Author}");
                    ImGui.TextWrapped(selectedPackageData.Description);

                    ImGui.Spacing();
                    if (!selectedPackageStatus.Installed)
                    {
                        if (ImGui.Button("Install"))
                        {
                            _ = InstallLexicon(selectedPackageData.InternalName);
                            selectedPackageStatus.Installed = true;
                        }
                    }
                    else if (ImGui.Button("Uninstall"))
                    {
                        UninstallLexicon(selectedPackageData.InternalName);
                        selectedPackageStatus.Installed = false;
                    }
                }
            }
        }
        ImGui.End();
    }

    /// <summary>
    /// Retrieves all remote package info files for the UI list.
    /// </summary>
    private async Task LoadPackageInfo()
    {
        if (this.remotePackagesLoading) return;
        this.remotePackagesLoading = true;
        var packages = await Task.WhenAll((await this.lexiconRepository.FetchPackages())
            .Select(package =>
            {
                var packageName = LexiconPackage.GetInternalName(package.Path);
                return this.lexiconRepository.GetPackage(packageName);
            })
            .Select(async package =>
            {
                var packageInfo = await package.GetPackageInfo();

                // Check the package for updates
                var shouldUpdate = new LexiconPackageStatus();
                if (package.IsInstalled())
                {
                    shouldUpdate.Installed = true;
                    foreach (var file in packageInfo.Files)
                    {
                        if (await package.HasUpdate(file))
                        {
                            shouldUpdate.HasUpdate = true;
                            break;
                        }
                    }
                }

                return (packageInfo, shouldUpdate);
            })
            .ToList());
        lock (this.rpLock)
        {
            this.remotePackages = packages;
            this.selectedPackage = default;
        }
        this.remotePackagesLoading = false;
        this.remotePackagesLoaded = true;
    }

    /// <summary>
    /// Loads all installed lexicons from the lexicon repository.
    /// </summary>
    private void LoadInstalledLexicons()
    {
        IEnumerable<LexiconPackage> packages;
        try
        {
            packages = Directory.EnumerateDirectories(this.lexiconRepository.CachePath)
                .Select(dir => new DirectoryInfo(dir).Name)
                .Where(packageName => this.lexiconRepository.GetPackage(packageName).IsInstalled())
                .Select(packageName => this.lexiconRepository.GetPackage(packageName));
        }
        catch (DirectoryNotFoundException)
        {
            // Cache folder has not yet been created
            return;
        }

        foreach (var package in packages)
        {
            var packageInfo = package.GetPackageInfoLocal();
            foreach (var file in packageInfo.Files)
            {
                var lexiconData = package.GetPackageFileLocal(file);
                if (lexiconData == null)
                {
                    PluginLog.Error($"Local data for lexicon file \"{file}\" of lexicon \"{package.PackageName}\" not found! Please reinstall this lexicon.");
                    continue;
                }

                try
                {
                    var lexiconId = GetLexiconFileId(package.PackageName, file);
                    this.lexiconManager.AddLexicon(lexiconData, lexiconId);
                }
                catch (Exception e)
                {
                    PluginLog.LogError(e, "Failed to load lexicon.");
                }
            }
        }
    }

    /// <summary>
    /// Installs a lexicon from the lexicon repository.
    /// </summary>
    /// <param name="packageName">The lexicon's package name.</param>
    private async Task InstallLexicon(string packageName)
    {
        // Fetch lexicon file list
        var package = this.lexiconRepository.GetPackage(packageName);
        var packageInfo = await package.GetPackageInfo();

        // Download each file and load it
        foreach (var file in packageInfo.Files)
        {
            var lexiconId = GetLexiconFileId(packageName, file);
            await using var lexiconData = await package.GetPackageFile(file);
            try
            {
                this.lexiconManager.AddLexicon(lexiconData, lexiconId);
            }
            catch (Exception e)
            {
                PluginLog.LogError(e, "Failed to load lexicon.");
            }
        }
    }

    /// <summary>
    /// Updates an installed lexicon from the lexicon repository.
    /// </summary>
    /// <param name="packageName">The package name of the lexicon to update.</param>
    private async Task UpdateLexicon(string packageName)
    {
        var package = this.lexiconRepository.GetPackage(packageName);
        if (!package.IsInstalled()) return;

        var packageInfo = await package.GetPackageInfo();

        // Download each updated file and (re)load it
        foreach (var file in packageInfo.Files)
        {
            if (!await package.HasUpdate(file)) continue;

            var lexiconId = GetLexiconFileId(packageName, file);
            await using var lexiconData = await package.GetPackageFile(file);
            this.lexiconManager.RemoveLexicon(lexiconId);
            this.lexiconManager.AddLexicon(lexiconData, lexiconId);
        }
    }

    /// <summary>
    /// Uninstalls a lexicon.
    /// </summary>
    /// <param name="packageName">The package name of the lexicon to uninstall.</param>
    private void UninstallLexicon(string packageName)
    {
        var package = this.lexiconRepository.GetPackage(packageName);
        if (!package.IsInstalled()) return;

        var packageInfo = package.GetPackageInfoLocal();
        foreach (var file in packageInfo.Files)
        {
            var lexiconId = GetLexiconFileId(packageName, file);

            try
            {
                this.lexiconManager.RemoveLexicon(lexiconId);
            }
            catch (Exception e)
            {
                PluginLog.LogError(e, "Failed to remove lexicon file.");
            }
        }

        try
        {
            this.lexiconRepository.RemovePackage(packageName);
        }
        catch (Exception e)
        {
            PluginLog.LogError(e, "Failed to uninstall lexicon.");
        }
    }

    /// <summary>
    /// Returns a file ID for a lexicon file, which should be unique.
    /// </summary>
    private static string GetLexiconFileId(string packageName, string filename)
    {
        return $"{packageName}.{filename}";
    }
}