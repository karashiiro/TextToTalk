using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;
using TextToTalk.Backends;
using TextToTalk.Lexicons;
using TextToTalk.Lexicons.Updater;

namespace TextToTalk.UI.Lexicons;

public class LexiconRepositorySubwindow
{
    private static readonly uint InstalledBg = ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 1.0f, 0.0f, 0.25f));
    private static readonly uint HasUpdateBg = ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 1.0f, 0.0f, 0.25f));

    private readonly LexiconManager lexiconManager;
    private readonly LexiconRepository lexiconRepository;
    private readonly PluginConfiguration config;

    private (LexiconPackageInfo?, LexiconPackageInstallationStatus?) selectedPackage;

    private readonly object rpLock;
    private IList<(LexiconPackageInfo?, LexiconPackageInstallationStatus)>? remotePackages;
    private bool remotePackagesLoading;
    private bool remotePackagesLoaded;

    public LexiconRepositorySubwindow(LexiconManager lm, LexiconRepository lr, PluginConfiguration config)
    {
        this.lexiconManager = lm;
        this.lexiconRepository = lr;
        this.config = config;

        this.rpLock = true;

        Task.Factory.StartNew(LoadInstalledLexicons);
    }

    public void Clear()
    {
        this.remotePackagesLoaded = false;
        this.remotePackages = null;
        this.selectedPackage = default;
    }

    public void Draw(ref bool visible)
    {
        ImGui.SetNextWindowSize(new Vector2(670, 480), ImGuiCond.Appearing);
        ImGui.Begin("Lexicon Repository##TTTLexiconRepositorySubwindow", ref visible);
        {
            DrawPackageList();
            DrawSelectedPackageInfo();
            DrawSelectedPackageSettings();
        }
        ImGui.End();
    }

    private void DrawPackageList()
    {
        if (!this.remotePackagesLoaded && !this.remotePackagesLoading)
        {
            // Fetch the list of lexicon packages
            DetailedLog.Info("Fetching lexicon package list...");
            _ = LoadPackageList();
        }
        else if (ImGui.BeginTable("##TTTLexiconRepoList", 3, ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn("Lexicon", ImGuiTableColumnFlags.None, 280f);
            ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.None, 100f);
            ImGui.TableSetupColumn("Authors", ImGuiTableColumnFlags.None, 220f);
            ImGui.TableHeadersRow();

            if (this.remotePackages != null)
            {
                lock (this.rpLock)
                {
                    foreach (var remotePackage in this.remotePackages)
                    {
                        var (selectedPackageData, _) = this.selectedPackage;
                        var (packageData, packageStatus) = remotePackage;

                        ImGui.TableNextRow();

                        SetTableRowBg(packageStatus);

                        ImGui.TableSetColumnIndex(0);
                        if (ImGui.Selectable($"##TTTLexiconRepoList{packageData.InternalName}", selectedPackageData == packageData, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap, Vector2.Zero))
                        {
                            this.selectedPackage = remotePackage;
                        }
                        ImGui.SameLine(0f, 0f);
                        ImGui.Text(packageData.Name);

                        ImGui.TableSetColumnIndex(1);
                        if (packageStatus.HasUpdate)
                        {
                            ImGui.Text("Has update");
                        }
                        else if (packageStatus.Installed)
                        {
                            ImGui.Text("Installed");
                        }

                        ImGui.TableSetColumnIndex(2);
                        ImGui.Text(packageData.Author);
                    }
                }
            }

            ImGui.EndTable();
        }
    }

    private void DrawSelectedPackageInfo()
    {
        lock (this.rpLock)
        {
            var (selectedPackageData, selectedPackageStatus) = this.selectedPackage;
            if (selectedPackageData != null)
            {
                ImGui.Text($"{selectedPackageData.Name} by {selectedPackageData.Author}");
                ImGui.TextWrapped(selectedPackageData.Description);

                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.Text("Lexicon package files:");
                foreach (var file in selectedPackageData.Files)
                {
                    ImGui.Text($" - {file}");
                }

                ImGui.Spacing();
                if (!selectedPackageStatus.Installed)
                {
                    if (ImGui.Button("Install##TTTLexiconRepoInstall"))
                    {
                        _ = InstallLexicon(selectedPackageData.InternalName);
                        selectedPackageStatus.Installed = true;
                    }
                }
                else if (ImGui.Button("Uninstall##TTTLexiconRepoUninstall"))
                {
                    UninstallLexicon(selectedPackageData.InternalName);
                    selectedPackageStatus.Installed = false;
                }

                if (selectedPackageStatus.HasUpdate)
                {
                    ImGui.SameLine();
                    if (selectedPackageStatus.Updating)
                    {
                        ImGui.Button("Updating##TextToTalkLexiconRepoUpdating");
                    }
                    else if (ImGui.Button("Update##TTTLexiconRepoUpdate"))
                    {
                        selectedPackageStatus.Updating = true;
                        Task.Run(async () =>
                        {
                            await UpdateLexicon(selectedPackageData.InternalName);
                            selectedPackageStatus.HasUpdate = false;
                            selectedPackageStatus.Updating = false;
                        });
                    }
                }
            }
        }
    }

    private void DrawSelectedPackageSettings()
    {
        lock (this.rpLock)
        {
            var (selectedPackageData, selectedPackageStatus) = this.selectedPackage;
            if (selectedPackageData != null && selectedPackageStatus.Installed)
            {
                ImGui.Separator();

                ImGui.Text("Enabled for backends:");
                ImGui.Indent();
                {
                    foreach (var backend in Enum.GetValues<TTSBackend>().Where(b => b.AreLexiconsEnabled()))
                    {
                        EnsureEnabledBackends(selectedPackageData.InternalName);

                        var name = backend.GetFormattedName();
                        var otherEnabled = this.config.RemoteLexiconEnabledBackends[selectedPackageData.InternalName][backend];
                        var currentEnabledBefore = this.config.RemoteLexiconEnabledBackends[selectedPackageData.InternalName][this.config.Backend];

                        if (ImGui.Checkbox($"{name}##TTTLexiconRepoEnabledBackend{name}", ref otherEnabled))
                        {
                            this.config.RemoteLexiconEnabledBackends[selectedPackageData.InternalName][backend] = otherEnabled;
                            var currentEnabled = this.config.RemoteLexiconEnabledBackends[selectedPackageData.InternalName][this.config.Backend];

                            var package = this.lexiconRepository.GetPackage(selectedPackageData.InternalName);
                            foreach (var file in selectedPackageData.Files)
                            {
                                var lexiconId = GetLexiconFileId(selectedPackageData.InternalName, file);

                                if (otherEnabled && currentEnabled)
                                {
                                    using var lexiconData = package.GetPackageFileLocal(file);
                                    try
                                    {
                                        this.lexiconManager.AddLexicon(lexiconData, lexiconId);
                                    }
                                    catch (Exception e)
                                    {
                                        DetailedLog.Error(e, "Failed to load lexicon.");
                                    }
                                }
                                else if (currentEnabledBefore && !currentEnabled)
                                {
                                    try
                                    {
                                        this.lexiconManager.RemoveLexicon(lexiconId);
                                    }
                                    catch (Exception e)
                                    {
                                        DetailedLog.Error(e, "Failed to unload lexicon.");
                                    }
                                }
                            }

                            this.config.Save();
                        }
                    }
                }
                ImGui.Unindent();
            }
        }
    }

    /// <summary>
    /// Retrieves all remote package info files for the UI list.
    /// </summary>
    private async Task LoadPackageList()
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

                // Check the package installation status
                var shouldUpdate = new LexiconPackageInstallationStatus();
                if (package.IsInstalled())
                {
                    shouldUpdate.Installed = true;
                    shouldUpdate.HasUpdate = await package.HasUpdate();
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
            EnsureEnabledBackends(package.PackageName);

            if (!this.config.RemoteLexiconEnabledBackends[package.PackageName][this.config.Backend])
            {
                continue;
            }

            var packageInfo = package.GetPackageInfoLocal();
            foreach (var file in packageInfo.Files)
            {
                var lexiconData = package.GetPackageFileLocal(file);
                if (lexiconData == null)
                {
                    DetailedLog.Error($"Local data for lexicon file \"{file}\" of lexicon \"{package.PackageName}\" not found! Please reinstall this lexicon.");
                    continue;
                }

                try
                {
                    var lexiconId = GetLexiconFileId(package.PackageName, file);
                    this.lexiconManager.AddLexicon(lexiconData, lexiconId);
                }
                catch (Exception e)
                {
                    DetailedLog.Error(e, "Failed to load lexicon.");
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

        // Create an enabled backends list for the new package
        EnsureEnabledBackends(packageName);

        // Download each file and load it
        foreach (var file in packageInfo.Files)
        {
            var lexiconId = GetLexiconFileId(packageName, file);
            await using var lexiconData = await package.GetPackageFile(file);
            try
            {
                if (this.config.RemoteLexiconEnabledBackends[package.PackageName][this.config.Backend])
                {
                    this.lexiconManager.AddLexicon(lexiconData, lexiconId);
                }
            }
            catch (Exception e)
            {
                DetailedLog.Error(e, "Failed to load lexicon.");
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

        EnsureEnabledBackends(packageName);

        // Unload all package files
        var packageInfoLocal = package.GetPackageInfoLocal();
        foreach (var file in packageInfoLocal.Files)
        {
            var lexiconId = GetLexiconFileId(packageName, file);
            if (this.config.RemoteLexiconEnabledBackends[package.PackageName][this.config.Backend])
            {
                this.lexiconManager.RemoveLexicon(lexiconId);
            }
        }

        // Download and (re)load all package files
        var packageInfo = await package.GetPackageInfo();
        foreach (var file in packageInfo.Files)
        {
            var lexiconId = GetLexiconFileId(packageName, file);
            await using var lexiconData = await package.GetPackageFile(file);
            if (this.config.RemoteLexiconEnabledBackends[package.PackageName][this.config.Backend])
            {
                this.lexiconManager.AddLexicon(lexiconData, lexiconId);
            }
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
                DetailedLog.Error(e, "Failed to remove lexicon file.");
            }
        }

        try
        {
            this.lexiconRepository.RemovePackage(packageName);
        }
        catch (Exception e)
        {
            DetailedLog.Error(e, "Failed to uninstall lexicon.");
        }
    }

    private void EnsureEnabledBackends(string packageName)
    {
        if (!this.config.RemoteLexiconEnabledBackends.TryGetValue(packageName, out _))
        {
            this.config.RemoteLexiconEnabledBackends[packageName] =
                new Dictionary<TTSBackend, bool>();
        }

        foreach (var backend in Enum.GetValues<TTSBackend>().Where(b => b.AreLexiconsEnabled()))
        {
            if (!this.config.RemoteLexiconEnabledBackends[packageName].TryGetValue(backend, out _))
            {
                this.config.RemoteLexiconEnabledBackends[packageName][backend] = false;
            }
        }

        this.config.Save();
    }

    /// <summary>
    /// Returns a file ID for a lexicon file, which should be unique.
    /// </summary>
    private static string GetLexiconFileId(string packageName, string filename)
    {
        return $"{packageName}.{filename}";
    }

    /// <summary>
    /// Sets the table row's background color based on the provided package status object.
    /// </summary>
    private static unsafe void SetTableRowBg(LexiconPackageInstallationStatus? status)
    {
        var rowBg0Default = ImGui.ColorConvertFloat4ToU32(*ImGui.GetStyleColorVec4(ImGuiCol.TableRowBg));
        var rowBg1Default = ImGui.ColorConvertFloat4ToU32(*ImGui.GetStyleColorVec4(ImGuiCol.TableRowBgAlt));

        if (status == null)
        {
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, rowBg0Default);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, rowBg1Default);
        }
        else if (status.HasUpdate)
        {
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, HasUpdateBg);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, HasUpdateBg);
        }
        else if (status.Installed)
        {
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, InstalledBg);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, InstalledBg);
        }
        else
        {
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, rowBg0Default);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, rowBg1Default);
        }
    }
}