using System.Text.Json;
using FrameGenManagerNext.Domain.Errors;
using FrameGenManagerNext.Domain.Gpu;
using FrameGenManagerNext.Domain.Results;
using FrameGenManagerNext.Infrastructure.Gpu;
using FrameGenManagerNext.Infrastructure.Paths;

namespace FrameGenManagerNext.Elevated;

internal static class Program
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            return 2;
        }

        var action = args[0];
        var argument = args[1];
        var resultPath = args[2];

        Result<GpuSpoofOperationReport> result;
        try
        {
            var paths = AppPaths.CreateDefault();
            result = action switch
            {
                "--gpu-apply" => GpuSpoofOperations.Apply(paths, argument),
                "--gpu-restore" => GpuSpoofOperations.Restore(
                    paths,
                    argument.Equals("driver", StringComparison.OrdinalIgnoreCase)
                        ? GpuSpoofRestoreTarget.DriverName
                        : GpuSpoofRestoreTarget.OriginalValue),
                _ => Result<GpuSpoofOperationReport>.Fail(new AppError(
                    ErrorCode.InvalidArgument,
                    "Unsupported elevated operation.")),
            };
        }
        catch (Exception exception)
        {
            result = Result<GpuSpoofOperationReport>.Fail(new AppError(
                ErrorCode.GpuSpoofFailed,
                exception.Message));
        }

        WriteResult(resultPath, result);
        return result.IsSuccess ? 0 : 1;
    }

    private static void WriteResult(string path, Result<GpuSpoofOperationReport> result)
    {
        var payload = new
        {
            ok = result.IsSuccess,
            code = result.IsSuccess ? null : result.Error!.Code.ToString(),
            message = result.IsSuccess ? result.Value.Message : result.Error!.Diagnostic,
        };

        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(payload, Options));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The parent reports a missing result when this fails.
        }
    }
}
