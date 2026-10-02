using System.Text;
using System.Text.Json;

namespace STS2MobileIos.Mods;

internal sealed record ResourceModInfo(string Id, string Name, string Version, string Directory, string Problem)
{
    public bool Supported => Problem == null;
}

internal static class ResourceModPolicy
{
    public static ResourceModInfo Inspect(string directory)
    {
        string id = Path.GetFileName(directory);
        string name = id, version = "";
        try
        {
            if (id.Length is 0 or > 100 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-'))
                throw new InvalidDataException("Use a folder named after the mod ID (letters, digits, _ or -).");
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Linked mod folders are not supported.");
            string manifest = Path.Combine(directory, id + ".json");
            if (new FileInfo(manifest).Length > 64 * 1024)
                throw new InvalidDataException("The mod manifest is too large.");
            using var json = JsonDocument.Parse(File.ReadAllText(manifest, Encoding.UTF8));
            var root = json.RootElement;
            if (root.GetProperty("id").GetString() != id)
                throw new InvalidDataException("The folder and manifest ID must match.");
            name = Clean(root.TryGetProperty("name", out var n) ? n.GetString() : id);
            version = Clean(root.TryGetProperty("version", out var v) ? v.GetString() : "");
            if (!root.TryGetProperty("has_dll", out var dll) || dll.ValueKind != JsonValueKind.False)
                throw new InvalidDataException("C# DLL mods require an individual iOS port.");
            if (!root.TryGetProperty("has_pck", out var pck) || pck.ValueKind != JsonValueKind.True)
                throw new InvalidDataException("No resource pack declared.");
            if (!root.TryGetProperty("affects_gameplay", out var gameplay) || gameplay.ValueKind != JsonValueKind.False)
                throw new InvalidDataException("Only resource mods declared cosmetic are supported in this first version.");
            if (root.TryGetProperty("dependencies", out var dependencies)
                && (dependencies.ValueKind != JsonValueKind.Array || dependencies.GetArrayLength() != 0))
                throw new InvalidDataException("Mods with dependencies need an individual compatibility review.");
            foreach (var file in System.IO.Directory.EnumerateFileSystemEntries(directory))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Linked mod files are not supported.");
                if (IsNativeOrManagedCode(file))
                    throw new InvalidDataException("This folder includes desktop code or a native extension.");
            }
            CheckPack(Path.Combine(directory, id + ".pck"));
            return new(id, name, version, directory, null);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException
            or InvalidOperationException or KeyNotFoundException or ArgumentException or OverflowException)
        {
            return new(id, name, version, directory, error is InvalidDataException ? error.Message : "Missing or invalid mod manifest/resource pack.");
        }
    }

    private static string Clean(string value) => new((value ?? "").Where(c => !char.IsControl(c)).Take(100).ToArray());
    private static bool IsNativeOrManagedCode(string name) => new[] {
        ".dll", ".dylib", ".so", ".gdextension", ".cs", ".cs.remap", ".framework"
    }.Any(suffix => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    public static void CheckPack(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, new UTF8Encoding(false, true));
        if (reader.ReadUInt32() != 0x43504447) throw new InvalidDataException("Invalid Godot resource pack.");
        uint format = reader.ReadUInt32();
        if (format is not 2 and not 3 || reader.ReadUInt32() != 4)
            throw new InvalidDataException("Only Godot 4 resource packs (v2/v3) are supported.");
        reader.ReadUInt32(); reader.ReadUInt32();
        uint flags = reader.ReadUInt32();
        if ((flags & ~2u) != 0) throw new InvalidDataException("Encrypted or unknown pack formats are unsupported.");
        ulong fileBase = reader.ReadUInt64();
        if (format == 3)
        {
            ulong offset = reader.ReadUInt64();
            if (offset > (ulong)stream.Length - 4) throw new InvalidDataException("Invalid pack directory.");
            stream.Position = (long)offset;
        }
        else stream.Position += 64;
        uint count = reader.ReadUInt32();
        if (count > 100_000) throw new InvalidDataException("Too many files in resource pack.");
        for (uint i = 0; i < count; i++)
        {
            uint length = reader.ReadUInt32();
            if (length is 0 or > 4096) throw new InvalidDataException("Invalid pack filename.");
            byte[] bytes = reader.ReadBytes((int)length);
            if (bytes.Length != length) throw new EndOfStreamException();
            string name = new UTF8Encoding(false, true).GetString(bytes).TrimEnd('\0');
            if (name.StartsWith("res://", StringComparison.Ordinal)) name = name[6..];
            if (name.Contains('\0') || name.Contains('\\') || name.Contains(':') || name.StartsWith('/')
                || name.Split('/').Any(p => p is "." or "..") || IsNativeOrManagedCode(name))
                throw new InvalidDataException("Pack includes desktop code, a native extension, or an invalid path.");
            ulong offset = reader.ReadUInt64(), size = reader.ReadUInt64();
            if ((flags & 2) != 0) offset = checked(offset + fileBase);
            if (offset > (ulong)stream.Length || size > (ulong)stream.Length - offset)
                throw new InvalidDataException("Truncated resource pack.");
            if (reader.ReadBytes(16).Length != 16) throw new EndOfStreamException();
            if (reader.ReadUInt32() != 0) throw new InvalidDataException("Encrypted or removed resources are unsupported.");
        }
    }
}
