using System;
using TextToTalk.Lexicons;

namespace TextToTalk.Backends;

public static class LexiconUtils
{
    public static void LoadFromConfigSystem(LexiconManager lexiconManager, PluginConfiguration config)
    {
        for (var i = 0; i < config.Lexicons.Count; i++)
        {
            var lexicon = config.Lexicons[i];

            try
            {
                lexiconManager.AddLexicon(lexicon);
            }
            catch (Exception e)
            {
                DetailedLog.Error(e, "Failed to add lexicon - removing from configuration.");
                config.Lexicons.RemoveAt(i);
                config.Save();
                i--;
            }
        }
    }

    public static void LoadFromConfigPolly(LexiconManager lexiconManager, PluginConfiguration config)
    {
        for (var i = 0; i < config.PollyLexiconFiles.Count; i++)
        {
            var lexicon = config.PollyLexiconFiles[i];

            try
            {
                lexiconManager.AddLexicon(lexicon);
            }
            catch (Exception e)
            {
                DetailedLog.Error(e, "Failed to add lexicon - removing from configuration.");
                config.PollyLexiconFiles.RemoveAt(i);
                config.Save();
                i--;
            }
        }
    }
    
    public static void LoadFromConfigAzure(LexiconManager lexiconManager, PluginConfiguration config)
    {
        for (var i = 0; i < config.AzureLexiconFiles.Count; i++)
        {
            var lexicon = config.AzureLexiconFiles[i];

            try
            {
                lexiconManager.AddLexicon(lexicon);
            }
            catch (Exception e)
            {
                DetailedLog.Error(e, "Failed to add lexicon - removing from configuration.");
                config.AzureLexiconFiles.RemoveAt(i);
                config.Save();
                i--;
            }
        }
    }
}