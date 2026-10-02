using STS2MobileIos.Lan;
using STS2MobileIos.Mods;
using System.Text;
using System.Text.Json;

// Optional local inspection reads manifests/PCK directories without loading game or mod code.
if (args.Length > 0)
{
    if (args[0] == "--inspect-pack")
    {
        foreach (string path in args.Skip(1))
        {
            ResourceModPolicy.CheckPack(path);
            Console.WriteLine(Path.GetFileName(path) + ": resource pack policy passed (runtime unverified)");
        }
        return;
    }
    if (args[0] != "--inspect") throw new ArgumentException("Use --inspect <mod folder>...");
    foreach (string directory in args.Skip(1))
    {
        var mod = ResourceModPolicy.Inspect(directory);
        Console.WriteLine($"{mod.Id}: {(mod.Supported ? "resource candidate (runtime unverified)" : mod.Problem)}");
    }
    return;
}

static void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
}
static void Reject(Action action)
{
    try { action(); }
    catch (Exception error) when (error is ArgumentException or IOException or InvalidDataException) { return; }
    throw new Exception("Expected invalid input to be rejected.");
}
static void Pack(string path, string entry = "images/card.png", uint format = 3, bool encrypted = false, bool badOffset = false)
{
    using var stream = File.Create(path);
    using var writer = new BinaryWriter(stream);
    writer.Write(0x43504447u); writer.Write(format);
    writer.Write(4u); writer.Write(5u); writer.Write(1u);
    writer.Write(encrypted ? 1u : 0u); writer.Write(0UL);
    if (format == 3) writer.Write(112UL);
    else writer.Write(new byte[64]);
    if (format == 3) writer.Write(new byte[112 - stream.Position]);
    writer.Write(1u);
    byte[] name = Encoding.UTF8.GetBytes(entry);
    writer.Write((uint)name.Length); writer.Write(name);
    writer.Write(badOffset ? ulong.MaxValue : 0UL); writer.Write(0UL);
    writer.Write(new byte[16]); writer.Write(0u);
}

Check(LanAddress.Parse(" 192.168.1.10 ", "33771") == new LanAddress("192.168.1.10", 33771), "Address normalization");
foreach (string host in new[] { "", "https://example.com", "0.0.0.0", "255.255.255.255", "224.0.0.1", "::1", "192.168.1.2:123" })
    Reject(() => LanAddress.Parse(host, "33771"));
foreach (string port in new[] { "", "0", "-1", "+1", "65536", "1.5" })
    Reject(() => LanAddress.Parse("192.168.1.2", port));

string root = Path.Combine(Path.GetTempPath(), "sts2-features-" + Guid.NewGuid());
Directory.CreateDirectory(root);
try
{
    string directory = Path.Combine(root, "Cosmetic"); Directory.CreateDirectory(directory);
    string manifest = Path.Combine(directory, "Cosmetic.json"), pack = Path.Combine(directory, "Cosmetic.pck");
    var data = new Dictionary<string, object> { ["id"] = "Cosmetic", ["name"] = "Cosmetic",
        ["version"] = "1.0.0", ["has_dll"] = false, ["has_pck"] = true, ["affects_gameplay"] = false, ["dependencies"] = Array.Empty<string>() };
    void Write() => File.WriteAllText(manifest, JsonSerializer.Serialize(data), new UTF8Encoding(true));
    Write(); Pack(pack);
    Check(ResourceModPolicy.Inspect(directory).Supported, "BOM manifest and v3 resource pack");
    Pack(pack, format: 2);
    Check(ResourceModPolicy.Inspect(directory).Supported, "v2 resource pack");
    foreach (string entry in new[] { "mod.dll", "native.gdextension", "src/Foo.cs.remap", "../escape", "res://../escape", "user://file", "/absolute", "bad\0name" })
    {
        Pack(pack, entry);
        Check(!ResourceModPolicy.Inspect(directory).Supported, "Forbidden pack entry: " + entry);
    }
    Pack(pack, encrypted: true); Check(!ResourceModPolicy.Inspect(directory).Supported, "Encrypted pack");
    Pack(pack, badOffset: true); Check(!ResourceModPolicy.Inspect(directory).Supported, "Invalid offset");
    File.WriteAllBytes(pack, new byte[3]); Check(!ResourceModPolicy.Inspect(directory).Supported, "Truncated pack");
    Pack(pack, "scenes/effect.gdc"); Check(ResourceModPolicy.Inspect(directory).Supported, "Godot script resource");
    data["has_dll"] = true; Write(); Check(!ResourceModPolicy.Inspect(directory).Supported, "Declared DLL");
    data["has_dll"] = false; data["affects_gameplay"] = true; Write(); Check(!ResourceModPolicy.Inspect(directory).Supported, "Gameplay mod");
    data["affects_gameplay"] = false; data["dependencies"] = new[] { "BaseLib" }; Write();
    Check(!ResourceModPolicy.Inspect(directory).Supported, "Dependency requiring port");
    data["dependencies"] = Array.Empty<string>(); data["id"] = "Other"; Write();
    Check(!ResourceModPolicy.Inspect(directory).Supported, "ID mismatch");
    data["id"] = "Cosmetic"; Write();
    File.WriteAllBytes(Path.Combine(directory, "hidden.DLL"), new byte[1]);
    Check(!ResourceModPolicy.Inspect(directory).Supported, "Undeclared DLL");
    File.Delete(Path.Combine(directory, "hidden.DLL"));
    File.Delete(manifest); Check(!ResourceModPolicy.Inspect(directory).Supported, "Missing manifest");
    Console.WriteLine("LAN address and resource-mod policy checks passed (offline; no game code).");
}
finally { Directory.Delete(root, recursive: true); }
