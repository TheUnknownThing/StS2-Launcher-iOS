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

sealed class FakeHandler(Queue<HttpResponseMessage> responses) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        => Task.FromResult(responses.Dequeue());
}
