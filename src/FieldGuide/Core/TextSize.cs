using System;

namespace FieldGuide.Core;

/// <summary>
/// How big the site reads in the window, as a percent of the site's own size. The site is drawn for a browser, which
/// is too large next to the game's HUD, so the window starts smaller and the title bar's A- and A+ step it.
/// </summary>
public static class TextSize
{
    public const int Min = 50;
    public const int Max = 150;
    public const int Default = 80;

    public static int Clamp(int percent) => Math.Clamp(percent, Min, Max);
}
