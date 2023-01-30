using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Standart.Hash.xxHash;

namespace TextToTalk.UI;

public static class MemoizedId
{
    private static readonly ConcurrentDictionary<(string, string, int, string), string> UsedIds = new();

    /// <summary>
    /// Fetch a static ID for use in ImGui components. This uses compile-time caller
    /// information to generate IDs, so these IDs are not suitable for any UI elements
    /// that need a predictable ID for user settings, such as window labels.
    /// </summary>
    /// <param name="memberName">Method name or property name of the caller.</param>
    /// <param name="sourceFilePath">
    /// Full path of the source file that contains the caller.
    /// The full path is the path at compile time.
    /// </param>
    /// <param name="sourceLineNumber">
    /// Line number in the source file from which the method is called.
    /// </param>
    /// <param name="uniq">
    /// A parameter that may be provided to generate different IDs for each element of
    /// a loop, where the same line of code generates multiple UI components. 
    /// </param>
    /// <returns>A static ID suitable for ImGui components.</returns>
    public static string Create(
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0,
        string uniq = "")
    {
        var key = (memberName, sourceFilePath, sourceLineNumber, uniq);
        if (UsedIds.TryGetValue(key, out var id))
        {
            return id;
        }

        id = xxHash128.ComputeHash($"{memberName}{sourceFilePath}{sourceLineNumber}{uniq}").ToGuid().ToString();
        UsedIds[key] = id;
        return id;
    }
}