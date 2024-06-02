using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;

namespace TextToTalk;

public static class DetailedLog
{
    private static IPluginLog? logger;

    public static void SetLogger(IPluginLog pluginLog)
    {
        logger = pluginLog;
    }

    public static void Verbose(
        string messageTemplate,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0,
        params object[] values)
    {
        logger?.Verbose(
            WrapTemplate(messageTemplate),
            WrapParameters(memberName, sourceFilePath, sourceLineNumber, values));
    }

    [Conditional("DEBUG")]
    public static void Debug(
        string messageTemplate,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0,
        params object[] values)
    {
        logger?.Debug(
            WrapTemplate(messageTemplate),
            WrapParameters(memberName, sourceFilePath, sourceLineNumber, values));
    }

    public static void Info(
        string messageTemplate,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0,
        params object[] values)
    {
        logger?.Information(
            WrapTemplate(messageTemplate),
            WrapParameters(memberName, sourceFilePath, sourceLineNumber, values));
    }

    public static void Warn(
        string messageTemplate,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0,
        params object[] values)
    {
        logger?.Warning(
            WrapTemplate(messageTemplate),
            WrapParameters(memberName, sourceFilePath, sourceLineNumber, values));
    }

    public static void Error(
        string messageTemplate,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0,
        params object[] values)
    {
        logger?.Error(
            WrapTemplate(messageTemplate),
            WrapParameters(memberName, sourceFilePath, sourceLineNumber, values));
    }

    public static void Error(
        Exception exception,
        string messageTemplate,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0,
        params object[] values)
    {
        logger?.Error(
            exception,
            WrapTemplate(messageTemplate),
            WrapParameters(memberName, sourceFilePath, sourceLineNumber, values));
    }

    private static object[] WrapParameters(
        string memberName,
        string sourceFilePath,
        int sourceLineNumber,
        IEnumerable<object> values)
    {
#if DEBUG
        var debug = new object[] { memberName, sourceFilePath, sourceLineNumber };
        return values.Concat(debug).ToArray();
#else
        return values.ToArray();
#endif
    }

    private static string WrapTemplate(string messageTemplate)
    {
#if DEBUG
        return $"{messageTemplate}\nMember: {{CallerMemberName}}\nSource: {{CallerFilePath}}:line {{CallerLineNumber}}";
#else
        return $"{messageTemplate}";
#endif
    }
}