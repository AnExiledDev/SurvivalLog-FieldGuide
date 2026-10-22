using BepInEx.Configuration;
using FieldGuide.Core;
using UnityEngine;

namespace FieldGuide.Adapters;

/// <summary>The mod's config entries, read where they are used so a change made in game applies at once.</summary>
internal sealed class GuideSettings
{
    private readonly ConfigEntry<KeyCode> _hotkey;
    private readonly ConfigEntry<SiteSource> _source;
    private readonly ConfigEntry<string> _window;
    private readonly ConfigEntry<int> _textSize;

    public GuideSettings(ConfigFile config)
    {
        _hotkey = config.Bind("Controls", "Hotkey", KeyCode.Slash,
            "Opens the Field Guide on whatever the pointer is over (an item in the backpack, on a trade shelf, or a place on the map), or opens and closes the window when it is over nothing. Ignored while the pointer is over the window. Set to None to use only the HUD button.\n" +
            "打开指针所指之物的野外指南页面（背包或交易货架上的物品、地图上的地点）；未指向任何物品时打开或关闭窗口。指针在窗口上时不生效。设为 None 则只用 HUD 按钮。");
        _source = config.Bind("Site", "Source", SiteSource.Auto,
            "Where pages come from. Auto uses the offline copy once it is downloaded and the website before that. Online always uses the website. Offline only uses the downloaded copy.\n" +
            "页面来源。Auto 在下载离线版后使用离线版，之前使用网站。Online 始终使用网站。Offline 只使用已下载的离线版。");
        _window = config.Bind("Window", "Layout", WindowLayout.Default.Format(),
            "Where the window sits and its size, as fractions of the screen: left, top, width, height. Saved when you move or resize the window.\n" +
            "窗口的位置和大小，以屏幕比例表示：左、上、宽、高。移动或调整窗口大小时自动保存。");
        _textSize = config.Bind("Window", "TextSize", Core.TextSize.Default,
            new ConfigDescription(
                "How big the site's text is in the window, in percent. The A- and A+ buttons on the title bar change it and save it here.\n" +
                "窗口中网站文字的大小（百分比）。标题栏的 A- 和 A+ 按钮会调整并保存此值。",
                new AcceptableValueRange<int>(Core.TextSize.Min, Core.TextSize.Max)));
    }

    public KeyCode Hotkey => _hotkey.Value;

    /// <summary>The key as the window's tip names it.</summary>
    public string HotkeyName => _hotkey.Value == KeyCode.Slash ? "/" : _hotkey.Value.ToString();

    public SiteSource Source => _source.Value;

    public WindowLayout Layout => WindowLayout.Parse(_window.Value);

    public int TextSize => Core.TextSize.Clamp(_textSize.Value);

    public void SaveTextSize(int percent) => _textSize.Value = Core.TextSize.Clamp(percent);

    public void SaveLayout(WindowLayout layout) => _window.Value = layout.Format();
}
