using System.Runtime.InteropServices;
using FrameGenManagerNext.Infrastructure.NvApi;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class NvApiApplicationBufferTests
{
    [Fact]
    public void ApplicationBufferUsesBareNameAndSeparateDirectory()
    {
        const string executable = @"C:\Games\Banana Airways Demo\BananaAirways.exe";
        var buffer = NvApiNative.CreateApplicationBuffer(executable);
        try
        {
            NvApiNative.ReadString(buffer, 8, 2048)
                .ShouldBe("BananaAirways.exe");
            NvApiNative.ReadString(buffer, NvApiNative.ApplicationFileInFolderOffset, 2048)
                .ShouldBe(@"C:\Games\Banana Airways Demo");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
