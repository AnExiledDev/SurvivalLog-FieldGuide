using System;
using FieldGuide.Core;
using UnityEngine;

namespace FieldGuide.Adapters;

/// <summary>
/// The frame loop: hears the page, keeps the window's click area and the keyboard in step with it, watches the hotkey,
/// and sends the page a fresh view when the language, the window's place or the offline copy changes.
/// </summary>
internal sealed class GuideSession
{
    private const float ViewCheckSeconds = 0.25f;
    private const string DiagPrefix = PageCommand.Prefix + "diag|";

    private readonly WebPage _page;
    private readonly OfflineCopy _offline;
    private readonly LocalServer _server;
    private readonly GuideSettings _settings;
    private readonly Action<string> _log;
    private readonly ClickArea _area = new();
    private readonly WebKeyboard _keyboard = new();
    private string? _sentView;
    private float _nextViewCheckAt;
    private float _dragHeardAt = float.NegativeInfinity;
    private bool _isPhoneOpen;
    private bool _isHoldingKeyboard;
    private readonly Func<bool> _isHotkeyDown;
    private static readonly Func<bool> IsTyping = OptionalLive.IsTyping;

    public GuideSession(WebPage page, OfflineCopy offline, LocalServer server, GuideSettings settings, Action<string> log)
    {
        _page = page;
        _offline = offline;
        _server = server;
        _settings = settings;
        _log = log;
        _isHotkeyDown = () => Input.GetKeyDown(_settings.Hotkey);
    }

    public void Update()
    {
        var now = Time.realtimeSinceStartup;
        var isDragging = now - _dragHeardAt <= ClickArea.StaleSeconds;

        _page.KeepInstalled(now);

        while (PageMessages.TryTake(out var command)) Handle(command, now);

        InputGate.SetDragging(isDragging);
        InputGate.SetArea(_area.At(now));
        HoldKeyboard(InputGate.IsPointerOver || _isPhoneOpen || isDragging);

        if (!_isHoldingKeyboard && Hotkey.Fires(_settings.Hotkey != KeyCode.None, _isHotkeyDown, IsTyping)) _page.Lookup(GameLanguage.IsChinese());

        SendViewIfChanged(now);
    }

    private void Handle(PageCommand command, float now)
    {
        switch (command)
        {
            case PageCommand.Ready:
                _sentView = null;
                _isPhoneOpen = false;
                _log("page script ready");
                break;
            case PageCommand.NoScript:
                _isPhoneOpen = false;
                _page.Install();
                break;
            case PageCommand.Toggle:
                _page.Toggle(GameLanguage.IsChinese());
                break;
            case PageCommand.Shown shown:
                _area.Heard(shown.Area, now);
                _dragHeardAt = float.NegativeInfinity;
                _offline.CheckSite();
                break;
            case PageCommand.Dragging:
                _dragHeardAt = now;
                break;
            case PageCommand.Hidden:
                _area.Forget();
                _dragHeardAt = float.NegativeInfinity;
                break;
            case PageCommand.Moved moved:
                _settings.SaveLayout(moved.Layout);
                break;
            case PageCommand.TextSized sized:
                _settings.SaveTextSize(sized.Percent);
                break;
            case PageCommand.Download:
                _offline.Download();
                break;
            case PageCommand.Phone phone:
                _isPhoneOpen = phone.IsOpen;

                if (phone.IsOpen) _offline.CheckSite();

                break;
            case PageCommand.Unknown unknown when unknown.Text.StartsWith(DiagPrefix, StringComparison.Ordinal):
                _log("page: " + unknown.Text.Substring(DiagPrefix.Length));
                break;
            case PageCommand.Unknown unknown:
                _log("unknown page message: " + unknown.Text);
                break;
        }
    }

    /// <summary>The keyboard goes to the page, and the game's hotkeys stop, while the player could be typing in it.</summary>
    private void HoldKeyboard(bool wantsKeyboard)
    {
        if (wantsKeyboard == _isHoldingKeyboard) return;

        _isHoldingKeyboard = wantsKeyboard;

        if (wantsKeyboard) _keyboard.Claim();
        else _keyboard.Release();

        InputGate.BlockHotkeys(wantsKeyboard);
    }

    private void SendViewIfChanged(float now)
    {
        if (now < _nextViewCheckAt) return;

        _nextViewCheckAt = now + ViewCheckSeconds;

        var isChinese = GameLanguage.IsChinese();
        var view = GuideView.Json(SiteBase(), isChinese, _settings.Layout, _settings.TextSize, _offline.State, _settings.HotkeyName);

        if (view == _sentView) return;
        if (_page.Setup(view, isChinese)) _sentView = view;
    }

    private string? SiteBase()
    {
        var source = _settings.Source;
        var hasCopy = _offline.State.HasCopy;

        if (hasCopy && source != SiteSource.Online) _server.Start();

        return Offline.BaseFor(source, hasCopy ? _server.Base : null, GuideView.OnlineBase);
    }
}
