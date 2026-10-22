namespace FieldGuide.Core;

/// <summary>What the title bar's offline button says, and whether pressing it starts a download.</summary>
public readonly record struct OfflineButton(string? Label, bool CanDownload);

/// <summary>
/// Everything the page script needs to draw the window and the phone app, as one JSON object: where pages come from
/// (null when the player chose Offline and has no copy), the site language, where the window sits, and the offline
/// button.
/// </summary>
public static class GuideView
{
    public const string OnlineBase = "https://sldb.core-directive.com";

    public static OfflineButton Button(OfflineState state, bool isChinese) => state switch
    {
        OfflineState.Missing { Bytes: > 0 } missing => new(Format(Texts.Get("download", isChinese), Offline.Megabytes(missing.Bytes)), true),
        OfflineState.Missing => new(Texts.Get("downloadSize", isChinese), true),
        OfflineState.Downloading downloading => new(Format(Texts.Get("downloading", isChinese), downloading.Percent.ToString(System.Globalization.CultureInfo.InvariantCulture)), false),
        OfflineState.Ready { NewerBytes: > 0 } ready => new(Format(Texts.Get("update", isChinese), Offline.Megabytes(ready.NewerBytes)), true),
        OfflineState.Ready => new(null, false),
        OfflineState.Failed => new(Texts.Get("failed", isChinese), true),
        _ => new(null, false),
    };

    public static string Json(string? siteBase, bool isChinese, WindowLayout layout, OfflineState state, string hotkey)
    {
        var button = Button(state, isChinese);
        var source = siteBase == null ? null : Texts.Get(siteBase == OnlineBase ? "online" : "offline", isChinese);

        return "{\"base\":" + Literal(siteBase) +
               ",\"lang\":" + JsText.Literal(isChinese ? "zh" : "en") +
               ",\"layout\":" + layout.Json() +
               ",\"source\":" + Literal(source) +
               ",\"action\":" + Literal(button.Label) +
               ",\"canDownload\":" + (button.CanDownload ? "true" : "false") +
               ",\"hotkey\":" + JsText.Literal(hotkey) + "}";
    }

    private static string Literal(string? text) => text == null ? "null" : JsText.Literal(text);

    private static string Format(string template, string value) => template.Replace("{0}", value);
}
