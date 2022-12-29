using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TextToTalk.Backends;
using TextToTalk.Backends.Azure;
using TextToTalk.Backends.Polly;
using TextToTalk.Backends.System;
using TextToTalk.Backends.Uberduck;
using TextToTalk.Backends.Websocket;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace TextToTalk;

public class VoicePresetConfiguration
{
    #region Obsolete Members

    [Obsolete("Use CurrentVoicePreset.")] public IDictionary<TTSBackend, int> CurrentVoicePresets { get; set; }

    [Obsolete("Use UngenderedVoicePresets.")]
    [JsonProperty("UngenderedVoicePresets")]
    public IDictionary<TTSBackend, object> UngenderedVoicePresetsBroken { get; init; }

    [Obsolete("Use MaleVoicePresets.")]
    [JsonProperty("MaleVoicePresets")]
    public IDictionary<TTSBackend, object> MaleVoicePresetsBroken { get; init; }

    [Obsolete("Use FemaleVoicePresets.")]
    [JsonProperty("FemaleVoicePresets")]
    public IDictionary<TTSBackend, object> FemaleVoicePresetsBroken { get; init; }

    #endregion

    [JsonIgnore] public IList<VoicePreset> VoicePresets { get; private set; }

    // Newtonsoft.Json doesn't like handling inheritance. This should probably go into LiteDB or something instead.
    // Saving VoicePreset objects correctly saves type information, but that gets completely ignored on load. It
    // also can't be loaded from within the plugin because of restrictions on collectable assemblies.
    [JsonProperty] private IList<IDictionary<string, object>> VoicePresetsRaw { get; set; }

    public IDictionary<TTSBackend, int> CurrentVoicePreset { get; init; }

    [JsonProperty("_UngenderedVoicePresets")]
    public IDictionary<TTSBackend, SortedSet<int>> UngenderedVoicePresets { get; init; }

    [JsonProperty("_MaleVoicePresets")] public IDictionary<TTSBackend, SortedSet<int>> MaleVoicePresets { get; init; }

    [JsonProperty("_FemaleVoicePresets")]
    public IDictionary<TTSBackend, SortedSet<int>> FemaleVoicePresets { get; init; }

