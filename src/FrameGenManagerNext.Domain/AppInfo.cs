using System.Reflection;

namespace FrameGenManagerNext.Domain;

/// <summary>
/// Product identity shared by the app, CLI, diagnostics and packaging.
/// </summary>
public static class AppInfo
{
    public const string ProductName = "FrameGen Manager-Next";
    public const string DataDirectoryName = "FrameGen-Manager-Next";
    public const string ExecutableBaseName = "FrameGen-Manager-Next";
    public const string UpdateFeedId = "FrameGenManagerNext";

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
