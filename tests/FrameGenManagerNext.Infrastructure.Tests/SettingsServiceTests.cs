using FrameGenManagerNext.Domain.Settings;
using FrameGenManagerNext.Infrastructure.Paths;
using FrameGenManagerNext.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly TestDirectory _directory = new("fgm-settings-tests");
    private readonly AppPaths _paths;
    private readonly SettingsService _service;

    public SettingsServiceTests()
    {
        _paths = new AppPaths(_directory.GetPath("data"));
        _paths.EnsureCreated();
        _service = new SettingsService(_paths, NullLogger<SettingsService>.Instance);
    }

    [Fact]
    public void MissingFileReturnsDefaults()
    {
        var settings = _service.Load();

        settings.ShouldBe(AppSettings.Default);
    }

    [Fact]
    public async Task SaveAndLoadRoundTrips()
    {
        var updated = AppSettings.Default with
        {
            Theme = AppTheme.Light,
            AutoScanOnStartup = true,
            ShowCovers = false,
        };

        var result = await _service.SaveAsync(updated, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        var loaded = _service.Load();
        loaded.ShouldBe(updated);
    }

    [Fact]
    public void ExistingLanguageChoiceStillLoads()
    {
        var path = Path.Combine(_paths.ConfigDirectory, "app.json");
        File.WriteAllText(path, """{"schemaVersion":1,"language":"english","theme":"light","autoScanOnStartup":false,"showCovers":true}""");

        var settings = _service.Load();

        settings.Language.ShouldBe(AppLanguage.English);
    }

    [Fact]
    public void UnknownLanguageFallsBackToSystem()
    {
        var path = Path.Combine(_paths.ConfigDirectory, "app.json");
        File.WriteAllText(path, """{"schemaVersion":1,"language":"klingon","theme":"light","autoScanOnStartup":false,"showCovers":true}""");

        var settings = _service.Load();

        settings.Language.ShouldBe(AppLanguage.System);
    }

    [Fact]
    public void CorruptFileIsQuarantined()
    {
        var path = Path.Combine(_paths.ConfigDirectory, "app.json");
        File.WriteAllText(path, "{ not json");

        var settings = _service.Load();

        settings.ShouldBe(AppSettings.Default);
        File.Exists(path + ".bad").ShouldBeTrue();
    }

    public void Dispose()
    {
        _directory.Dispose();
    }
}
