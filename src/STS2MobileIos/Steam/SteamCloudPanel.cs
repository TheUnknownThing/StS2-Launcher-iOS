using Godot;
using System.Text.Json.Nodes;

namespace STS2MobileIos.Steam;

internal sealed class SteamCloudPanel
{
    private readonly SteamCloudClient _client = new();
    private readonly PanelContainer _panel;
    private readonly Label _status;
    private readonly TextureRect _qr;
    private readonly ItemList _files;
    private readonly Label _preview;
    private readonly Button _connect, _refresh, _download, _disconnect;
    private CancellationTokenSource _operation;
    private List<CloudFile> _inventory = new();
    private bool _busy;

    public SteamCloudPanel(Control root)
    {
        _panel = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        root.AddChild(_panel);
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _panel.OffsetLeft = 100;
        _panel.OffsetTop = 80;
        _panel.OffsetRight = -100;
        _panel.OffsetBottom = -80;
        var margin = new MarginContainer();
        foreach (string side in new[] { "left", "top", "right", "bottom" })
            margin.AddThemeConstantOverride("margin_" + side, 24);
        _panel.AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        margin.AddChild(column);
        column.AddChild(new Label { Text = "Steam Cloud" });
        _status = new Label { Text = "Connect to browse your Slay the Spire 2 saves.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        column.AddChild(_status);
        column.AddChild(new Label { Text = "Downloads are kept separately. Your active iPad saves and Steam Cloud are not overwritten.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 12);
        column.AddChild(actions);
        _connect = Button(actions, "Connect Steam", () => Run(Connect));
        _refresh = Button(actions, "Refresh saves", () => Run(Refresh));
        _disconnect = Button(actions, "Disconnect", Disconnect);
        Button(actions, "Close", Close);
        _qr = new TextureRect { Visible = false, CustomMinimumSize = new Vector2(280, 280),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
        column.AddChild(_qr);
        _files = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 160) };
        column.AddChild(_files);
        _files.ItemSelected += index =>
        {
            var file = _inventory[(int)index];
            _preview.Text = file.Name + "\n" + CloudSavePolicy.Classification(file.Name)
                + $" | {file.Size:N0} bytes | {DateTimeOffset.FromUnixTimeSeconds(file.Timestamp):yyyy-MM-dd HH:mm} UTC";
            UpdateButtons();
        };
        _preview = new Label { Text = "Select a file to preview it.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        column.AddChild(_preview);
        _download = Button(column, "Download a separate copy", () => Run(Download));
        UpdateButtons();
        root.TreeExiting += () => { _operation?.Cancel(); _client.Dispose(); };
    }

    private static Button Button(Node parent, string text, Action pressed)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 48) };
        parent.AddChild(button);
        button.Pressed += pressed;
        return button;
    }

    public void Show()
    {
        _panel.Show();
        if (_client.Login == null && !_busy)
            Run(async cancellation =>
            {
                string saved = NativeBridge.ReadCredential();
                if (string.IsNullOrEmpty(saved)) return;
                var value = JsonNode.Parse(saved);
                var login = new SteamLogin(value["account"]!.GetValue<string>(), value["refresh"]!.GetValue<string>(), value["steamid"]!.GetValue<string>());
                _status.Text = "Restoring Steam connection...";
                await _client.Resume(login, cancellation);
                await Refresh(cancellation);
            });
    }

    private async void Run(Func<CancellationToken, Task> operation)
    {
        if (_busy) return;
        _busy = true;
        using var source = new CancellationTokenSource();
        _operation = source;
        UpdateButtons();
        try { await operation(source.Token); }
        catch (OperationCanceledException) { _status.Text = "Connection cancelled or timed out. Try again when ready."; }
        catch (HttpRequestException) { _status.Text = "Cannot reach Steam. Check your connection and try again."; }
        catch (Exception error)
        {
            _status.Text = error is InvalidOperationException or InvalidDataException ? error.Message
                : "Could not complete this operation. No active saves were changed.";
        }
        finally
        {
            _operation = null;
            _busy = false;
            _qr.Hide();
            UpdateButtons();
        }
    }

    private async Task Connect(CancellationToken cancellation)
    {
        _status.Text = "Requesting Steam login...";
        var challenge = await _client.BeginLogin(cancellation);
        DrawQr(challenge.Url);
        _status.Text = "Scan with Steam on your phone, then approve the StS2 iOS login.";
        var login = await _client.WaitForLogin(challenge, DrawQr, cancellation);
        NativeBridge.WriteCredential(new JsonObject { ["account"] = login.AccountName,
            ["refresh"] = login.RefreshToken, ["steamid"] = login.SteamId }.ToJsonString());
        _qr.Hide();
        await Refresh(cancellation);
    }

    private void DrawQr(string url)
    {
        var uri = new Uri(url);
        if (uri.Scheme != "https" || uri.Host != "s.team")
            throw new InvalidOperationException("Steam returned an unexpected login address.");
        using var image = new Image();
        if (image.LoadPngFromBuffer(NativeBridge.QrPng(url)) != Error.Ok)
            throw new InvalidOperationException("Could not display the Steam QR code.");
        _qr.Texture = ImageTexture.CreateFromImage(image);
        _qr.Show();
    }

    private async Task Refresh(CancellationToken cancellation)
    {
        _status.Text = "Reading Steam Cloud...";
        _inventory = await _client.ListFiles(cancellation);
        _files.Clear();
        foreach (var file in _inventory)
            _files.AddItem(file.Name + "  [" + CloudSavePolicy.Classification(file.Name) + "]");
        _preview.Text = "Select a file to preview it.";
        _status.Text = $"Connected as {_client.Login.AccountName}. {_inventory.Count} cloud files.";
    }

    private async Task Download(CancellationToken cancellation)
    {
        var selected = _files.GetSelectedItems();
        if (selected.Length != 1) return;
        var file = _inventory[selected[0]];
        _status.Text = "Downloading a separate copy...";
        var bytes = await _client.Download(file, cancellation);
        cancellation.ThrowIfCancellationRequested();
        var path = CloudSavePolicy.Archive(Path.Combine(OS.GetUserDataDir(), "cloud-downloads"), file, bytes);
        _preview.Text = CloudSavePolicy.Describe(bytes);
        _status.Text = "Downloaded to Files > StS2 iOS > cloud-downloads/" + Path.GetFileName(Path.GetDirectoryName(path))
            + ". Active saves are unchanged.";
    }

    private void Disconnect()
    {
        if (_busy) return;
        try
        {
            NativeBridge.DeleteCredential();
            _client.ForgetLogin();
            _inventory.Clear();
            _files.Clear();
            _preview.Text = "Select a file to preview it.";
            _status.Text = "Steam disconnected. Saved login removed from this iPad.";
        }
        catch (InvalidOperationException error) { _status.Text = error.Message; }
        UpdateButtons();
    }

    private void Close()
    {
        _operation?.Cancel();
        _panel.Hide();
    }

    private void UpdateButtons()
    {
        bool connected = _client.Login != null;
        _connect.Disabled = _busy || connected;
        _refresh.Disabled = _busy || !connected;
        _disconnect.Disabled = _busy || !connected;
        _download.Disabled = _busy || !connected || _files.GetSelectedItems().Length != 1;
    }
}
