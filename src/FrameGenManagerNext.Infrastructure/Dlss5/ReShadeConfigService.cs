using FrameGenManagerNext.Core.Features.Dlss5;
using FrameGenManagerNext.Domain.Ini;

namespace FrameGenManagerNext.Infrastructure.Dlss5;

public sealed class ReShadeConfigService : IReShadeConfigService
{
    public async Task MergeSearchPathsAsync(
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        var iniPath = Path.Combine(targetDirectory, "ReShade.ini");
        if (!File.Exists(iniPath))
        {
            return;
        }

        var text = await File.ReadAllTextAsync(iniPath, cancellationToken);
        var document = IniDocument.Parse(text);

        document.SetValue("General", "EffectSearchPaths", @".\reshade-shaders\Shaders\**");
        document.SetValue("General", "TextureSearchPaths", @".\reshade-shaders\Textures\**");

        await File.WriteAllTextAsync(iniPath, document.ToText("\r\n"), cancellationToken);
    }

    public async Task EnsureOverlayKeyAsync(
        string targetDirectory,
        int virtualKey,
        CancellationToken cancellationToken)
    {
        if (virtualKey is < 1 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(virtualKey));
        }

        var iniPath = Path.Combine(targetDirectory, "ReShade.ini");
        var document = File.Exists(iniPath)
            ? IniDocument.Parse(await File.ReadAllTextAsync(iniPath, cancellationToken))
            : IniDocument.Parse(string.Empty);
        document.SetValue("INPUT", "KeyOverlay", $"{virtualKey},0,0,0");
        await File.WriteAllTextAsync(iniPath, document.ToText("\r\n"), cancellationToken);
    }
}
