using STS2MobileIos.Lan;

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
Check(LanAddress.Parse(" 192.168.1.10 ", "33771") == new LanAddress("192.168.1.10", 33771), "Address normalization");
foreach (string host in new[] { "", "https://example.com", "0.0.0.0", "255.255.255.255", "224.0.0.1", "::1", "192.168.1.2:123" })
    Reject(() => LanAddress.Parse(host, "33771"));
foreach (string port in new[] { "", "0", "-1", "+1", "65536", "1.5" })
    Reject(() => LanAddress.Parse("192.168.1.2", port));

Console.WriteLine("LAN address checks passed (offline; no game code).");
