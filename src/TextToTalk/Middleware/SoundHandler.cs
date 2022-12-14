#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;

namespace TextToTalk.Middleware;

public class SoundHandler : IDisposable
{
    private const string LoadSoundFileSig = "E8 ?? ?? ?? ?? 48 85 C0 75 04 B0 F6";
    private const string PlaySpecificSoundSig =
        "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 33 F6 8B DA 48 8B F9 0F BA E2 0F";

    private delegate IntPtr LoadSoundFileDelegate(IntPtr resourceHandlePtr, uint arg2);
    private delegate IntPtr PlaySpecificSoundDelegate(IntPtr soundPtr, int arg2);
    
    private readonly Hook<LoadSoundFileDelegate>? loadSoundFileHook;
    private readonly Hook<PlaySpecificSoundDelegate>? playSpecificSoundHook;

    private static readonly int ResourceDataOffset = Marshal.SizeOf<ResourceHandle>();
    private static readonly int SoundDataOffset = Marshal.SizeOf<IntPtr>();

    private const string SoundContainerFileNameSuffix = ".scd";
    private static readonly Regex IgnoredSoundFileNameRegex = new(
        @"^(bgcommon|music|sound/(battle|foot|instruments|strm|vfx|voice/Vo_Emote|zingle))/");
    private static readonly Regex VoiceLineFileNameRegex = new(@"^cut/.*/(vo_|voice)");
    private readonly HashSet<IntPtr> knownVoiceLinePtrs = new();
    
    private readonly TalkAddonHandler talkAddonHandler;

    public SoundHandler(TalkAddonHandler talkAddonHandler, SigScanner sigScanner)
    {
        this.talkAddonHandler = talkAddonHandler;
        
        if (sigScanner.TryScanText(LoadSoundFileSig, out var loadSoundFilePtr))
        {
            this.loadSoundFileHook = Hook<LoadSoundFileDelegate>.FromAddress(loadSoundFilePtr, LoadSoundFileDetour);
            this.loadSoundFileHook.Enable();
            PluginLog.Log("Hooked into LoadSoundFile");
        } else {
            PluginLog.LogError("Failed to hook into LoadSoundFile");
        }
    
        if (sigScanner.TryScanText(PlaySpecificSoundSig, out var playSpecificSoundPtr))
        {
            this.playSpecificSoundHook = Hook<PlaySpecificSoundDelegate>.FromAddress(playSpecificSoundPtr, PlaySpecificSoundDetour);
            this.playSpecificSoundHook.Enable();
            PluginLog.Log("Hooked into PlaySpecificSound");
        } else {
            PluginLog.LogError("Failed to hook into PlaySpecificSound");
        }
    }
    
    public void Dispose()
    {
        this.loadSoundFileHook?.Dispose();
        this.playSpecificSoundHook?.Dispose();
    }
    
    private IntPtr LoadSoundFileDetour(IntPtr resourceHandlePtr, uint arg2)
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
                if (resourceDataPtr != IntPtr.Zero)
                {
                    var isVoiceLine = false;

                    if (!IgnoredSoundFileNameRegex.IsMatch(fileName))
                    {
                        PluginLog.Log($"Loaded sound: {fileName}");

                        if (VoiceLineFileNameRegex.IsMatch(fileName))
                        {
                            isVoiceLine = true;
                        }
                    }
                    
                    if (isVoiceLine)
                    {
                        PluginLog.Log($"Discovered voice line at address {resourceDataPtr:x}");
                        this.knownVoiceLinePtrs.Add(resourceDataPtr);
                    }
                    else
                    {
                        // Addresses can be reused, so a non-voice-line sound may be loaded to an address previously
                        // occupied by a voice line.
                        if (this.knownVoiceLinePtrs.Remove(resourceDataPtr))
                        {
                            PluginLog.Log(
                                $"Cleared voice line from address {resourceDataPtr:x} (address reused by: {fileName})");
                        }
                    }
                }
            }
        }
        catch (Exception exc)
        {
            PluginLog.LogError(exc, "Error in LoadSoundFile detour");
        }

        return result;
    }

    private IntPtr PlaySpecificSoundDetour(IntPtr soundPtr, int arg2)
    {
        var result = this.playSpecificSoundHook!.Original(soundPtr, arg2);

        try
        {
            var soundDataPtr = Marshal.ReadIntPtr(soundPtr + SoundDataOffset);
            // Assume that a voice line will be played only once after it's loaded. Then the set can be pruned as voice
            // lines are played.
            if (this.knownVoiceLinePtrs.Remove(soundDataPtr))
            {
                PluginLog.Log($"Caught playback of known voice line at address {soundDataPtr:x}");
                talkAddonHandler.PollAddon(TalkAddonHandler.PollSource.VoiceLinePlayback);
            }
        }
        catch (Exception exc)
        {
            PluginLog.LogError(exc, "Error in PlaySpecificSound detour");
        }

        return result;
    }
}