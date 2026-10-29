using System.IO;
using FrameGenManagerNext.App.Localization;
using Microsoft.Extensions.Logging;

namespace FrameGenManagerNext.App.ViewModels;

public sealed partial class LibraryViewModel
{
    private async Task RefreshDeploymentStatesAsync()
    {
        var games = Games.ToList();
        using var gate = new SemaphoreSlim(4);
        var tasks = games.Select(async game =>
        {
            await gate.WaitAsync();
            try
            {
                var result = await _frameGen.InspectAsync(
                    game.TargetDirectory,
                    Options.ToOptions(),
                    CancellationToken.None);

                if (result.IsSuccess)
                {
                    game.ApplyInspection(result.Value);
                }
                else
                {
                    game.DeploymentStateDisplay = result.Error!.Code.ToString();
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not inspect deployment state for {Game}", game.Name);
                game.DeploymentStateDisplay = L.T("State.CheckFailed");
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task LoadCoversAsync()
    {
        foreach (var game in Games)
        {
            try
            {
                game.CoverPath = await _covers.GetCoverPathAsync(game.ToEntry(), CancellationToken.None);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(exception, "Could not resolve cover for {Game}", game.Name);
            }
        }
    }
}
