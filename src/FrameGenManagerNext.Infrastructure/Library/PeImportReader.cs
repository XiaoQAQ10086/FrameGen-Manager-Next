using System.Reflection.PortableExecutable;
using System.Text;

namespace FrameGenManagerNext.Infrastructure.Library;

public static class PeImportReader
{
    private const int MaxDescriptors = 4096;
    private const int MaxModuleNameLength = 512;

    public static IReadOnlyList<string> GetImportedDlls(string exePath)
    {
        try
        {
            using var stream = File.OpenRead(exePath);
            using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
            var headers = pe.PEHeaders;
            var header = headers.PEHeader;
            if (header is null)
            {
                return [];
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ReadDescriptors(stream, headers, header.ImportTableDirectory, 20, 12, names);
            ReadDescriptors(stream, headers, header.DelayImportTableDirectory, 32, 4, names);
            return names.ToList();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or BadImageFormatException or ArgumentException)
        {
            return [];
        }
    }

    private static void ReadDescriptors(
        FileStream stream,
        PEHeaders headers,
        DirectoryEntry directory,
        int descriptorSize,
        int nameFieldOffset,
        HashSet<string> names)
    {
        if (directory.Size <= 0 || directory.RelativeVirtualAddress <= 0)
        {
            return;
        }

        var offset = RvaToFileOffset(headers, directory.RelativeVirtualAddress);
        if (offset is null)
        {
            return;
        }

        var buffer = new byte[descriptorSize];
        for (var index = 0; index < MaxDescriptors; index++)
        {
            stream.Position = offset.Value + (long)index * descriptorSize;
            if (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
            {
                return;
            }

            var allZero = true;
            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] != 0)
                {
                    allZero = false;
                    break;
                }
            }

            if (allZero)
            {
                return;
            }

            var nameRva = BitConverter.ToInt32(buffer, nameFieldOffset);
            var nameOffset = RvaToFileOffset(headers, nameRva);
            if (nameOffset is null)
            {
                continue;
            }

            var name = ReadAscii(stream, nameOffset.Value);
            if (!string.IsNullOrWhiteSpace(name))
            {
                names.Add(name);
            }
        }
    }

    private static long? RvaToFileOffset(PEHeaders headers, int rva)
    {
        foreach (var section in headers.SectionHeaders)
        {
            var size = Math.Max(section.VirtualSize, section.SizeOfRawData);
            if (rva >= section.VirtualAddress && rva < section.VirtualAddress + size)
            {
                return section.PointerToRawData + (long)(rva - section.VirtualAddress);
            }
        }

        return null;
    }

    private static string? ReadAscii(Stream stream, long offset)
    {
        stream.Position = offset;
        var bytes = new byte[MaxModuleNameLength];
        var count = 0;
        while (count < bytes.Length)
        {
            var value = stream.ReadByte();
            if (value <= 0)
            {
                break;
            }

            bytes[count++] = (byte)value;
        }

        return count == 0 ? null : Encoding.ASCII.GetString(bytes, 0, count);
    }
}
