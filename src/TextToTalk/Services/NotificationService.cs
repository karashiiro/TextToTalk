using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Plugin.Services;

// ReSharper disable ExplicitCallerInfoArgument

namespace TextToTalk.Services;

public class NotificationService(INotificationManager notificationManager, IChatGui chat, IClientState clientState)
    : INotificationService
{
    private readonly ConcurrentQueue<NotificationData> pending = new();

    public void NotifyWarning(
        string title,
        string description,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        DetailedLog.Warn($"{title} {description}", memberName, sourceFilePath, sourceLineNumber);
        pending.Enqueue(new NotificationData(title, description, NotificationType.Warning));
    }

    public void NotifyError(
        string title,
        string description,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        DetailedLog.Error($"{title} {description}", memberName, sourceFilePath, sourceLineNumber);
        pending.Enqueue(new NotificationData(title, description, NotificationType.Error));
    }

    /// <summary>
    /// Wrapper around <see cref="ProcessNotifications()"/> to allow for hooking it up to
    /// <see cref="IFramework.Update"/>.
    /// </summary>
    /// <param name="f"></param>
    public void ProcessNotifications(IFramework f) => ProcessNotifications();

    /// <summary>
    /// Process all queued notifications and show them to the user. This is intended
    /// to be called as part of a frame handler.
    /// </summary>
    public void ProcessNotifications()
    {
        // Notifications won't show up in chat on login if we try to print on the title screen etc.
        if (!clientState.IsLoggedIn) return;

        while (pending.TryDequeue(out var notificationData))
        {
            var (title, description, type) = notificationData;
            var message = $"{title}\n{description}";

            if (type == NotificationType.Error)
                chat.PrintError(message);
            else
                chat.Print(message);

            var notification = new Notification
            {
                Title = title,
                Content = description,
                MinimizedText = title,
                Type = type,
            };
            notificationManager.AddNotification(notification);
        }
    }

    private record NotificationData(string title, string description, NotificationType type);
}