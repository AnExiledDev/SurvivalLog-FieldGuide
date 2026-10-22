using System.Collections.Generic;
using System.Linq;

namespace FieldGuide.Core;

/// <summary>Every word the window, the HUD button and the phone app show, in the game's two languages.</summary>
public static class Texts
{
    private static readonly Dictionary<string, (string En, string Zh)> Words = new()
    {
        ["title"] = ("Field Guide", "野外指南"),
        ["tip"] = ("The Survival Log Database. Hover an item or a map location and press {0} to look it up", "生存日志数据库。悬停在物品或地图地点上按 {0} 即可查看"),
        ["home"] = ("Home", "首页"),
        ["close"] = ("Close", "关闭"),
        ["online"] = ("Online", "在线"),
        ["offline"] = ("Offline copy", "离线版"),
        ["download"] = ("Download for offline ({0})", "下载离线版（{0}）"),
        ["downloadSize"] = ("Download for offline", "下载离线版"),
        ["downloading"] = ("Downloading {0}%", "下载中 {0}%"),
        ["update"] = ("Update offline copy ({0})", "更新离线版（{0}）"),
        ["failed"] = ("Download failed, try again", "下载失败，点击重试"),
        ["needCopy"] = ("Source is set to Offline and there is no offline copy yet. Download it here, or set Source to Auto to read the site online.", "来源设为 Offline，但还没有离线版。可在此下载，或把 Source 设为 Auto 在线浏览。"),
        ["back"] = ("Back", "返回"),
    };

    public static string Get(string key, bool isChinese) => Words.TryGetValue(key, out var pair) ? (isChinese ? pair.Zh : pair.En) : key;

    /// <summary>The words as a JavaScript object literal for the page script.</summary>
    public static string Json(bool isChinese) =>
        "{" + string.Join(",", Words.Select(pair => JsText.Literal(pair.Key) + ":" + JsText.Literal(isChinese ? pair.Value.Zh : pair.Value.En))) + "}";
}
