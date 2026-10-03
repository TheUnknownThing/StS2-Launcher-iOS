using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using STS2MobileIos.Steam;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

if (args.Contains("--login-probe"))
{
    using var live = new SteamCloudClient();
    var qr = await live.BeginLogin(CancellationToken.None);
    Check(new Uri(qr.Url).Host == "s.team", "Unexpected QR host");
    Console.WriteLine("Steam QR challenge received without credentials.");
    return;
}
if (args.Contains("--cm-probe"))
{
    using var http = new HttpClient();
    using var cm = new SteamCmConnection();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    await cm.Probe(http, timeout.Token);
    Console.WriteLine("Steam client protocol returned an anonymous QR challenge over port 443.");
    return;
}

foreach (string unsafePath in new[] { "../progress.save", "/absolute", "a/../b", "a\\b", "user://save", "a//b", "a\nb" })
    Check(!CloudSavePolicy.SafePath(unsafePath), "Unsafe cloud path accepted");
Check(CloudSavePolicy.Classification("modded/profile1/saves/progress.save").StartsWith("Modded"), "Modded profile misclassified");
Check(CloudSavePolicy.Classification("modded/profile1/saves/progress.save") == "Modded candidate", "Modded save labeled archive-only");
Check(CloudSavePolicy.Classification("profile1/saves/progress.save") == "Vanilla candidate", "Vanilla profile misclassified");

var data = Encoding.UTF8.GetBytes("{\"schema_version\":7}");
using (var output = new MemoryStream())
{
    using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
    using (var entry = zip.CreateEntry("save").Open()) entry.Write(data);
    Check(CloudSavePolicy.Decode(output.ToArray()).SequenceEqual(data), "ZIP decoding changed save bytes");
}
foreach (string host in new[] { "localhost", "127.0.0.1", "device.local", "host.example:80", "user@host.example", "host.example/escape" })
{
    try { SteamCloudClient.DownloadUri(host, "/save"); throw new Exception("Invalid download host accepted"); }
    catch (InvalidOperationException) { }
}
Check(SteamCloudClient.DownloadUri("steamcloud-eu-ams.storage.googleapis.com", "/signed-save?token=example").Scheme == "https", "Steam storage provider rejected");
Check(SteamCloudClient.DownloadUri("steamcloud-eu-ams.storage.googleapis.com", "/save?signature=a%2Fb%2Bc%3D&expires=123").Query
    == "?signature=a%2Fb%2Bc%3D&expires=123", "Signed download query changed");
foreach (string path in new[] { "https://other.example/save", "//other.example/save", "/save#fragment", "/save\\escape", "/save\n" })
{
    try { SteamCloudClient.DownloadUri("storage.example", path); throw new Exception("Invalid download path accepted"); }
    catch (InvalidOperationException) { }
}

string checksum = Convert.ToHexString(SHA1.HashData(data));
var responses = new Queue<HttpResponseMessage>();
HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent("{\"response\":" + json + "}") };
responses.Enqueue(Json("{\"files\":[{\"filename\":\"profile1/saves/progress.save\",\"file_size\":20,\"timestamp\":123}],\"total_files\":1}"));
responses.Enqueue(Json(new JsonObject {
    ["appid"] = SteamCloudClient.AppId, ["time_stamp"] = 124,
    ["url_host"] = "test.steamusercontent.com", ["url_path"] = "/save",
    ["sha_file"] = Convert.ToBase64String(SHA1.HashData(data)),
}.ToJsonString()));
using var client = new SteamCloudClient(new FakeHandler(responses));
client.CloudTransport = async (_, _, _) => {
    using var response = responses.Dequeue();
    if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Steam request failed.");
    return JsonNode.Parse(await response.Content.ReadAsStringAsync())!["response"]!;
};
await client.Resume(new("test", "not-a-real-token", "76561190000000000"), CancellationToken.None);
var list = await client.ListFiles(CancellationToken.None);
Check(list.Count == 1 && list[0].Timestamp == 123, "Cloud inventory failed");
try { await client.Download(list[0], CancellationToken.None); throw new Exception("Changed remote save downloaded"); }
catch (InvalidOperationException error) { Check(error.Message.Contains("changed"), "Wrong conflict error"); }
Check(responses.Count == 0, "Unexpected requests");

