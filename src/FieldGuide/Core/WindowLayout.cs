using System;
using System.Globalization;
using System.Linq;

namespace FieldGuide.Core;

/// <summary>
/// Where the window sits and how big it is, as fractions of the screen so it lands in the same place at any resolution.
/// Saved in the config as "x,y,w,h".
/// </summary>
public readonly record struct WindowLayout(double X, double Y, double W, double H)
{
    public const double MinW = 0.15;
    public const double MinH = 0.2;

    /// <summary>A tall window down the right side, clear of the HUD's tool column on the left.</summary>
    public static readonly WindowLayout Default = new(0.6, 0.08, 0.36, 0.8);

    /// <summary>A saved value that cannot be read gives the default, so a hand-edited config never loses the window.</summary>
    public static WindowLayout Parse(string? text) =>
        text != null && PageCommand.TryRect(text.Trim(), out var rect) ? Clamp(rect) : Default;

    /// <summary>At least the minimum size, at most the screen, and wholly on screen.</summary>
    public static WindowLayout Clamp(NormalRect rect)
    {
        var w = Math.Clamp(rect.W, MinW, 1);
        var h = Math.Clamp(rect.H, MinH, 1);

        return new WindowLayout(Math.Clamp(rect.X, 0, 1 - w), Math.Clamp(rect.Y, 0, 1 - h), w, h);
    }

    public string Format() => string.Join(",", new[] { X, Y, W, H }.Select(Round));

    public string Json() => "{\"x\":" + Round(X) + ",\"y\":" + Round(Y) + ",\"w\":" + Round(W) + ",\"h\":" + Round(H) + "}";

    private static string Round(double value) => Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);
}
