using System;
using System.Diagnostics;
using System.Reflection;

namespace TextToTalk.UI.SourceGeneration;

internal static class ToolInfo
{
    internal static readonly Assembly ToolAssembly =
        RunWithConsoleLogging(() => typeof(ConfigComponentsGenerator).Assembly);

    internal static readonly string AssemblyName =
        RunWithConsoleLogging(() => ToolAssembly.GetName().Name!);

    internal static readonly string AssemblyVersion =
        RunWithConsoleLogging(() => FileVersionInfo.GetVersionInfo(ToolAssembly.Location).FileVersion!);

    private static T RunWithConsoleLogging<T>(Func<T> fn)
    {
        try
        {
            return fn();
        }
        catch (Exception ex)
        {
            LogException(ex);
            throw;
        }
    }

    private static void LogException(Exception ex)
    {
        while (true)
        {
            Console.WriteLine(ex);
            if (ex.InnerException != null)
            {
                ex = ex.InnerException;
                continue;
            }

            break;
        }
    }
}