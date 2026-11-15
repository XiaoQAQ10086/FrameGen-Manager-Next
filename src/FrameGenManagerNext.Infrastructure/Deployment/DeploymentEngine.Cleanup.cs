using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Results;

namespace FrameGenManagerNext.Infrastructure.Deployment;

public sealed partial class DeploymentEngine
{
    /// <summary>
    /// Removes the proxies the previous deployment left behind but the new plan does not contain,
    /// before any new file is written. Upstream keeps exactly one proxy next to the game EXE and
    /// tells users to delete the old one first, because the proxy the process loads first decides
    /// which version runs. Files the game shipped itself are restored from their backup first.
    /// </summary>
    private static async Task<Result<List<string>>> RemoveStaleProxiesAsync(
        DeploymentManifest? previous,
        DeploymentPlan plan,
        string targetDirectory,
        string backupDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            var warnings = await CleanupStaleAsync(
                previous,
                plan,
                targetDirectory,
                backupDirectory,
                cancellationToken,
                ProxyRole);
            return Result<List<string>>.Ok(warnings);
        }
        catch (OperationCanceledException)
        {
            return Result<List<string>>.Fail(new AppError(ErrorCode.OperationCancelled));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result<List<string>>.Fail(MapIoError(exception, targetDirectory));
        }
        catch (TimeoutException exception)
        {
            return Result<List<string>>.Fail(AppError.Create(
                ErrorCode.OperationTimedOut,
                ("target", targetDirectory),
                ("reason", exception.Message)));
        }
    }

    private async Task<Result> SaveManifestAsync(
        DeploymentManifest manifest,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            await _backups.SaveManifestAsync(manifest, cancellationToken);
            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            return Result.Fail(new AppError(ErrorCode.OperationCancelled));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Fail(MapIoError(exception, targetDirectory));
        }
        catch (TimeoutException exception)
        {
            return Result.Fail(AppError.Create(
                ErrorCode.OperationTimedOut,
                ("target", targetDirectory),
                ("reason", exception.Message)));
        }
    }
}
