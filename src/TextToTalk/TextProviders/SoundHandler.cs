#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Dalamud.Game;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;

namespace TextToTalk.TextProviders;

public class SoundHandler : IDisposable
{
    // Signature strings drawn from Anna Clemens's Sound Filter plugin -
    // https://git.anna.lgbt/ascclemens/SoundFilter/src/commit/3b8512b4cd2f3ea0a0d162db4fa251ccb61f7dc4/SoundFilter/Filter.cs#L12
    private const string LoadSoundFileSig = "E8 ?? ?? ?? ?? 48 85 C0 75 04 B0 F6";

    private const string PlaySpecificSoundSig =
        "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 33 F6 8B DA 48 8B F9 0F BA E2 0F";

    private delegate nint LoadSoundFileDelegate(nint resourceHandlePtr, uint arg2);

    private delegate nint PlaySpecificSoundDelegate(nint soundPtr, int arg2);

    private readonly Hook<LoadSoundFileDelegate>? loadSoundFileHook;
    private readonly Hook<PlaySpecificSoundDelegate>? playSpecificSoundHook;

    private static readonly int ResourceDataOffset = Marshal.SizeOf<ResourceHandle>();
    private static readonly int SoundDataOffset = Marshal.SizeOf<nint>();

    private const string SoundContainerFileNameSuffix = ".scd";

    private static readonly Regex IgnoredSoundFileNameRegex = new(
        @"^(bgcommon|music|sound/(battle|foot|instruments|strm|vfx|voice/Vo_Emote|zingle))/");

    private static readonly Regex VoiceLineFileNameRegex = new(@"^cut/.*/(vo_|voice)");
    private readonly HashSet<nint> knownVoiceLinePtrs = new();

    private readonly AddonTalkHandler addonTalkHandler;
    private readonly AddonBattleTalkHandler addonBattleTalkHandler;

    public SoundHandler(AddonTalkHandler addonTalkHandler, AddonBattleTalkHandler addonBattleTalkHandler,
        SigScanner sigScanner)
    {
        this.addonTalkHandler = addonTalkHandler;
        this.addonBattleTalkHandler = addonBattleTalkHandler;

        if (sigScanner.TryScanText(LoadSoundFileSig, out var loadSoundFilePtr))
        {
            this.loadSoundFileHook = Hook<LoadSoundFileDelegate>.FromAddress(loadSoundFilePtr, LoadSoundFileDetour);
            this.loadSoundFileHook.Enable();
            DetailedLog.Debug("Hooked into LoadSoundFile");
        }
        else
        {
            DetailedLog.Error("Failed to hook into LoadSoundFile");
        }

        if (sigScanner.TryScanText(PlaySpecificSoundSig, out var playSpecificSoundPtr))
        {
            this.playSpecificSoundHook =
                Hook<PlaySpecificSoundDelegate>.FromAddress(playSpecificSoundPtr, PlaySpecificSoundDetour);
            this.playSpecificSoundHook.Enable();
            DetailedLog.Debug("Hooked into PlaySpecificSound");
        }
        else
        {
            DetailedLog.Error("Failed to hook into PlaySpecificSound");
        }
    }

    public void Dispose()
    {
        this.loadSoundFileHook?.Dispose();
        this.playSpecificSoundHook?.Dispose();
    }

    private nint LoadSoundFileDetour(nint resourceHandlePtr, uint arg2)
    {
        var result = this.loadSoundFileHook!.Original(resourceHandlePtr, arg2);

        try
        {
            string fileName;
            unsafe
            {
                fileName = ((ResourceHandle*)resourceHandlePtr)->FileName.ToString();
            }

            if (fileName.EndsWith(SoundContainerFileNameSuffix))
            {
                var resourceDataPtr = Marshal.ReadIntPtr(resourceHandlePtr + ResourceDataOffset);
                if (resourceDataPtr != nint.Zero)
                {
                    var isVoiceLine = false;

                    if (!IgnoredSoundFileNameRegex.IsMatch(fileName))
                    {
                        DetailedLog.Debug($"Loaded sound: {fileName}");

                        if (VoiceLineFileNameRegex.IsMatch(fileName))
                        {
                            isVoiceLine = true;
                        }
                    }

                    if (isVoiceLine)
                    {
                        DetailedLog.Debug($"Discovered voice line at address {resourceDataPtr:x}");
                        this.knownVoiceLinePtrs.Add(resourceDataPtr);
                    }
                    else
                    {
                        // Addresses can be reused, so a non-voice-line sound may be loaded to an address previously
                        // occupied by a voice line.
                        if (this.knownVoiceLinePtrs.Remove(resourceDataPtr))
                        {
                            DetailedLog.Debug(
                                $"Cleared voice line from address {resourceDataPtr:x} (address reused by: {fileName})");
                        }
                    }
                }
            }
        }
        catch (Exception exc)
        {
            DetailedLog.Error(exc, "Error in LoadSoundFile detour");
        }

        return result;
    }

    private nint PlaySpecificSoundDetour(nint soundPtr, int arg2)
    {
        var result = this.playSpecificSoundHook!.Original(soundPtr, arg2);

        try
        {
            var soundDataPtr = Marshal.ReadIntPtr(soundPtr + SoundDataOffset);
            // Assume that a voice line will be played only once after it's loaded. Then the set can be pruned as voice
            // lines are played.
            if (this.knownVoiceLinePtrs.Remove(soundDataPtr))
            {
                DetailedLog.Debug($"Caught playback of known voice line at address {soundDataPtr:x}");
                this.addonTalkHandler.PollAddon(AddonPollSource.VoiceLinePlayback);
                this.addonBattleTalkHandler.PollAddon(AddonPollSource.VoiceLinePlayback);
            }
        }
        catch (Exception exc)
        {
            DetailedLog.Error(exc, "Error in PlaySpecificSound detour");
        }

        return result;
    }
}