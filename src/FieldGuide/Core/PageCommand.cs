using System;
using System.Globalization;

namespace FieldGuide.Core;

/// <summary>
/// What the page script tells the plugin. It sends "slmod|fieldguide|" followed by a verb and its arguments; anything
/// else parses to Unknown so it can be logged rather than acted on (the script's "diag|..." notes land there on purpose).
/// </summary>
public abstract record PageCommand
{
    public const string Prefix = "slmod|fieldguide|";

    private PageCommand()
    {
    }

    /// <summary>The page script just installed itself.</summary>
    public sealed record Ready : PageCommand;

    /// <summary>The page has no script, usually because the web view reloaded.</summary>
    public sealed record NoScript : PageCommand;

    /// <summary>The HUD button was clicked.</summary>
    public sealed record Toggle : PageCommand;

    /// <summary>The window is now open and covers this part of the screen. Repeated every second while it stays open.</summary>
    public sealed record Shown(NormalRect Area) : PageCommand;

    /// <summary>A drag or resize started: every click goes to the page until it ends.</summary>
    public sealed record Dragging : PageCommand;

    /// <summary>The window was closed.</summary>
    public sealed record Hidden : PageCommand;

    /// <summary>The player moved or resized the window; the place to open it next time.</summary>
    public sealed record Moved(WindowLayout Layout) : PageCommand;

    /// <summary>The player pressed A- or A+; the text size to open the window with next time.</summary>
    public sealed record TextSized(int Percent) : PageCommand;

    /// <summary>The download button: fetch the offline copy, or a newer one.</summary>
    public sealed record Download : PageCommand;

    /// <summary>The Field Guide app on the phone opened (true) or closed (false).</summary>
    public sealed record Phone(bool IsOpen) : PageCommand;

    public sealed record Unknown(string Text) : PageCommand;

    public static bool IsOurs(string? message) => message is not null && message.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Parses a whole message, prefix included.</summary>
    public static PageCommand Parse(string message)
    {
        if (!IsOurs(message)) return new Unknown(message);

        var parts = message.Substring(Prefix.Length).Split('|');

        return (parts[0], parts.Length) switch
        {
            ("ready", 1) => new Ready(),
            ("noscript", 1) => new NoScript(),
            ("toggle", 1) => new Toggle(),
            ("shown", 2) when TryRect(parts[1], out var area) => new Shown(area),
            ("dragging", 1) => new Dragging(),
            ("hidden", 1) => new Hidden(),
            ("moved", 2) when TryRect(parts[1], out var rect) => new Moved(WindowLayout.Clamp(rect)),
            ("textsize", 2) when int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var percent) => new TextSized(TextSize.Clamp(percent)),
            ("download", 1) => new Download(),
            ("phone", 2) when parts[1] is "1" or "0" => new Phone(parts[1] == "1"),
            _ => new Unknown(message),
        };
    }

    /// <summary>"x,y,w,h" in page fractions, as the script writes them.</summary>
    public static bool TryRect(string text, out NormalRect rect)
    {
        rect = default;

        var parts = text.Split(',');

        if (parts.Length != 4) return false;

        var values = new double[4];

        for (var i = 0; i < 4; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) || double.IsNaN(values[i]) || double.IsInfinity(values[i])) return false;
        }

        rect = new NormalRect(values[0], values[1], values[2], values[3]);

        return true;
    }
}