    [JsonIgnore] private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        Formatting = Formatting.Indented,
    };

    [JsonIgnore] private readonly object cfgLock;

    public VoicePresetConfiguration()
    {
        this.cfgLock = true;

        VoicePresets = new List<VoicePreset>();
        CurrentVoicePreset = new Dictionary<TTSBackend, int>();
        UngenderedVoicePresets = new Dictionary<TTSBackend, SortedSet<int>>();
        MaleVoicePresets = new Dictionary<TTSBackend, SortedSet<int>>();
        FemaleVoicePresets = new Dictionary<TTSBackend, SortedSet<int>>();
    }

    /// <summary>
    /// Retrieves the ungendered voice presets for the specified backend, creating the set if it
    /// doesn't yet exist.
    /// </summary>
    /// <param name="backend">The backend to select voices for.</param>
    /// <returns></returns>
    public SortedSet<int> GetUngenderedPresets(TTSBackend backend)
    {
        if (UngenderedVoicePresets.TryGetValue(backend, out var presets))
        {
            return presets;
        }

        var newPresets = new SortedSet<int>();
        UngenderedVoicePresets[backend] = newPresets;
        return newPresets;
    }

    /// <summary>
    /// Retrieves the male voice presets for the specified backend, creating the set if it
    /// doesn't yet exist.
    /// </summary>
    /// <param name="backend">The backend to select voices for.</param>
    /// <returns></returns>
    public SortedSet<int> GetMalePresets(TTSBackend backend)
    {
        if (MaleVoicePresets.TryGetValue(backend, out var presets))
        {
            return presets;
        }

        var newPresets = new SortedSet<int>();
        MaleVoicePresets[backend] = newPresets;
        return newPresets;
    }

    /// <summary>
    /// Retrieves the female voice presets for the specified backend, creating the set if it
    /// doesn't yet exist.
    /// </summary>
    /// <param name="backend">The backend to select voices for.</param>
    /// <returns></returns>
    public SortedSet<int> GetFemalePresets(TTSBackend backend)
    {
        if (FemaleVoicePresets.TryGetValue(backend, out var presets))
        {
            return presets;
        }

        var newPresets = new SortedSet<int>();
        FemaleVoicePresets[backend] = newPresets;
        return newPresets;
    }

    public void SaveToFile(string path)
    {
        var serializerSettings = SerializerSettings;
        lock (cfgLock)
        {
            VoicePresetsRaw = VoicePresets.Select(CorruptPreset).ToList();
            var data = JsonConvert.SerializeObject(this, serializerSettings);
            File.WriteAllText(path, data);
        }
    }

    public static VoicePresetConfiguration LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            return new VoicePresetConfiguration();
        }

        var data = File.ReadAllText(path);
        var config = JsonConvert.DeserializeObject<VoicePresetConfiguration>(data, SerializerSettings);
        if (config == null)
        {
            throw new InvalidOperationException("Voice preset config was null.");
        }

        config.VoicePresets = config.VoicePresetsRaw.Select(RepairPreset).ToList();
        return config;
    }

    private static IDictionary<string, object> CorruptPreset(VoicePreset p)
    {
        var o = new Dictionary<string, object>();
        var properties = p.GetType().GetProperties()
            .Where(prop => prop.SetMethod != null && prop.GetMethod != null)
            .Select(prop =>
                new KeyValuePair<string, object>(prop.Name, prop.GetMethod!.Invoke(p, Array.Empty<object>())));
        foreach (var (k, v) in properties)
        {
            o[k] = v;
        }

        return o;
    }

    private static VoicePreset RepairPreset(IDictionary<string, object> corrupted)
    {
        var backendCorrupt = (TTSBackend)corrupted["EnabledBackend"];
        return backendCorrupt switch
        {
            TTSBackend.System => new SystemVoicePreset
            {
                // These get read as Int64 objects, so they need to be
                // cast to Int64 and then converted to Int32. Similar thing
                // for Double objects.
                Id = Convert.ToInt32((long)corrupted["Id"]),
                Name = (string)corrupted["Name"],
                Rate = Convert.ToInt32((long)corrupted["Rate"]),
                Volume = Convert.ToInt32((long)corrupted["Volume"]),
                VoiceName = (string)corrupted["VoiceName"],
                EnabledBackend = TTSBackend.System,
            },
            TTSBackend.AmazonPolly => new PollyVoicePreset
            {
                Id = Convert.ToInt32((long)corrupted["Id"]),
                Name = (string)corrupted["Name"],
                SampleRate = Convert.ToInt32((long)corrupted["SampleRate"]),
                PlaybackRate = Convert.ToInt32((long)corrupted["PlaybackRate"]),
                Volume = Convert.ToSingle((double)corrupted["Volume"]),
                VoiceName = (string)corrupted["VoiceName"],
                VoiceEngine = (string)corrupted["VoiceEngine"],
                EnabledBackend = TTSBackend.AmazonPolly,
            },
            TTSBackend.Uberduck => new UberduckVoicePreset
            {
                Id = Convert.ToInt32((long)corrupted["Id"]),
                Name = (string)corrupted["Name"],
                PlaybackRate = Convert.ToInt32((long)corrupted["PlaybackRate"]),
                Volume = Convert.ToSingle((double)corrupted["Volume"]),
                VoiceName = (string)corrupted["VoiceName"],
                EnabledBackend = TTSBackend.Uberduck,
            },
            TTSBackend.Websocket => new WebsocketVoicePreset
            {
                Id = Convert.ToInt32((long)corrupted["Id"]),
                Name = (string)corrupted["Name"],
                EnabledBackend = TTSBackend.Websocket,
            },
            TTSBackend.Azure => new AzureVoicePreset
            {
                Id = Convert.ToInt32((long)corrupted["Id"]),
                Name = (string)corrupted["Name"],
                PlaybackRate = Convert.ToInt32((long)corrupted["PlaybackRate"]),
                Volume = Convert.ToSingle((double)corrupted["Volume"]),
                VoiceName = (string)corrupted["VoiceName"],
                EnabledBackend = TTSBackend.Azure,
            },
            _ => throw new ArgumentOutOfRangeException($"{backendCorrupt}"),
        };
    }
}