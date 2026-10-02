using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace STS2MobileIos.Mods;

internal sealed class ModsPanel
{
    private readonly PanelContainer _panel;
    private readonly VBoxContainer _list;
    private readonly Label _status;

    public ModsPanel(Control root)
    {
        _panel = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        root.AddChild(_panel); _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _panel.OffsetLeft = 100; _panel.OffsetRight = -100;
        _panel.OffsetTop = 80; _panel.OffsetBottom = -80;
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = new Color(0.055f, 0.065f, 0.085f, 1),
            ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 24, ContentMarginBottom = 24 });
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 16); _panel.AddChild(column);
        column.AddChild(new Label { Text = "Resource mods (experimental)" });
        column.AddChild(new Label { Text = "In Files, copy each mod to StS2/mods/<id>/<id>.json and <id>.pck.\nOnly enable mods you trust. Restart the app to apply changes.\nFirst activation copies vanilla profiles into the separate modded save area.\nC# DLL mods need individual iOS ports. Modded Cloud saves remain archive-only.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var actions = new HBoxContainer(); column.AddChild(actions);
        var refresh = new Button { Text = "Refresh", CustomMinimumSize = new Vector2(130, 48) };
        var close = new Button { Text = "Close", CustomMinimumSize = new Vector2(130, 48) };
        actions.AddChild(refresh); actions.AddChild(close);
        refresh.Pressed += Refresh; close.Pressed += () => _panel.Hide();
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart }; column.AddChild(_status);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill }; column.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; scroll.AddChild(_list);
    }

    public void Show() { Refresh(); _panel.Show(); }
    private void Refresh()
    {
        foreach (var child in _list.GetChildren()) { _list.RemoveChild(child); child.QueueFree(); }
        try
        {
            var mods = ResourceMods.Scan();
            _status.Text = mods.Count == 0 ? "No mod folders found." : "Enabled mods use the game's separate modded save area.";
            foreach (var mod in mods)
            {
                bool enabled = ResourceMods.Enabled(mod.Id);
                var loaded = ModManager.Mods.FirstOrDefault(m => m.manifest?.id == mod.Id);
                var toggle = new CheckButton { Text = mod.Name + " " + mod.Version, ButtonPressed = enabled,
                    Disabled = !mod.Supported && !enabled, CustomMinimumSize = new Vector2(0, 48) };
                _list.AddChild(toggle);
                _list.AddChild(new Label { Text = mod.Problem ?? (loaded != null ? "This launch: " + loaded.state : "Not loaded this launch."),
                    AutowrapMode = TextServer.AutowrapMode.WordSmart });
                toggle.Toggled += value => {
                    try { ResourceMods.SetEnabled(mod.Id, value); _status.Text = "Saved. Fully close and reopen StS2 to apply the change."; }
                    catch (Exception error) { toggle.SetPressedNoSignal(!value); _status.Text = error.Message; }
                };
            }
        }
        catch { _status.Text = "Could not read the mods folder."; }
    }
}
