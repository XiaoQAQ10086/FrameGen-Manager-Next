using System.Reflection;
using FrameGenNext.Domain;
using Shouldly;

namespace FrameGenNext.Core.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void CoreDoesNotReferenceUiFrameworks()
    {
        var referenced = GetReferences(typeof(CoreAssemblyMarker).Assembly);

        referenced.ShouldNotContain("PresentationFramework");
        referenced.ShouldNotContain("PresentationCore");
        referenced.ShouldNotContain("WindowsBase");
        referenced.ShouldNotContain("System.Windows.Forms");
    }

    [Fact]
    public void DomainDoesNotReferenceApplicationLayers()
    {
        var referenced = GetReferences(typeof(AppInfo).Assembly);

        referenced.ShouldNotContain("FrameGenNext.Core");
        referenced.ShouldNotContain("FrameGenNext.Infrastructure");
        referenced.ShouldNotContain("FrameGenNext.App");
    }

    private static string[] GetReferences(Assembly assembly)
    {
        return assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .OfType<string>()
            .ToArray();
    }
}
