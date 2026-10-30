using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using FrameGenManagerNext.Domain.Library;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.App.Web;

public sealed partial class AppWebBridge
{
    private const int CoverThumbnailWidth = 360;

    private readonly ConcurrentDictionary<string, string> _coverUrlCache =
        new(StringComparer.OrdinalIgnoreCase);

    private async Task<string?> GetCoverUrlAsync(GameEntry game)
    {
        if (_coverUrlCache.TryGetValue(game.Id, out var cachedUrl))
        {
            return cachedUrl;
        }

        try
        {
            var path = await _covers.GetCoverPathAsync(game, CancellationToken.None);
            string? url;
            if (path is null || !File.Exists(path))
            {
                url = await GetExecutableIconUrlAsync(game.RenderExePath);
            }
            else
            {
                var info = new FileInfo(path);
                url = info.Length is <= 0 or > 3 * 1024 * 1024 || !IsCoverLargeEnough(path)
                    ? await GetExecutableIconUrlAsync(game.RenderExePath)
                    : CoverUrl(await EnsureCachedCoverAsync(path));
            }

            if (!string.IsNullOrWhiteSpace(url))
            {
                _coverUrlCache[game.Id] = url;
            }

            return url;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(exception, "Could not load cover for {Game}", game.Name);
            return null;
        }
    }

    private async Task<string?> GetExecutableIconUrlAsync(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return null;
        }

        try
        {
            var cacheDirectory = Path.Combine(_paths.CacheDirectory, "covers", "icons");
            Directory.CreateDirectory(cacheDirectory);
            var key = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes($"{exePath.ToLowerInvariant()}|{File.GetLastWriteTimeUtc(exePath).Ticks}")));
            var cachePath = Path.Combine(cacheDirectory, $"{key}.png");

            if (!File.Exists(cachePath))
            {
                await Task.Run(() =>
                {
                    using var icon = Icon.ExtractAssociatedIcon(exePath);
                    if (icon is null)
                    {
                        return;
                    }

                    using var bitmap = icon.ToBitmap();
                    bitmap.Save(cachePath, ImageFormat.Png);
                });
            }

            return File.Exists(cachePath) && new FileInfo(cachePath).Length <= 3 * 1024 * 1024
                ? CoverUrl(cachePath)
                : null;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or ExternalException)
        {
            _logger.LogDebug(exception, "Could not extract executable icon for {Exe}", exePath);
            return null;
        }
    }

    private async Task<string> EnsureCachedCoverAsync(string source)
    {
        var full = Path.GetFullPath(source);
        var root = Path.GetFullPath(_paths.CacheDirectory);
        var thumbsDirectory = Path.Combine(root, "covers", "thumbs");
        if (full.StartsWith(Path.GetFullPath(thumbsDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return full;
        }

        var key = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{full.ToLowerInvariant()}|{File.GetLastWriteTimeUtc(full).Ticks}")))
            .ToLowerInvariant();
        Directory.CreateDirectory(thumbsDirectory);
        var destination = Path.Combine(thumbsDirectory, key + ".jpg");
        if (File.Exists(destination) && new FileInfo(destination).Length > 0)
        {
            return destination;
        }

        try
        {
            await Task.Run(() => CreateCoverThumbnail(full, destination));
        }
        catch (Exception exception) when (
            exception is ArgumentException or ExternalException or IOException or OutOfMemoryException)
        {
            _logger.LogDebug(exception, "Could not create cover thumbnail for {Source}", full);
            return full;
        }

        return File.Exists(destination) ? destination : full;
    }

    private static void CreateCoverThumbnail(string source, string destination)
    {
        using var image = Image.FromFile(source);
        var width = Math.Min(CoverThumbnailWidth, image.Width);
        var height = Math.Max(1, (int)Math.Round(image.Height * (width / (double)image.Width)));
        using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            graphics.DrawImage(image, 0, 0, width, height);
        }

        var codec = ImageCodecInfo.GetImageEncoders()
            .First(encoder => encoder.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 86L);
        var temp = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            bitmap.Save(temp, codec, parameters);
            File.Move(temp, destination, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup.
            }
        }
    }

    private static bool IsCoverLargeEnough(string path)
    {
        try
        {
            using var image = Image.FromFile(path);
            return image.Width >= 500;
        }
        catch (Exception exception) when (exception is ArgumentException or ExternalException or IOException)
        {
            return false;
        }
    }

    private string CoverUrl(string path)
    {
        var relative = Path.GetRelativePath(_paths.CacheDirectory, path).Replace('\\', '/');
        return $"https://fgm-covers.local/{relative}";
    }

}
