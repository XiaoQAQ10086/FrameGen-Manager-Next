using FrameGenManagerNext.Domain.Gpu;

namespace FrameGenManagerNext.Core.Features.Gpu;

public sealed class GpuService : IGpuService
{
    private readonly IGpuProbe _probe;
    private readonly object _gate = new();
    private Task<GpuInfo>? _probeTask;

    public GpuService(IGpuProbe probe)
    {
        _probe = probe;
    }

    public Task<GpuInfo> GetAsync(CancellationToken cancellationToken)
    {
        var task = GetOrStartProbe();
        return cancellationToken.CanBeCanceled
            ? task.WaitAsync(cancellationToken)
            : task;
    }

    private Task<GpuInfo> GetOrStartProbe()
    {
        lock (_gate)
        {
            if (_probeTask is { IsFaulted: false, IsCanceled: false })
            {
                return _probeTask;
            }

            _probeTask = ProbeAsync();
            return _probeTask;
        }
    }

    private async Task<GpuInfo> ProbeAsync()
    {
        try
        {
            // One probe per process: page switches and parallel state requests share
            // the result, and a caller cancelling must not cancel the shared probe.
            return await _probe.GetAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            lock (_gate)
            {
                _probeTask = null;
            }

            throw;
        }
    }
}
