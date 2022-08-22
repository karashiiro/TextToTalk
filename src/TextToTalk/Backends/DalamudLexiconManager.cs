using System.IO;
using Dalamud.Logging;
using TextToTalk.Lexicons;

namespace TextToTalk.Backends;

public class DalamudLexiconManager : LexiconManager
{
    public override int AddLexicon(Stream data, string lexiconId)
    {
        LogPreAdd(lexiconId);
        var newEntries = base.AddLexicon(data, lexiconId);
        LogPostAdd(lexiconId, newEntries, Count);
        return newEntries;
    }

    public override int AddLexicon(string lexiconUrl)
    {
        LogPreAdd(lexiconUrl);
        var newEntries = base.AddLexicon(lexiconUrl);
        LogPostAdd(lexiconUrl, newEntries, Count);
        return newEntries;
    }
    
    public override void RemoveLexicon(string lexiconId)
    {
        LogPreRemove(lexiconId);
        base.RemoveLexicon(lexiconId);
        LogPostRemove(lexiconId, Count);
    }

    private static void LogPreAdd(string lexiconId)
    {
        PluginLog.Log($"Adding lexicon \"{lexiconId}\"");
    }

    private static void LogPostAdd(string lexiconId, int newEntries, int totalEntries)
    {
        PluginLog.Log($"Lexicon \"{lexiconId}\" added; {newEntries} new lexicon entries ({totalEntries} total).");
    }

    private static void LogPreRemove(string lexiconId)
    {
        PluginLog.Log($"Removing lexicon \"{lexiconId}\"");
    }

    private static void LogPostRemove(string lexiconId, int totalEntries)
    {
        PluginLog.Log($"Lexicon \"{lexiconId}\" removed; {totalEntries} lexicon entries remaining.");
    }
}