using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Dalamud.Logging;

namespace TextToTalk;

public static class DetailedLog
{
    public static void Verbose(
        string messageTemplate,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0,
        params object[] values)
    {
        PluginLog.LogVerbose(
            WrapTemplate(messageTemplate),
            WrapParameters(memberName, sourceFilePath, sourceLineNumber, values));
    }

    public static void Debug(
        string messageTemplate,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0,
        params object[] values)
    {
        PluginLog.LogDebug(
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
        PluginLog.LogInformation(
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
        PluginLog.LogWarning(
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
        PluginLog.LogError(
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
        PluginLog.LogError(
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
        var debug = new object[] { memberName, sourceFilePath, sourceLineNumber };
        return values.Concat(debug).ToArray();
    }

    private static string WrapTemplate(string messageTemplate)
    {
        return $"{messageTemplate}\nMember: {{CallerMemberName}}\nSource: {{CallerFilePath}}:line {{CallerLineNumber}}";
    }
}