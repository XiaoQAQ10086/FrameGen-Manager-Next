namespace FrameGenManagerNext.Domain.Library;

/// <summary>
/// Maps PE imports, sibling DLLs and EXE name tokens to a graphics API.
/// Order matches the old manager: DXVK wrapper, filename evidence, imports, runtime siblings.
/// </summary>
public static class GraphicsApiClassifier
{
    public static GraphicsApi Classify(GraphicsApiFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (facts.WrapperIsVulkan)
        {
            return GraphicsApi.Vulkan;
        }

        var fromExeName = FromExeName(facts.ExeName);
        if (fromExeName != GraphicsApi.Unknown)
        {
            return fromExeName;
        }

        var fromImports = FromNames(Normalize(facts.ImportedModules));
        if (fromImports != GraphicsApi.Unknown)
        {
            return fromImports;
        }

        var siblings = Normalize(facts.SiblingFiles);
        if (siblings.Contains("vulkan-1.dll"))
        {
            return GraphicsApi.Vulkan;
        }

        if (siblings.Contains("d3d12.dll"))
        {
            return GraphicsApi.Direct3D12;
        }

        return GraphicsApi.Unknown;
    }

    private static GraphicsApi FromExeName(string? exeName)
    {
        if (string.IsNullOrWhiteSpace(exeName))
        {
            return GraphicsApi.Unknown;
        }

        var tokens = new HashSet<string>(
            exeName
                .ToLowerInvariant()
                .Split(
                    ['.', '-', '_', ' '],
                    StringSplitOptions.RemoveEmptyEntries),
            StringComparer.Ordinal);

        if (tokens.Contains("d3d12") || tokens.Contains("dx12"))
        {
            return GraphicsApi.Direct3D12;
        }

        if (tokens.Contains("vulkan") || tokens.Contains("vk"))
        {
            return GraphicsApi.Vulkan;
        }

        if (tokens.Contains("d3d11") || tokens.Contains("dx11"))
        {
            return GraphicsApi.Direct3D11;
        }

        if (tokens.Contains("d3d10") || tokens.Contains("dx10"))
        {
            return GraphicsApi.Direct3D10;
        }

        if (tokens.Contains("d3d9") || tokens.Contains("dx9"))
        {
            return GraphicsApi.Direct3D9;
        }

        if (tokens.Contains("opengl") || tokens.Contains("gl"))
        {
            return GraphicsApi.OpenGl;
        }

        return GraphicsApi.Unknown;
    }

    private static GraphicsApi FromNames(HashSet<string> names)
    {
        if (names.Contains("d3d12.dll") || names.Contains("d3d12core.dll"))
        {
            return GraphicsApi.Direct3D12;
        }

        if (names.Contains("vulkan-1.dll"))
        {
            return GraphicsApi.Vulkan;
        }

        if (names.Contains("d3d11.dll"))
        {
            return GraphicsApi.Direct3D11;
        }

        if (names.Contains("d3d10.dll") || names.Contains("d3d10_1.dll"))
        {
            return GraphicsApi.Direct3D10;
        }

        if (names.Contains("d3d9.dll"))
        {
            return GraphicsApi.Direct3D9;
        }

        if (names.Contains("opengl32.dll"))
        {
            return GraphicsApi.OpenGl;
        }

        return GraphicsApi.Unknown;
    }

    private static HashSet<string> Normalize(IReadOnlyCollection<string> names)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (names is null)
        {
            return result;
        }

        foreach (var name in names)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                result.Add(name.Trim().ToLowerInvariant());
            }
        }

        return result;
    }
}
