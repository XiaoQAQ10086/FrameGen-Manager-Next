using System.ComponentModel;
using System.Diagnostics;
using FrameGenManagerNext.Core.Features.Library;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Library;
using FrameGenManagerNext.Domain.Results;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.Infrastructure.Library;

public sealed class GameLauncher : IGameLauncher
{
    private readonly ILogger<GameLauncher> _logger;

    public GameLauncher(ILogger<GameLauncher> logger)
    {
        _logger = logger;
    }

    public Task<Result> LaunchAsync(GameEntry game, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(game);
        cancellationToken.ThrowIfCancellationRequested();

        var kind = GameLaunchPlanner.Resolve(game);
        return Task.FromResult(kind switch
        {
            GameLaunchKind.Steam => Start(
                $"steam://rungameid/{game.AppId}",
                workingDirectory: null,
                game,
                "steam"),
            GameLaunchKind.Epic => Start(
                $"com.epicgames.launcher://apps/{Uri.EscapeDataString(game.AppId!)}?action=launch&silent=true",
                workingDirectory: null,
                game,
                "epic"),
            GameLaunchKind.Executable => StartExecutable(game),
            _ => Result.Fail(new AppError(
                ErrorCode.LaunchUnsupported,
                "this game must be started from its platform")),
        });
    }

    private Result StartExecutable(GameEntry game)
    {
        var exePath = game.RenderExePath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return Result.Fail(new AppError(ErrorCode.FileNotFound, "render executable not found"));
        }

        var directory = Path.GetDirectoryName(exePath);
        return Start(exePath, directory, game, "exe");
    }

    private Result Start(string target, string? workingDirectory, GameEntry game, string mode)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true,
            };
            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            using var process = Process.Start(startInfo);
            _logger.LogInformation(
                "game.launch game={GameId} launcher={Launcher} mode={Mode}",
                game.Id,
                game.Launcher,
                mode);
            return Result.Ok();
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            _logger.LogWarning(
                exception,
                "game.launch failed game={GameId} launcher={Launcher} mode={Mode}",
                game.Id,
                game.Launcher,
                mode);
            return Result.Fail(new AppError(ErrorCode.LaunchFailed, exception.Message));
        }
    }
}
