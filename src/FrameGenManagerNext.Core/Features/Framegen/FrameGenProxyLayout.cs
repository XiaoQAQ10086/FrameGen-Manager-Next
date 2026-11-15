namespace FrameGenManagerNext.Core.Features.Framegen;

/// <summary>
/// Layout of the proxy DLLs shipped by the FrameGen pack. Upstream keeps five spare proxies in
/// <c>alternatives\</c> and wants exactly one of them next to the game EXE at a time, so the pack
/// lists every proxy by its root name and the spares are only ever referenced as pack sources.
/// The <c>alternatives\</c> target is still derived here so a folder a user copied by hand can be
/// recognised when an unrecorded deployment is cleaned up.
/// </summary>
public static class FrameGenProxyLayout
{
    /// <summary>Directory upstream uses for the proxies which are not currently selected.</summary>
    public const string AlternateDirectory = "alternatives";

    /// <summary>Maps a root proxy target such as <c>winmm.dll</c> to <c>alternatives/winmm.dll</c>.</summary>
    public static string AlternateTarget(string rootTarget)
    {
        var fileName = Path.GetFileName(rootTarget.Replace('\\', '/'));
        return $"{AlternateDirectory}/{fileName}";
    }
}
