using PeNet;

namespace FrameGenManagerNext.Infrastructure.Library;

public static class PeImportReader
{
    public static IReadOnlyList<string> GetImportedDlls(string exePath)
    {
        try
        {
            var pe = new PeFile(exePath);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (pe.ImportedFunctions is not null)
            {
                foreach (var function in pe.ImportedFunctions)
                {
                    if (!string.IsNullOrWhiteSpace(function.DLL))
                    {
                        names.Add(function.DLL.Trim());
                    }
                }
            }

            if (pe.DelayImportedFunctions is not null)
            {
                foreach (var function in pe.DelayImportedFunctions)
                {
                    if (!string.IsNullOrWhiteSpace(function.DLL))
                    {
                        names.Add(function.DLL.Trim());
                    }
                }
            }

            return names.ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }
}
