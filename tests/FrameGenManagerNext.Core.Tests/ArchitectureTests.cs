using System.Reflection;
using FrameGenManagerNext.Domain;
using Shouldly;

namespace FrameGenManagerNext.Core.Tests;

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

        referenced.ShouldNotContain("FrameGenManagerNext.Core");
        referenced.ShouldNotContain("FrameGenManagerNext.Infrastructure");
        referenced.ShouldNotContain("FrameGenManagerNext.App");
    }

    private static string[] GetReferences(Assembly assembly)
    {
        return assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .OfType<string>()
            .ToArray();
    }
}
