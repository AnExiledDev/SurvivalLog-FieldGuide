using GameCore.HotUpdate.ReduxUI;

namespace FieldGuide.Adapters;

/// <summary>
/// Sends the keyboard to the web UI while the pointer is over the window or the phone app is open, so the site's search
/// box can be typed in, and gives it back as it was found.
/// </summary>
internal sealed class WebKeyboard
{
    private bool? _wasEnabled;

    public bool IsClaimed => _wasEnabled != null;

    public void Claim()
    {
        var layer = GameSingleton.Existing<ReduxUISystem>()?.GetWebUILayer();

        if (layer == null) return;

        _wasEnabled ??= layer.IsKeyboardEnabled;
        layer.SetKeyboardEnabled(true);
    }

    public void Release()
    {
        if (_wasEnabled is not { } wasEnabled) return;

        _wasEnabled = null;
        GameSingleton.Existing<ReduxUISystem>()?.GetWebUILayer()?.SetKeyboardEnabled(wasEnabled);
    }
}
