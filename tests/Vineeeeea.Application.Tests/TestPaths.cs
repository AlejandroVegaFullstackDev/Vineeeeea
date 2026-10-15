namespace Vineeeeea.Application.Tests;

internal static class TestPaths
{
    /// <summary>Turns a Windows-style absolute path into one that is absolute on the running OS.</summary>
    public static string Abs(string windowsPath) =>
        OperatingSystem.IsWindows() ? windowsPath : windowsPath[2..].Replace('\\', '/');
}
