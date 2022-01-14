using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TextToTalk.UngenderedOverrides
{
    public class UngenderedOverrideManager
    {
        private readonly IReadOnlyDictionary<int, bool> ungenderedMap;

        public UngenderedOverrideManager()
        {
            this.ungenderedMap = ReadAssemblyOverridesFile();
        }

        public UngenderedOverrideManager(string overrideData)
        {
            this.ungenderedMap = ParseOverridesFile(overrideData);
        }

        public bool IsUngendered(int modelId)
        {
            return this.ungenderedMap.TryGetValue(modelId, out _);
        }

        private static IReadOnlyDictionary<int, bool> ReadAssemblyOverridesFile()
        {
            using var fileData = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("TextToTalk.UngenderedOverrides.overridenModelIds.txt");
            if (fileData == null) throw new FileNotFoundException("Failed to load ungendered model overrides file!");
            using var sr = new StreamReader(fileData);
            return ParseOverridesFile(sr.ReadToEnd());
        }

        private static IReadOnlyDictionary<int, bool> ParseOverridesFile(string fileData)
        {
            return fileData.Split('\r', '\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToDictionary(line =>
            {
                line = line.Split(';')[0].Trim(); // Remove comments

                try
                {
                    return int.Parse(line);
                }
                catch (Exception e)
                {
                    throw new AggregateException($"Failed to parse model ID \"{line}\"!", e);
                }
            }, _ => true);
        }
    }
}
