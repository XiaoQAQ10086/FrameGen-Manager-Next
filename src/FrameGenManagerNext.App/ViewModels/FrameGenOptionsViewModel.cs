using CommunityToolkit.Mvvm.ComponentModel;
using FrameGenManagerNext.Core.Features.Framegen;

namespace FrameGenManagerNext.App.ViewModels;

public sealed partial class FrameGenOptionsViewModel : ObservableObject
{
    public IReadOnlyList<string> ProxySlots { get; } = FrameGenOptionCatalog.ProxySlots;

    public IReadOnlyList<int> OptimizedLevels { get; } = FrameGenOptionCatalog.OptimizedLevels;

    public IReadOnlyList<int> FrameMultipliers { get; } = FrameGenOptionCatalog.FrameMultipliers;

    public IReadOnlyList<string> Presets { get; } = FrameGenOptionCatalog.Presets;

    public IReadOnlyList<string> Engines { get; } = FrameGenOptionCatalog.Engines;

    public IReadOnlyList<string> SuperResolutionModes { get; } = FrameGenOptionCatalog.SuperResolutionModes;

    public IReadOnlyList<string> XeSsFlowResolutions { get; } = FrameGenOptionCatalog.XeSsFlowResolutions;

    public IReadOnlyList<string> XeSsHudlessValues { get; } = FrameGenOptionCatalog.XeSsHudlessValues;

    [ObservableProperty]
    private string _proxySlot = "version.dll";

    [ObservableProperty]
    private int _optimized = 1;

    [ObservableProperty]
    private int _frames = 3;

    [ObservableProperty]
    private string _preset = "A";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsXeSsEngine))]
    private string _engine = "DLSS";

    public bool IsXeSsEngine => string.Equals(Engine, "XeSS", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    private string _superResolutionMode = "Off";

    [ObservableProperty]
    private string _xeSsFlow = "Middle";

    [ObservableProperty]
    private string _xeSsHudless = "Auto";

    public FrameGenOptions ToOptions()
    {
        return new FrameGenOptions(
            ProxySlot,
            Optimized,
            Frames,
            Preset,
            Engine,
            SuperResolutionMode,
            XeSsFlow,
            XeSsHudless);
    }
}
