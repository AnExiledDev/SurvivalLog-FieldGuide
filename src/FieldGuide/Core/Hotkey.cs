using System;

namespace FieldGuide.Core;

/// <summary>
/// The look-up key. It keeps its default: the HUD button only opens and closes the window, and the key is the only way
/// to open the page for what the pointer is over. A bound key does nothing while the player types in a text box.
/// </summary>
public static class Hotkey
{
    /// <summary>The Unity key name the Hotkey setting starts with.</summary>
    public const string DefaultKey = "Slash";

    /// <summary>
    /// Whether this frame's press fires the key. The press and the typing check are only asked for when they can change
    /// the answer: an unbound key asks nothing, and typing is only checked on a press.
    /// </summary>
    public static bool Fires(bool isBound, Func<bool> isPressed, Func<bool> isTyping) => isBound && isPressed() && !isTyping();

    /// <summary>The key as the HUD button's tip names it: "/" for Slash, and nothing at all when the key is unbound.</summary>
    public static string Name(string keyName) => keyName switch
    {
        "None" => "",
        "Slash" => "/",
        _ => keyName,
    };
}