responses.Enqueue(Json(new JsonObject {
    ["appid"] = SteamCloudClient.AppId, ["time_stamp"] = 123,
    ["url_host"] = "test.steamusercontent.com", ["url_path"] = "/save",
    ["sha_file"] = Convert.ToBase64String(SHA1.HashData(data)),
}.ToJsonString()));
responses.Enqueue(new(HttpStatusCode.OK) { Content = new ByteArrayContent(data) });
var downloaded = await client.Download(new("profile1/saves/progress.save", data.Length, 123, checksum), CancellationToken.None);
Check(downloaded.SequenceEqual(data), "Download changed bytes");

responses.Enqueue(Json(new JsonObject {
    ["appid"] = SteamCloudClient.AppId, ["time_stamp"] = 123,
    ["url_host"] = "test.steamusercontent.com", ["url_path"] = "/save",
    ["sha_file"] = Convert.ToBase64String(new byte[20]),
}.ToJsonString()));
responses.Enqueue(new(HttpStatusCode.OK) { Content = new ByteArrayContent(data) });
try { await client.Download(new("profile1/saves/progress.save", data.Length, 123, checksum), CancellationToken.None); throw new Exception("Corrupt download accepted"); }
catch (InvalidOperationException error) { Check(error.Message.Contains("checksum"), "Wrong integrity error"); }

responses.Enqueue(new(HttpStatusCode.Unauthorized) { Content = new StringContent("private-test-token") });
try { await client.ListFiles(CancellationToken.None); throw new Exception("Expired login accepted"); }
catch (InvalidOperationException error) { Check(!error.Message.Contains("private-test-token"), "Credentials leaked in error"); }

using (var output = new MemoryStream())
{
    using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
    {
        zip.CreateEntry("first");
        zip.CreateEntry("second");
    }
    try { CloudSavePolicy.Decode(output.ToArray()); throw new Exception("Multi-file archive accepted"); }
    catch (InvalidDataException) { }
}

var directory = Path.Combine(Path.GetTempPath(), "sts2-cloud-test-" + Guid.NewGuid());
try
{
    string first = CloudSavePolicy.Archive(directory, list[0], data);
    string second = CloudSavePolicy.Archive(directory, list[0], data);
    Check(first != second && File.Exists(first), "Archive replaced existing save");
}
finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
Console.WriteLine("Cloud path, archive, integrity, and conflict tests passed.");

var wire = new SteamWire().Number(1, 2868840).Text(3, "profile1/saves/progress.save").Fixed(10, ulong.MaxValue).Bytes(11, new byte[] { 1, 2, 3 });
var decoded = SteamWire.Read(wire.ToArray());
Check(decoded[0].Number == 2868840 && decoded[1].Text == "profile1/saves/progress.save" && decoded[2].Number == ulong.MaxValue, "Steam wire round trip failed");
foreach (var malformed in new[] { new byte[] { 0 }, new byte[] { 10, 255 }, new byte[] { 8, 128 }, new byte[] { 9, 0 } })
{
    try { SteamWire.Read(malformed); throw new Exception("Malformed Steam packet accepted"); }
    catch (InvalidDataException) { }
}
Console.WriteLine("Steam protocol field tests passed.");

Check(CloudProfileImport.SourceProfile("profile2/saves/progress.save") == "profile2", "Wrong source profile");
Check(CloudProfileImport.SourceProfile("modded/profile1/saves/progress.save") == "modded/profile1", "Modded source prefix lost");
Check(CloudProfileImport.SourceProfile("modded/profile3/saves/current_run.save") == "modded/profile3", "Modded run not importable");
foreach (string path in new[] { "profile1/saves/history/test.run", "profile1/../profile2/saves/progress.save", "profile4/saves/progress.save",
    "modded/profile1/saves/progress.save\n", "modded/../profile1/saves/progress.save", "modded/modded/profile1/saves/progress.save",
    "modded/profile4/saves/progress.save", "modded/profile1/saves/current_run_mp.save" })
    Check(CloudProfileImport.SourceProfile(path) == null, "Unsupported source accepted");
