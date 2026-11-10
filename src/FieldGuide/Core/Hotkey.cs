namespace FieldGuide.Core;

/// <summary>
/// The look-up key. It keeps its default: the HUD button only opens and closes the window, and the key is the only way
/// to open the page for what the pointer is over. Whether a press fires is Exiled's Toolkit's Hotkey.Fires, so a bound
/// key does nothing while the player types in a text box.
/// </summary>
public static class Hotkey
{
    /// <summary>The Unity key name the Hotkey setting starts with.</summary>
    public const string DefaultKey = "Slash";

    /// <summary>The key as the HUD button's tip names it: "/" for Slash, and nothing at all when the key is unbound.</summary>
    public static string Name(string keyName) => keyName switch
    {
        "None" => "",
        "Slash" => "/",
        _ => keyName,
    };
}
