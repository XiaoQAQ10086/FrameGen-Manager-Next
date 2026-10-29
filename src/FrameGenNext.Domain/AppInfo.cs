using System.Reflection;

namespace FrameGenNext.Domain;

/// <summary>
/// Product identity shared by the app, CLI, diagnostics and packaging.
/// </summary>
public static class AppInfo
{
    public const string ProductName = "FrameGen Next";
    public const string DataDirectoryName = "FrameGenNext";
    public const string ExecutableBaseName = "FrameGenNext";
    public const string UpdateFeedId = "FrameGenNext";

    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var assembly = typeof(AppInfo).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var buildMetadataIndex = informational.IndexOf('+');
            return buildMetadataIndex >= 0 ? informational[..buildMetadataIndex] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
