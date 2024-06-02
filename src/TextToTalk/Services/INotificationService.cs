using System.Runtime.CompilerServices;

namespace TextToTalk.Services;

public interface INotificationService
{
    void NotifyWarning(
        string title,
        string description,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0);

    void NotifyError(
        string title,
        string description,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0);
}