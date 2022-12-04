using TextToTalk.UI.Dalamud;

namespace TextToTalk.UI;

public class WindowController
{
    private readonly UnlockerResultWindow unlockerResultWindow;
    private readonly ChannelPresetModificationWindow channelPresetModificationWindow;

    public WindowController(UnlockerResultWindow unlockerResultWindow, ChannelPresetModificationWindow channelPresetModificationWindow)
    {
        this.unlockerResultWindow = unlockerResultWindow;
        this.channelPresetModificationWindow = channelPresetModificationWindow;
    }

    public void SetUnlockerResult(string result)
    {
        this.unlockerResultWindow.Text = result;
    }

    public void OpenUnlockerResultWindow()
    {
        this.unlockerResultWindow.IsOpen = true;
    }
    
    public void OpenChannelPresetModificationWindow()
    {
        this.channelPresetModificationWindow.IsOpen = true;
    }
}