using FieldGuide.Core;
using Xunit;

namespace FieldGuide.Tests;

/// <summary>The look-up key: kept on / since no button looks up what the pointer is over. Exiled's Toolkit tests that it stays silent while the player types.</summary>
public class HotkeyTests
{
    [Fact]
    public void The_look_up_key_keeps_slash_by_default()
    {
        Assert.Equal("Slash", Hotkey.DefaultKey);
    }

    [Theory]
    [InlineData("Slash", "/")]
    [InlineData("None", "")]
    [InlineData("F1", "F1")]
    public void The_tip_names_the_key_and_leaves_an_unbound_one_out(string keyName, string shown)
    {
        Assert.Equal(shown, Hotkey.Name(keyName));
    }
}
