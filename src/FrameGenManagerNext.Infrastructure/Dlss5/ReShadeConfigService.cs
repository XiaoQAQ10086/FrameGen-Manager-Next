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
}