Check(CloudProfileImport.ModeMismatch(true, true) == null, "Modded import blocked with mods enabled");
Check(CloudProfileImport.ModeMismatch(false, false) == null, "Vanilla import blocked without mods");
Check(CloudProfileImport.ModeMismatch(true, false) != null, "Modded import allowed without mods");
Check(CloudProfileImport.ModeMismatch(false, true) != null, "Vanilla import allowed into a modded session");
string importRoot = Path.Combine(Path.GetTempPath(), "sts2-import-test-" + Guid.NewGuid());
try
{
    string account = Path.Combine(importRoot, "account"), backups = Path.Combine(importRoot, "backups");
    Directory.CreateDirectory(Path.Combine(account, "profile1", "saves"));
    File.WriteAllBytes(Path.Combine(account, "profile1", "saves", "progress.save"), data);
    var file = new CloudFile("profile1/saves/progress.save", data.Length, 123, checksum);
    var copy = new CloudProfileCopy("profile1", new[] { file }, new() { ["progress.save"] = data });
    Check(CloudProfileImport.FindUnusedSlot(account, 1) == 2, "Occupied slot selected");
    Directory.CreateDirectory(Path.Combine(account, "profile2", "saves", "history"));
    Check(CloudProfileImport.FindUnusedSlot(account, 1) == 2, "Empty pre-created profile rejected");
    Check(CloudProfileImport.FindUnusedSlot(account, 2) == 3, "Active empty profile offered as destination");
    var receipt = CloudProfileImport.Commit(account, backups, 2, copy, "test-account");
    Check(File.ReadAllBytes(Path.Combine(account, "profile2", "saves", "progress.save")).SequenceEqual(data), "Import changed bytes");
    Check(File.ReadAllBytes(Path.Combine(receipt.Snapshot, "local", "profile1", "saves", "progress.save")).SequenceEqual(data), "Backup missing original save");
    Check(File.Exists(Path.Combine(receipt.Snapshot, "incoming", "progress.save")), "Incoming snapshot missing");
    Check(File.Exists(Path.Combine(receipt.Snapshot, "manifest.json")), "Backup manifest missing");
    try { CloudProfileImport.Commit(account, backups, 2, copy, "test-account"); throw new Exception("Occupied slot overwritten"); }
    catch (InvalidOperationException) { }
    Check(CloudProfileImport.FindUnusedSlot(account, 1) == 3, "Imported slot selected again");
    string linked = Path.Combine(account, "linked");
    Directory.CreateSymbolicLink(linked, backups);
    try { CloudProfileImport.Commit(account, backups, 3, copy, "test-account"); throw new Exception("Linked backup accepted"); }
    catch (InvalidOperationException) { }
    Directory.Delete(linked);
    Check(!Directory.Exists(Path.Combine(account, "profile3")), "Failed backup published a profile");
    Check(!Directory.EnumerateDirectories(backups, ".pending-*").Any(), "Failed backup leaked a partial snapshot");

    string third = Path.Combine(account, "profile3");
    Directory.CreateDirectory(Path.Combine(third, "saves"));
    File.WriteAllText(Path.Combine(third, "saves", "progress.save.backup"), "retained backup");
    Check(!CloudProfileImport.IsEmptySlot(third), "Backup-only profile treated as empty");
    try { CloudProfileImport.FindUnusedSlot(account, 1); throw new Exception("Saved profile offered for reuse"); }
    catch (InvalidOperationException) { }
    try { CloudProfileImport.Commit(account, backups, 3, copy, "test-account"); throw new Exception("New file in target slot overwritten"); }
    catch (InvalidOperationException) { }
    Check(File.ReadAllText(Path.Combine(third, "saves", "progress.save.backup")) == "retained backup", "Target backup changed");
    string linkedSlot = Path.Combine(importRoot, "linked-slot");
    Directory.CreateSymbolicLink(linkedSlot, third);
    Check(!CloudProfileImport.IsEmptySlot(linkedSlot), "Linked slot treated as empty");
    Directory.Delete(linkedSlot);

    client.CloudTransport = (_, _, _) => Task.FromResult<JsonNode>(new JsonObject {
        ["total_files"] = 1, ["files"] = new JsonArray(new JsonObject {
            ["filename"] = file.Name, ["file_size"] = file.Size, ["timestamp"] = file.Timestamp + 1, ["file_sha"] = checksum,
        }),
    });
    try { await CloudProfileImport.Recheck(client, copy, false, CancellationToken.None); throw new Exception("Changed profile imported"); }
    catch (InvalidOperationException error) { Check(error.Message.Contains("changed"), "Wrong profile conflict error"); }

    // The same slot number belongs to separate vanilla and modded profile trees.
    var modData = new Dictionary<string, byte[]> {
        ["progress.save"] = Encoding.UTF8.GetBytes("{\"schema_version\":7,\"mod_progress\":{\"unlocks\":[\"WATCHER\"]}}"),
        ["prefs.save"] = Encoding.UTF8.GetBytes("{\"schema_version\":1,\"mod_settings\":{\"value\":3}}"),
        ["current_run.save"] = Encoding.UTF8.GetBytes("{\"schema_version\":1,\"players\":[{\"mod_data\":{\"stance\":\"WRATH\"}}]}"),
    };
    var modFiles = modData.Select(pair => new CloudFile("modded/profile3/saves/" + pair.Key,
        pair.Value.Length, 1000, Convert.ToHexString(SHA1.HashData(pair.Value))))
        .OrderBy(file => file.Name).ToList();
    var requested = new List<string>();
    client.CloudTransport = (method, request, _) => {
        if (method.Contains("EnumerateUserFiles"))
            return Task.FromResult<JsonNode>(new JsonObject {
                ["total_files"] = modFiles.Count + 1,
                ["files"] = new JsonArray(modFiles.Append(file).Select(item => (JsonNode)new JsonObject {
                    ["filename"] = item.Name, ["file_size"] = item.Size, ["timestamp"] = item.Timestamp, ["file_sha"] = item.Sha1,
                }).ToArray()),
            });
        string name = request["filename"]!.GetValue<string>();
        requested.Add(name);
        var item = modFiles.Single(item => item.Name == name);
        responses.Enqueue(new(HttpStatusCode.OK) { Content = new ByteArrayContent(modData[Path.GetFileName(name)]) });
        return Task.FromResult<JsonNode>(new JsonObject {
            ["appid"] = SteamCloudClient.AppId, ["time_stamp"] = item.Timestamp,
            ["url_host"] = "test.steamusercontent.com", ["url_path"] = "/save", ["sha_file"] = Convert.ToBase64String(Convert.FromHexString(item.Sha1)),
        });
    };
    var preparedModded = await CloudProfileImport.Prepare(client, "modded/profile3", true, CancellationToken.None);
    Check(preparedModded.IsModded && preparedModded.Files.SequenceEqual(modFiles), "Wrong cloud profile downloaded");
    Check(requested.Count == 3 && requested.All(name => name.StartsWith("modded/profile3/")), "Import mixed vanilla and modded cloud files");
    Check(CloudProfileImport.FindUnusedSlot(account, 1, true) == 2, "Vanilla saves blocked unused modded slot");
    string vanillaBefore = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(account, "profile2", "saves", "progress.save"))));
    var modReceipt = CloudProfileImport.Commit(account, backups, 2, preparedModded, "test-account");
    foreach (var pair in modData)
        Check(File.ReadAllBytes(Path.Combine(account, "modded", "profile2", "saves", pair.Key)).SequenceEqual(pair.Value), "Import changed mod-specific save data");
    Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(account, "profile2", "saves", "progress.save")))) == vanillaBefore, "Modded import overwrote vanilla save");
    Check(File.ReadAllBytes(Path.Combine(modReceipt.Snapshot, "local", "profile2", "saves", "progress.save")).SequenceEqual(data), "Modded import did not back up vanilla tree");
    var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(modReceipt.Snapshot, "manifest.json")))!;
    Check(manifest["destination_profile"]!.GetValue<string>() == "modded/profile2", "Snapshot lost destination save mode");
    try { CloudProfileImport.Commit(account, backups, 2, preparedModded, "test-account"); throw new Exception("Existing modded save overwritten"); }
    catch (InvalidOperationException) { }
    Check(CloudProfileImport.FindUnusedSlot(account, 1, true) == 3, "Occupied modded slot selected");
    var progressOnly = await CloudProfileImport.Prepare(client, "modded/profile3", false, CancellationToken.None);
    Check(!progressOnly.Data.ContainsKey("current_run.save") && progressOnly.Data.Count == 2, "Progress-only modded import included a run");
    var thirdReceipt = CloudProfileImport.Commit(account, backups, 3, progressOnly, "test-account");
    Check(File.ReadAllBytes(Path.Combine(thirdReceipt.Snapshot, "local", "modded", "profile2", "saves", "current_run.save")).SequenceEqual(modData["current_run.save"]), "Existing modded run missing from backup");
    Check(File.ReadAllText(Path.Combine(third, "saves", "progress.save.backup")) == "retained backup", "Modded slot collided with vanilla backup");
    Check(!File.Exists(Path.Combine(account, "modded", "profile3", "saves", "current_run.save")), "Progress-only import published a run");
    modFiles.RemoveAll(item => item.Name.EndsWith("/prefs.save", StringComparison.Ordinal));
    try { await CloudProfileImport.Recheck(client, preparedModded, true, CancellationToken.None); throw new Exception("Changed modded profile accepted"); }
    catch (InvalidOperationException error) { Check(error.Message.Contains("changed"), "Wrong modded profile conflict error"); }
}
finally { if (Directory.Exists(importRoot)) Directory.Delete(importRoot, true); }
Console.WriteLine("Profile import isolation, backup, and conflict tests passed.");

