using System.Buffers;
using System.Security.Cryptography;
using FrameGenManagerNext.Domain.Deployment;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Results;

namespace FrameGenManagerNext.Infrastructure.Deployment;

public sealed partial class DeploymentEngine
{
    private static async Task<Result> VerifyPlannedFileAsync(
        DeploymentFilePlan file,
        IProgress<DeploymentProgress>? progress,
        int current,
        int total,
        CancellationToken cancellationToken)
    {
        if (file.SourcePath is not null)
        {
            string hash;
            try
            {
                hash = await HashFileAsync(
                    file.SourcePath,
                    cancellationToken,
                    progress,
                    "preflight",
                    file.RelativePath,
                    current,
                    total);
            }
            catch (TimeoutException exception)
            {
                return Result.Fail(AppError.Create(
                    ErrorCode.OperationTimedOut,
                    ("file", file.SourcePath),
                    ("reason", exception.Message)));
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                return Result.Fail(AppError.Create(
                    ErrorCode.PackInvalid,
                    ("file", file.SourcePath),
                    ("reason", exception.Message)));
            }

            if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Fail(AppError.Create(
                    ErrorCode.PackHashMismatch,
                    ("file", file.SourcePath)));
            }

            return Result.Ok();
        }

        if (file.Content is null)
        {
            return Result.Fail(new AppError(ErrorCode.InvalidArgument, "file has no content"));
        }

        var contentHash = Convert.ToHexString(SHA256.HashData(file.Content)).ToLowerInvariant();
        if (!string.Equals(contentHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail(new AppError(ErrorCode.PackHashMismatch, "generated content hash mismatch"));
        }

        return Result.Ok();
    }

    private static async Task<Result<string>> TryHashFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            return Result<string>.Ok(await HashFileAsync(path, cancellationToken));
        }
        catch (TimeoutException exception)
        {
            return Result<string>.Fail(AppError.Create(
                ErrorCode.OperationTimedOut,
                ("file", path),
                ("reason", exception.Message)));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return Result<string>.Fail(AppError.Create(
                ErrorCode.FileLocked,
                ("file", path),
                ("reason", exception.Message)));
        }
    }

    private static readonly TimeSpan HashStallTimeout = TimeSpan.FromSeconds(30);

    private static async Task<string> HashFileAsync(
        string path,
        CancellationToken cancellationToken,
        IProgress<DeploymentProgress>? progress = null,
        string phase = "preflight",
        string subject = "",
        int current = 0,
        int total = 0)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1024 * 1024,
            useAsync: true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                stall.CancelAfter(HashStallTimeout);

                int read;
                try
                {
                    read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), stall.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        $"Reading {Path.GetFileName(path)} made no progress for {HashStallTimeout.TotalSeconds:0} seconds.");
                }

                if (read == 0)
                {
                    break;
                }

                hash.AppendData(buffer, 0, read);
                progress?.Report(new DeploymentProgress(phase, subject, current, total));
            }

            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

}
