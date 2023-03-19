namespace TextToTalk.UI.SourceGeneration.Contexts;

public class ToolContext
{
    public string AssemblyName { get; }

    public string AssemblyVersion { get; }

    public ToolContext()
    {
        AssemblyName = ToolInfo.AssemblyName;
        AssemblyVersion = ToolInfo.AssemblyVersion;
    }
}