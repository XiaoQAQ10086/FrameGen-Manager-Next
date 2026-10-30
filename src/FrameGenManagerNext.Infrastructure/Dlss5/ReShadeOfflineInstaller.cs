using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.PortableExecutable;
using FrameGenManagerNext.Core.Features.Dlss5;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Library;
using FrameGenManagerNext.Domain.Results;

namespace FrameGenManagerNext.Infrastructure.Dlss5;

/// <summary>
/// ReShade ships its module inside the setup executable as an appended ZIP.
/// Extracting that file avoids running the setup, which contacts the network to
/// download compatibility data and can take a minute or more.
/// </summary>
public sealed class ReShadeOfflineInstaller : IReShadeOfflineInstaller
{
    private const string ReShade64Entry = "ReShade64.dll";
    private const string ReShade32Entry = "ReShade32.dll";

    public async Task<Result<string>> InstallAsync(
        string setupPath,
        string targetDirectory,
        string exePath,
        GraphicsApi api,
        IProgress<DeploymentProgress>? progress,
        CancellationToken cancellationToken)
    {
        var module = ModuleName(api);
        if (module is null)
        {
            return Result<string>.Fail(AppError.Create(
                ErrorCode.InstallerFailed,
                ("api", api.ToString()),
                ("reason", "ReShade's Vulkan layer cannot be installed without running the setup")));
        }

        if (!File.Exists(setupPath))
        {
            return Result<string>.Fail(AppError.Create(
                ErrorCode.AssetMissing,
                ("file", setupPath)));
        }

        var entry = Is64BitExecutable(exePath) ? ReShade64Entry : ReShade32Entry;
        progress?.Report(new DeploymentProgress("installer", "ReShade", 0, 0));

        var dll = await Task.Run(
            () => ExtractEmbeddedModule(setupPath, entry, cancellationToken),
            cancellationToken);
        if (dll.IsFailure)
        {
            return Result<string>.Fail(dll.Error!);
        }

        var modulePath = Path.Combine(targetDirectory, module);
        if (File.Exists(modulePath) && !IsReShade(modulePath))
        {
            return Result<string>.Fail(AppError.Create(
                ErrorCode.Conflict,
                ("path", modulePath),
                ("reason", "the target module belongs to another program and was not overwritten")));
        }

        var tempPath = modulePath + ".fgm-tmp";
        await File.WriteAllBytesAsync(tempPath, dll.Value, cancellationToken);
        File.Move(tempPath, modulePath, overwrite: true);

        var iniPath = Path.Combine(targetDirectory, "ReShade.ini");
        if (!File.Exists(iniPath))
        {
            await File.WriteAllTextAsync(iniPath, DefaultIni, cancellationToken);
        }

        progress?.Report(new DeploymentProgress("writing", module, 1, 1));
        return Result<string>.Ok(module);
    }

    private static Result<byte[]> ExtractEmbeddedModule(
        string setupPath,
        string entryName,
        CancellationToken cancellationToken)
    {
        var bytes = File.ReadAllBytes(setupPath);
        for (var offset = 0; offset + 30 <= bytes.Length; offset += 512)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (bytes[offset] != 'P' ||
                bytes[offset + 1] != 'K' ||
                bytes[offset + 2] != 0x03 ||
                bytes[offset + 3] != 0x04 ||
                !bytes.AsSpan(offset + 4, 26).ContainsAnyExcept((byte)0))
            {
                continue;
            }

            try
            {
                using var stream = new MemoryStream(bytes, offset, bytes.Length - offset, writable: false);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                var entry = archive.Entries.FirstOrDefault(candidate =>
                    string.Equals(
                        Path.GetFileName(candidate.FullName.Replace('\\', '/')),
                        entryName,
                        StringComparison.OrdinalIgnoreCase));
                if (entry is null)
                {
                    continue;
                }

                using var entryStream = entry.Open();
                using var memory = new MemoryStream();
                entryStream.CopyTo(memory);
                return Result<byte[]>.Ok(memory.ToArray());
            }
            catch (InvalidDataException)
            {
                // Not a real ZIP start; keep scanning.
            }
        }

        return Result<byte[]>.Fail(AppError.Create(
            ErrorCode.InstallerFailed,
            ("file", setupPath),
            ("reason", $"{entryName} was not found inside the setup executable")));
    }

    private static bool Is64BitExecutable(string exePath)
    {
        try
        {
            using var stream = File.OpenRead(exePath);
            using var reader = new PEReader(stream);
            return reader.PEHeaders.CoffHeader.Machine is Machine.Amd64 or Machine.Arm64;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
            return true;
        }
    }

    private static bool IsReShade(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return info.ProductName?.Contains("ReShade", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    private static string? ModuleName(GraphicsApi api)
    {
        return api switch
        {
            GraphicsApi.Direct3D9 => "d3d9.dll",
            GraphicsApi.Direct3D10 => "d3d10.dll",
            GraphicsApi.Direct3D11 => "d3d11.dll",
            GraphicsApi.Direct3D12 => "d3d12.dll",
            GraphicsApi.OpenGl => "opengl32.dll",
            _ => null,
        };
    }

    private const string DefaultIni =
        "[GENERAL]\r\n" +
        "EffectSearchPaths=.\\reshade-shaders\\Shaders\\**\r\n" +
        "TextureSearchPaths=.\\reshade-shaders\\Textures\\**\r\n" +
        "PreprocessorDefinitions=RESHADE_DEPTH_LINEARIZATION_FAR_PLANE=1000.0,RESHADE_DEPTH_INPUT_IS_UPSIDE_DOWN=0,RESHADE_DEPTH_INPUT_IS_REVERSED=1,RESHADE_DEPTH_INPUT_IS_LOGARITHMIC=0\r\n" +
        "\r\n" +
        "[INPUT]\r\n" +
        "GamepadNavigation=0\r\n" +
        "KeyOverlay=36,0,0,0\r\n";
}
