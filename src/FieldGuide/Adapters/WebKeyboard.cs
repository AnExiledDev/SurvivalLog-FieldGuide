using GameCore.HotUpdate.ReduxUI;
using GameCore.Scripts;

namespace FieldGuide.Adapters;

/// <summary>
/// Sends the keyboard to the web UI while the pointer is over the window or the phone app is open, so the site's search
/// box can be typed in, and gives it back as it was found.
/// The game keeps the system's input method (for typing Chinese) off while playing,
/// and turns it on only while one of its own text boxes has focus (Reducer_Web_ToolTable.RA_SearchFocus), so a page
/// field turns it on too, and off again when it lets go.
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
        ImeGuard.SetEnabled(true);
    }

    public void Release()
    {
        if (_wasEnabled is not { } wasEnabled) return;

        _wasEnabled = null;
        GameSingleton.Existing<ReduxUISystem>()?.GetWebUILayer()?.SetKeyboardEnabled(wasEnabled);
        ImeGuard.SetEnabled(false);
    }
}
