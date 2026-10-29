using FrameGenManagerNext.Infrastructure.Library;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class PeImportReaderTests
{
    [Fact]
    public void ReadsImportsFromNotepad()
    {
        var notepad = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "notepad.exe");

        File.Exists(notepad).ShouldBeTrue("notepad.exe is expected on Windows");

        var imports = PeImportReader.GetImportedDlls(notepad);

        imports.ShouldNotBeEmpty();
    }

    [Fact]
    public void MissingFileReturnsEmptyList()
    {
        PeImportReader.GetImportedDlls("Z:\\does-not-exist\\missing.exe").ShouldBeEmpty();
    }
}
