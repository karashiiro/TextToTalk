using System.Diagnostics;
using System.Reflection;

namespace TextToTalk.UI.SourceGeneration;

internal static class ToolInfo
{
    internal static readonly Assembly ToolAssembly = typeof(ConfigComponentsGenerator).Assembly;

    internal static readonly string AssemblyName = ToolAssembly.GetName().Name!;

    internal static readonly string AssemblyVersion = FileVersionInfo.GetVersionInfo(ToolAssembly.Location).FileVersion!;
}