var browserFiles = new[] {
    new CloudFile("modded/profile1/saves/progress.save", 10, 500, ""),
    new CloudFile("profile1/saves/history/old.run", 10, 100, ""),
    new CloudFile("profile1/saves/prefs.save", 10, 500, ""),
    new CloudFile("profile.save", 10, 500, ""),
    new CloudFile("modded/profile1/saves/history/new.run", 10, 900, ""),
    new CloudFile("profile1/saves/progress.save", 10, 500, ""),
    new CloudFile("profile2/saves/current_run.save", 10, 600, ""),
    new CloudFile("profile1/saves/history/recent.run", 10, 800, ""),
    new CloudFile("profile1/saves/current_run_mp.save", 10, 500, ""),
}.Select(CloudBrowser.Describe).ToList();
var vanillaSaves = CloudBrowser.Filter(browserFiles, false, CloudFileKind.Saves);
Check(vanillaSaves.Count == 3 && vanillaSaves[0].File.Name == "profile1/saves/progress.save", "Save list did not prioritize progress");
Check(vanillaSaves.All(entry => entry.ImportProfile != null), "Archive-only file appeared as importable");
var moddedSaves = CloudBrowser.Filter(browserFiles, true, CloudFileKind.Saves);
Check(moddedSaves.Count == 1 && moddedSaves[0].ImportProfile == "modded/profile1", "Modded save not offered for import");
var history = CloudBrowser.Filter(browserFiles, false, CloudFileKind.History);
Check(history.Count == 2 && history[0].File.Name.EndsWith("recent.run") && history.All(entry => entry.ImportProfile == null), "History mixed with saves or ordered incorrectly");
Check(CloudBrowser.Filter(browserFiles, true, CloudFileKind.History).Count == 1, "Modded history mixed with vanilla");
Check(CloudBrowser.Filter(browserFiles, false, CloudFileKind.Other).Count == 2, "Account/multiplayer files offered for import");
Check(CloudBrowser.Filter(browserFiles, true, CloudFileKind.Other).Count == 0, "Empty category returned files");
Check(CloudBrowser.Describe(new("profile1/../profile2/saves/progress.save", 10, 500, "")).ImportProfile == null, "Unsafe path offered for import");
Console.WriteLine("Cloud browser grouping, import eligibility, and sorting tests passed.");

