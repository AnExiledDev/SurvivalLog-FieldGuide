namespace FieldGuide.Core;

/// <summary>A box on the page as fractions of the page's size, top-left origin, the way the game's pages report theirs.</summary>
public readonly record struct NormalRect(double X, double Y, double W, double H)
{
    public static readonly NormalRect FullScreen = new(0, 0, 1, 1);

    /// <summary>Left and top edges count, right and bottom do not, as in the game's own check.</summary>
    public bool Contains(double x, double y) => x >= X && x < X + W && y >= Y && y < Y + H;

    /// <summary>
    /// A point in the web view's rect transform, as Unity gives it, mapped to page fractions with the game's own
    /// formula (WebUIVm.IsPointInInteractiveArea): Unity's y grows upward, the page's grows downward.
    /// </summary>
    public bool ContainsLocalPoint(double localX, double localY, double width, double height, double pivotX, double pivotY)
    {
        if (width <= 0 || height <= 0) return false;

        return Contains(localX / width + pivotX, 1 - (localY / height + pivotY));
    }
}

/// <summary>
/// Where the window is, trusted only while the page keeps saying so. A page that reloaded or lost its script stops
/// reporting, and an old box must not keep swallowing clicks meant for the game. The page repeats it every second.
/// </summary>
public sealed class ClickArea
{
    public const double StaleSeconds = 3;

    private NormalRect? _area;
    private double _heardAt;

    public void Heard(NormalRect area, double now)
    {
        _area = area;
        _heardAt = now;
    }

    public void Forget() => _area = null;

    public NormalRect? At(double now) => _area is { } area && now - _heardAt <= StaleSeconds ? area : null;
}
