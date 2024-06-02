using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Plugin.Services;
using Moq;
using TextToTalk.Services;
using Xunit;

namespace TextToTalk.Tests.Services;

public class NotificationServiceTests
{
    [Fact]
    public void NotifyWarning_SendsWarning()
    {
        var notificationManager = new Mock<INotificationManager>();
        var chatGui = new Mock<IChatGui>();
        var clientState = new Mock<IClientState>();

        notificationManager.Setup(x => x.AddNotification(IsType(NotificationType.Warning))).Verifiable();
        chatGui.Setup(x => x.Print(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<ushort?>())).Verifiable();
        clientState.Setup(x => x.IsLoggedIn).Returns(true);

        var notificationService =
            new NotificationService(notificationManager.Object, chatGui.Object, clientState.Object);
        notificationService.NotifyWarning("This is a warning.", "");
        notificationService.ProcessNotifications();

        notificationManager.Verify();
        chatGui.Verify();
        clientState.Verify();
    }

    [Fact]
    public void NotifyError_SendsError()
    {
        var notificationManager = new Mock<INotificationManager>();
        var chatGui = new Mock<IChatGui>();
        var clientState = new Mock<IClientState>();

        notificationManager.Setup(x => x.AddNotification(IsType(NotificationType.Error))).Verifiable();
        chatGui.Setup(x => x.PrintError(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<ushort?>())).Verifiable();
        clientState.Setup(x => x.IsLoggedIn).Returns(true);

        var notificationService =
            new NotificationService(notificationManager.Object, chatGui.Object, clientState.Object);
        notificationService.NotifyError("This is an error.", "");
        notificationService.ProcessNotifications();

        notificationManager.Verify();
        chatGui.Verify();
        clientState.Verify();
    }

    private static Notification IsType(NotificationType type)
    {
        return It.Is<Notification>(it => it.Type == type);
    }
}