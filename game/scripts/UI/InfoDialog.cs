using System;
using DatacenterArchitect.Core;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// Info screen: current version and channel, update check, install update,
/// and the switch between the stable and the test channel.
/// </summary>
public partial class InfoDialog : Modal
{
    readonly Label _status;
    readonly Button _checkBtn, _updateBtn, _notesBtn;
    readonly ProgressBar _progress;
    readonly Button _stableBtn, _testBtn;
    readonly Action _beforeInstall;
    ReleaseInfo _offer;

    public InfoDialog(Action beforeInstall) : base("info_title", 560)
    {
        _beforeInstall = beforeInstall;
        var logo = new HBoxContainer();
        logo.AddThemeConstantOverride("separation", 14);
        logo.AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://icon.svg"), CustomMinimumSize = new Vector2(56, 56), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        var names = new VBoxContainer();
        var name = new Label { Text = "Datacenter Architect", Uppercase = true };
        name.AddThemeFontOverride("font", Ui.DisplayBold); name.AddThemeFontSizeOverride("font_size", 26);
        names.AddChild(name);
        names.AddChild(Ui.Text(Loc.T("info_version", GameVersion.Text, Loc.T("channel_" + GameVersion.BuildChannel)), 15, Brand.InkDim));
        logo.AddChild(names);
        Body.AddChild(logo);
        Body.AddChild(new HSeparator());

        Body.AddChild(Ui.Heading("info_channel"));
        var ch = new HBoxContainer();
        ch.AddThemeConstantOverride("separation", 8);
        _stableBtn = ChannelButton("channel_stable", "stable");
        _testBtn = ChannelButton("channel_test", "test");
        ch.AddChild(_stableBtn); ch.AddChild(_testBtn);
        Body.AddChild(ch);
        Body.AddChild(Ui.Text("info_channel_help", 13, Brand.InkDim));
        Body.AddChild(new HSeparator());

        Body.AddChild(Ui.Heading("info_updates"));
        _status = Ui.Text("info_not_checked", 15);
        Body.AddChild(_status);
        _progress = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0.001, Visible = false, CustomMinimumSize = new Vector2(0, 8), ShowPercentage = false };
        Body.AddChild(_progress);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        _checkBtn = Ui.Secondary("info_check", () => _ = Check());
        _updateBtn = Ui.Primary("info_update_now", () => _ = Install());
        _updateBtn.Visible = false;
        _notesBtn = Ui.Secondary("info_release_notes", () => { if (_offer != null) OS.ShellOpen(_offer.PageUrl); });
        _notesBtn.Visible = false;
        _checkBtn.SizeFlagsHorizontal = _updateBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(_checkBtn); row.AddChild(_notesBtn); row.AddChild(_updateBtn);
        Body.AddChild(row);
        var auto = new CheckBox { Text = "info_auto_check", ButtonPressed = AppSettings.Instance.CheckUpdatesOnStart };
        auto.Toggled += on => { AppSettings.Instance.CheckUpdatesOnStart = on; AppSettings.Instance.Save(); };
        Body.AddChild(auto);
        Body.AddChild(new HSeparator());
        Body.AddChild(Ui.Text("info_credits", 12, Brand.InkDim));
        RefreshChannel();
    }

    Button ChannelButton(string key, string channel)
    {
        var b = Ui.Secondary(key, () =>
        {
            AppSettings.Instance.UpdateChannel = channel; AppSettings.Instance.Save();
            RefreshChannel();
            _ = Check();
        });
        b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        b.ToggleMode = true;
        return b;
    }

    void RefreshChannel()
    {
        var test = AppSettings.Instance.UpdateChannel == "test";
        Mark(_stableBtn, !test); Mark(_testBtn, test);
    }

    static void Mark(Button b, bool on)
    {
        b.SetPressedNoSignal(on);
        b.AddThemeStyleboxOverride("normal", Ui.Box(on ? new Color(Brand.Amber, 0.16f) : new Color(1, 1, 1, 0.05f), 8, on ? Brand.Amber : Brand.Line, 1, 10));
        b.AddThemeStyleboxOverride("pressed", Ui.Box(new Color(Brand.Amber, 0.16f), 8, Brand.Amber, 1, 10));
        b.AddThemeColorOverride("font_color", on ? Brand.Amber : Brand.Ink);
        b.AddThemeColorOverride("font_pressed_color", Brand.Amber);
    }

    public async System.Threading.Tasks.Task Check()
    {
        _checkBtn.Disabled = true; _updateBtn.Visible = false; _notesBtn.Visible = false; _offer = null;
        _status.Text = Loc.T("info_checking");
        var r = await UpdateService.CheckAsync(AppSettings.Instance.UpdateChannel);
        if (!IsInstanceValid(this)) return;
        _checkBtn.Disabled = false;
        switch (r.Verdict)
        {
            case UpdateVerdict.UpToDate: _status.Text = Loc.T("info_up_to_date"); break;
            case UpdateVerdict.NoReleases: _status.Text = Loc.T("info_no_releases"); break;
            case UpdateVerdict.Error: _status.Text = Loc.T("info_error", r.Error); break;
            case UpdateVerdict.NewerAvailable:
            case UpdateVerdict.ChannelSwitch:
                _offer = r.Release;
                _status.Text = Loc.T(r.Verdict == UpdateVerdict.NewerAvailable ? "info_available" : "info_switch", r.Release.Version.ToString());
                _updateBtn.Visible = true; _notesBtn.Visible = true;
                break;
        }
    }

    async System.Threading.Tasks.Task Install()
    {
        if (_offer == null) return;
        _updateBtn.Disabled = _checkBtn.Disabled = true;
        _progress.Visible = true;
        _status.Text = Loc.T("info_downloading", _offer.Version.ToString());
        try
        {
            var path = await UpdateService.DownloadAsync(_offer, p => Callable.From(() => { if (IsInstanceValid(_progress)) _progress.Value = p; }).CallDeferred());
            _beforeInstall?.Invoke();
            _status.Text = Loc.T("info_installing");
            if (UpdateService.RunInstaller(path)) GetTree().Quit();
            else { _status.Text = Loc.T("info_manual", path); _updateBtn.Disabled = _checkBtn.Disabled = false; }
        }
        catch (Exception e)
        {
            _status.Text = Loc.T("info_error", e.Message);
            _updateBtn.Disabled = _checkBtn.Disabled = false; _progress.Visible = false;
        }
    }
}
