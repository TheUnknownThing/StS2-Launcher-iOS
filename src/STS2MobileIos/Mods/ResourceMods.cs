using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace STS2MobileIos.Mods;

public static class ResourceMods
{
    private static readonly HashSet<string> EnabledPaths = new(StringComparer.Ordinal);
    private static string Root => Path.Combine(OS.GetUserDataDir(), "mods");
    private static string OriginalRoot => Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath()), "mods");

    internal static List<ResourceModInfo> Scan() => Directory.Exists(Root)
        ? Directory.EnumerateDirectories(Root).Order(StringComparer.Ordinal).Take(100).Select(ResourceModPolicy.Inspect).ToList()
        : new();

    internal static bool Enabled(string id)
    {
        var config = new ConfigFile(); config.Load("user://resource-mods.cfg");
        return config.GetValue("enabled", id, false).AsBool();
    }

    internal static void SetEnabled(string id, bool enabled)
    {
        var config = new ConfigFile(); config.Load("user://resource-mods.cfg");
        config.SetValue("enabled", id, enabled);
        if (config.Save("user://resource-mods.cfg") != Error.Ok)
            throw new IOException("Could not save the mod selection.");
    }

    // Keep the original dependency/version checks, tracking, and modded save isolation.
    public static void InitializePrefix(ref IModManagerFileIo fileIo, ref ModSettings settings)
    {
        EnabledPaths.Clear();
        try
        {
            Directory.CreateDirectory(Root);
            foreach (var mod in Scan())
                if (mod.Supported && Enabled(mod.Id)) EnabledPaths.Add(mod.Directory);
        }
        catch (IOException) { EnabledPaths.Clear(); }
        catch (UnauthorizedAccessException) { EnabledPaths.Clear(); }
        fileIo = new ResourceFileIo(fileIo);
        settings = new ModSettings { PlayerAgreedToModLoading = true };
    }

    public static void ScanPrefix(ref string path)
    {
        if (path == OriginalRoot) path = Root;
    }

    private sealed class ResourceFileIo(IModManagerFileIo original) : IModManagerFileIo
    {
        public bool DirectoryExists(string path) => path == OriginalRoot
            || (path != OriginalRoot + "_STEAMTEST" && original.DirectoryExists(path));
        public string[] GetDirectoriesAt(string path) => path == Root
            ? EnabledPaths.Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()
            : path.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? Array.Empty<string>() : original.GetDirectoriesAt(path);
        public string[] GetFilesAt(string path) => EnabledPaths.Contains(path)
            ? new[] { Path.GetFileName(path) + ".json" } : path == Root ? Array.Empty<string>() : original.GetFilesAt(path);
        public bool FileExists(string path) => original.FileExists(path);
        public Stream OpenStream(string path, Godot.FileAccess.ModeFlags mode) => original.OpenStream(path, mode);
        public void MakeDirRecursive(string path) => original.MakeDirRecursive(path);
        public Error CopyFile(string sourcePath, string destinationPath) => original.CopyFile(sourcePath, destinationPath);
    }
}
