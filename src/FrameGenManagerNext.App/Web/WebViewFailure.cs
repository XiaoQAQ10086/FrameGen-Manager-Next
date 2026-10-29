using Microsoft.Web.WebView2.Core;

namespace FrameGenManagerNext.App.Web;

public sealed record WebViewFailure(
    CoreWebView2ProcessFailedKind Kind,
    CoreWebView2ProcessFailedReason Reason,
    int ExitCode,
    string? ProcessDescription,
    string? FailureSourceModulePath)
{
    public bool MentionsRivaTuner =>
        ContainsRivaTuner(FailureSourceModulePath) ||
        ContainsRivaTuner(ProcessDescription);

    public string Diagnostic =>
        $"kind={Kind}; reason={Reason}; exit={ExitCode}; " +
        $"module={FailureSourceModulePath ?? "-"}; process={ProcessDescription ?? "-"}";

    private static bool ContainsRivaTuner(string? value)
    {
        return value is not null &&
               (value.Contains("RTSS", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("RivaTuner", StringComparison.OrdinalIgnoreCase));
    }
}
