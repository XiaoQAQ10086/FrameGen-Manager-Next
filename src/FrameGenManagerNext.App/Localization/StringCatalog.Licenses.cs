namespace FrameGenManagerNext.App.Localization;

internal static partial class StringCatalog
{
    // Called from the single static constructor in StringCatalog.Anr.cs: a class can
    // only have one static constructor, so extra files add strings through a method.
    private static void AddLicenseEntries()
    {
        Add("About.Licenses", "第三方许可", "Third-party licenses");
        Add(
            "About.LicensesHint",
            "本程序分发的每个第三方组件、来源与许可全文都在程序目录的 licenses 文件夹里。",
            "Every third-party component this app ships, its origin and the full license text, is in the licenses folder next to the executable.");
        Add("About.OpenLicenses", "打开许可目录", "Open licenses folder");
    }
}