Check(ProgressImportWarnings.IsRetainedHistory(false, "CardStats.[491]", "Unknown card ID: CARD.RETIRED_CARD"), "Retained card history blocked");
Check(ProgressImportWarnings.IsRetainedHistory(false, "EncounterStats.[86]", "Unknown encounter ID: ENCOUNTER.RETIRED_ENCOUNTER"), "Retained encounter history blocked");
Check(ProgressImportWarnings.IsRetainedHistory(false, "EnemyStats.[107]", "Unknown enemy ID: MONSTER.RETIRED_MONSTER"), "Retained enemy history blocked");
Check(ProgressImportWarnings.IsRetainedHistory(false, "DiscoveredCards", "Unknown CardModel ID: CARD.RETIRED_CARD"), "Retained discovery blocked");
Check(!ProgressImportWarnings.IsRetainedHistory(true, "DiscoveredCards", "Unknown CardModel ID: CARD.RETIRED_CARD"), "Fatal warning allowed");
Check(!ProgressImportWarnings.IsRetainedHistory(false, "EncounterStats.[0].FightStats.[0]", "Unknown character ID: CHARACTER.RETIRED_CHARACTER, removing"), "Destructive nested repair allowed");
Check(!ProgressImportWarnings.IsRetainedHistory(false, "CardStats.[0]", "Negative TimesPicked (-1), clamping to 0"), "Stat repair silently allowed");
Check(!ProgressImportWarnings.IsRetainedHistory(false, "Epochs.[0]", "Unknown epoch ID: OldEpoch"), "Unreviewed unlock warning allowed");
Check(!ProgressImportWarnings.IsRetainedHistory(false, "CardStats.[0]", "Unknown card ID: CARD.RETIRED_CARD, removing"), "Changed validator behavior silently allowed");
Check(ProgressImportWarnings.Summary(5).Contains("remain in the save"), "Preview does not explain retained references");
Console.WriteLine("Historical progress compatibility tests passed.");

sealed class FakeHandler(Queue<HttpResponseMessage> responses) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        => Task.FromResult(responses.Dequeue());
}
