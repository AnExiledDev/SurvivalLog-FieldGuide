using FieldGuide.Core;
using Xunit;

namespace FieldGuide.Tests;

/// <summary>page.js fed the real core's view the way the plugin feeds it: the window, the HUD button, the lookups and the phone app.</summary>
public class PageScriptTests
{
    private const string Online = GuideView.OnlineBase;
    private const string Local = "http://127.0.0.1:5123";

    // Live-site pages carry the Field Guide's utm tags so the site's analytics can count its visits.
    private static string InWindow(string path) => Online + path + (path.Contains('?') ? "&" : "?") + "utm_source=field-guide&utm_medium=window";

    private static string OnPhone(string path) => Online + path + "?utm_source=field-guide&utm_medium=phone";

    private static string ViewOf(string? siteBase, OfflineState? state = null, bool isChinese = false, int textSize = TextSize.Default) =>
        GuideView.Json(siteBase, isChinese, WindowLayout.Default, textSize, state ?? new OfflineState.Ready("v1", 0), "/");

    private static PageScript Ready(string? siteBase = Online, OfflineState? state = null)
    {
        var page = new PageScript();

        page.Setup(ViewOf(siteBase, state));

        return page;
    }

    private static PageScript Opened()
    {
        var page = Ready();

        page.Toggle();
        page.Forget();

        return page;
    }

    // ===== The window =====

    [Fact]
    public void The_hud_button_asks_the_plugin_to_toggle_the_window()
    {
        var page = Ready();

        page.Run("openHud()");
        page.Poll();
        page.Click("hudButton().querySelector('svg')");

        Assert.Contains(new PageCommand.Toggle(), page.Sent);
    }

    [Fact]
    public void The_hud_button_is_placed_once_however_often_the_page_is_checked()
    {
        var page = Ready();

        page.Run("openHud()");
        page.Poll();
        page.Poll();

        Assert.Equal(1, page.Count("pageOf(/CoreUI1/).querySelectorAll('.fg-hud-btn')"));
    }

    [Fact]
    public void Opening_shows_the_site_home_and_tells_the_plugin_where_the_window_is()
    {
        var page = Ready();

        page.Toggle();

        Assert.True(page.Is("guideWindow().classList.contains('is-open')"));
        Assert.Equal(InWindow("/en/"), page.Text("guideFrame().src"));
        Assert.Contains(new PageCommand.Shown(new NormalRect(0.6, 0.08, 0.36, 0.8)), page.Sent);
    }

    [Fact]
    public void The_close_button_hides_the_window_and_releases_its_clicks()
    {
        var page = Opened();

        page.Click("windowButton('close')");

        Assert.False(page.Is("guideWindow().classList.contains('is-open')"));
        Assert.Contains(new PageCommand.Hidden(), page.Sent);
    }

    [Fact]
    public void The_home_button_goes_back_to_the_site_home()
    {
        var page = Opened();

        page.Run("guideFrame().src = 'elsewhere'");
        page.Click("windowButton('home')");

        Assert.Equal(InWindow("/en/"), page.Text("guideFrame().src"));
    }

    [Fact]
    public void Dragging_the_title_bar_moves_the_window_and_saves_where_it_landed()
    {
        var page = Opened();

        // 100 px left and 50 px up on a 1000x500 page; the top edge stops at the top of the screen.
        page.Run("dragFrom(guideWindow().querySelector('.fg-title'), 500, 100, 400, 50)");

        Assert.Equal(new PageCommand.Dragging(), page.Sent[0]);
        Assert.Contains(new PageCommand.Moved(new WindowLayout(0.5, 0, 0.36, 0.8)), page.Sent);
        Assert.Contains(new PageCommand.Shown(new NormalRect(0.5, 0, 0.36, 0.8)), page.Sent);
    }

    [Fact]
    public void A_window_dragged_past_the_edge_stays_wholly_on_screen()
    {
        var page = Opened();

        page.Run("dragFrom(guideWindow().querySelector('.fg-bar'), 500, 100, 1900, 900)");

        // Shown is read as the page sent it, so this is the page's own clamp, not the plugin's.
        var shown = page.Sent.OfType<PageCommand.Shown>().Single().Area;

        Assert.Equal(new NormalRect(0.64, 0.2, 0.36, 0.8), shown);
    }

    [Fact]
    public void Resizing_from_the_left_edge_stops_at_the_minimum_width()
    {
        var page = Opened();

        page.Run("dragFrom(guideWindow().querySelector('.fg-w'), 600, 100, 1500, 100)");

        Assert.Contains(new PageCommand.Moved(new WindowLayout(0.81, 0.08, 0.15, 0.8)), page.Sent);
    }

    [Fact]
    public void The_site_is_drawn_at_the_saved_text_size_and_still_fills_the_window()
    {
        var page = new PageScript();

        page.Setup(ViewOf(Online, textSize: 50));
        page.Toggle();

        Assert.Equal("scale(0.5)", page.Text("guideFrame().style.transform"));
        Assert.Equal("200%", page.Text("guideFrame().style.width"));
        Assert.Equal("200%", page.Text("guideFrame().style.height"));
    }

    [Fact]
    public void Larger_text_grows_the_site_and_saves_the_new_size()
    {
        var page = Opened();

        page.Click("windowButton('bigger')");

        Assert.Equal("scale(0.9)", page.Text("guideFrame().style.transform"));
        Assert.Equal(new PageCommand.TextSized(90), page.Sent.Single());
    }

    [Fact]
    public void Smaller_text_stops_at_the_smallest_size()
    {
        var page = new PageScript();

        page.Setup(ViewOf(Online, textSize: TextSize.Min));
        page.Toggle();
        page.Forget();
        page.Click("windowButton('smaller')");

        Assert.Equal("scale(0.5)", page.Text("guideFrame().style.transform"));
        Assert.Empty(page.Sent.OfType<PageCommand.TextSized>());
    }

    [Fact]
    public void The_window_frame_carries_the_name_the_site_shows_its_short_header_for()
    {
        var page = Opened();

        Assert.Equal("field-guide", page.Text("guideFrame().name"));
    }

    [Fact]
    public void Resizing_from_a_corner_moves_both_its_edges()
    {
        var page = Opened();

        // The bottom-right corner pulled 100 px left and 50 px up.
        page.Run("dragFrom(guideWindow().querySelector('.fg-se'), 960, 440, 860, 390)");

        Assert.Contains(new PageCommand.Moved(new WindowLayout(0.6, 0.08, 0.26, 0.7)), page.Sent);
    }

    [Fact]
    public void A_press_on_a_title_bar_button_is_a_click_not_a_drag()
    {
        var page = Opened();

        page.Run("fire('mousedown', windowButton('home'), { clientX: 700, clientY: 50 })");

        Assert.DoesNotContain(new PageCommand.Dragging(), page.Sent);
    }

    [Fact]
    public void While_dragging_the_page_keeps_the_plugin_routing_every_click_to_it()
    {
        var page = Opened();

        page.Run("fire('mousedown', guideWindow().querySelector('.fg-bar'), { clientX: 700, clientY: 50 })");
        page.Forget();
        page.Poll();

        Assert.Equal(new PageCommand[] { new PageCommand.Dragging() }, page.Sent);
        Assert.Equal("block", page.Text("document.getElementById('fg-shield').style.display"));
    }

    [Fact]
    public void A_new_view_keeps_the_window_where_the_player_put_it()
    {
        var page = Opened();

        page.Run("dragFrom(guideWindow().querySelector('.fg-bar'), 500, 100, 400, 100)");
        page.Setup(ViewOf(Online));
        page.Forget();
        page.Poll();

        Assert.Contains(new PageCommand.Shown(new NormalRect(0.5, 0.08, 0.36, 0.8)), page.Sent);
    }

    [Fact]
    public void A_finished_download_reloads_the_same_page_from_the_offline_copy()
    {
        var page = Opened();

        page.Run("vuePage('file:///game/WebUI/UI/BackpackUI/BackpackUI.html', { tooltipState: { visible: true, item: { cfgId: 5 } } })");
        page.Lookup();
        page.Setup(ViewOf(Local));

        Assert.Equal(Local + "/en/go/?item=5", page.Text("guideFrame().src"));
        Assert.Equal("Offline copy", page.Text("guideWindow().querySelector('.fg-chip').textContent"));
    }

    [Fact]
    public void Maximize_fills_the_screen_and_restore_brings_back_the_saved_place()
    {
        var page = Opened();

        page.Click("windowButton('maximize')");

        Assert.Equal(new PageCommand.Shown(new NormalRect(0, 0, 1, 1)), page.Sent[^1]);
        Assert.DoesNotContain(page.Sent, command => command is PageCommand.Moved);

        page.Click("windowButton('maximize')");

        Assert.Equal(new PageCommand.Shown(new NormalRect(0.6, 0.08, 0.36, 0.8)), page.Sent[^1]);
    }

    [Fact]
    public void A_maximized_window_cannot_be_dragged()
    {
        var page = Opened();

        page.Click("windowButton('maximize')");
        page.Forget();
        page.Run("dragFrom(guideWindow().querySelector('.fg-title'), 500, 100, 400, 50)");

        Assert.Empty(page.Sent);
    }

    [Fact]
    public void Minimize_keeps_only_the_title_bar_and_the_hotkey_brings_the_window_back()
    {
        var page = Opened();

        page.Click("windowButton('minimize')");

        // The 32 px title bar on a 500 px page.
        Assert.Equal(new PageCommand.Shown(new NormalRect(0.6, 0.08, 0.36, 0.064)), page.Sent[^1]);
        Assert.True(page.Is("guideWindow().classList.contains('is-min')"));

        page.Toggle();

        Assert.True(page.Is("guideWindow().classList.contains('is-open')"));
        Assert.False(page.Is("guideWindow().classList.contains('is-min')"));
        Assert.Equal(new PageCommand.Shown(new NormalRect(0.6, 0.08, 0.36, 0.8)), page.Sent[^1]);
    }

    [Fact]
    public void A_lookup_brings_a_minimized_window_back_on_the_new_page()
    {
        var page = Opened();

        page.Click("windowButton('minimize')");
        page.Run("vuePage('file:///game/WebUI/UI/BackpackUI/BackpackUI.html', { tooltipState: { visible: true, item: { cfgId: 1201 } } })");
        page.Lookup();

        Assert.False(page.Is("guideWindow().classList.contains('is-min')"));
        Assert.Equal(InWindow("/en/go/?item=1201"), page.Text("guideFrame().src"));
    }

    [Fact]
    public void A_finished_download_reloads_the_page_the_reader_moved_to()
    {
        var page = Opened();

        page.Run("siteSays('field-guide', '/en/items/crackers/?utm_source=field-guide&utm_medium=window')");
        page.Setup(ViewOf(Local));

        Assert.Equal(Local + "/en/items/crackers/", page.Text("guideFrame().src"));
    }

    // ===== Offline with no copy =====

    [Fact]
    public void Offline_without_a_copy_offers_the_download_instead_of_the_site()
    {
        var page = Ready(null, new OfflineState.Missing(149_926_714));

        page.Toggle();
        page.Click("guideWindow().querySelector('.fg-empty').querySelector('.fg-get')");

        Assert.True(page.Is("guideWindow().querySelector('.fg-body').classList.contains('is-empty')"));
        Assert.Equal("Download for offline (150 MB)", page.Text("guideWindow().querySelector('.fg-empty').querySelector('.fg-get').textContent"));
        Assert.Contains(new PageCommand.Download(), page.Sent);
    }

    [Fact]
    public void A_download_in_progress_cannot_be_started_twice()
    {
        var page = Ready(null, new OfflineState.Downloading(30));

        page.Toggle();
        page.Click("guideWindow().querySelector('.fg-bar').querySelector('.fg-get')");

        Assert.DoesNotContain(new PageCommand.Download(), page.Sent);
    }

    // ===== Looking up what the pointer is over =====

    [Fact]
    public void The_hotkey_over_a_backpack_item_opens_that_item()
    {
        var page = Ready();

        page.Run("vuePage('file:///game/WebUI/UI/BackpackUI/BackpackUI.html', { tooltipState: { visible: true, item: { cfgId: 1201 } } })");
        page.Lookup();

        Assert.Equal(InWindow("/en/go/?item=1201"), page.Text("guideFrame().src"));
    }

    [Fact]
    public void A_hidden_backpack_tooltip_is_not_a_hover()
    {
        var page = Ready();

        page.Run("vuePage('file:///game/WebUI/UI/BackpackUI/BackpackUI.html', { tooltipState: { visible: false, item: { cfgId: 1201 } } })");
        page.Lookup();

        Assert.Equal(InWindow("/en/"), page.Text("guideFrame().src"));
    }

    [Fact]
    public void A_trader_shelf_item_is_named_by_its_id_and_a_bag_item_by_its_config_id()
    {
        var page = Ready();

        page.Run("var shelfItem = { id: 55, cfgId: 9001 }; var trade = vuePage('file:///game/WebUI/UI/TradeUI/TradeUI.html', { shelf: { items: [shelfItem] }, tooltipState: { visible: true, item: shelfItem } })");
        page.Lookup();
        Assert.Equal(InWindow("/en/go/?item=55"), page.Text("guideFrame().src"));

        page.Run("trade.getElementById('app')._vnode.component.setupState.tooltipState.item = { id: 777, cfgId: 56 }");
        page.Lookup();
        Assert.Equal(InWindow("/en/go/?item=56"), page.Text("guideFrame().src"));
    }

    [Fact]
    public void The_hotkey_over_a_map_marker_opens_that_place()
    {
        var page = Ready();

        // The locked location has no marker on the explore map, so the first marker is the second location.
        page.Run("var map = openMap('ExploreMap', [{ configId: 10, lockState: 2 }, { configId: 11, lockState: 0 }, { configId: 12, lockState: 0, isActive: true }])");
        page.Poll();
        page.Run("fire('mouseover', marker(map, 0))");
        page.Lookup();

        Assert.Equal(InWindow("/en/go/?place=11"), page.Text("guideFrame().src"));
    }

    [Fact]
    public void Over_no_marker_the_selected_place_is_looked_up()
    {
        var page = Ready();

        page.Run("var map = openMap('PreDisasterMap', [{ configId: 20 }, { configId: 21, isActive: true }])");
        page.Poll();
        page.Run("fire('mouseover', map.body)");
        page.Lookup();

        Assert.Equal(InWindow("/en/go/?place=21"), page.Text("guideFrame().src"));
    }

    [Fact]
    public void The_lookup_uses_the_game_language()
    {
        var page = new PageScript();

        page.Setup(ViewOf(Online, isChinese: true));
        page.Run("vuePage('file:///game/WebUI/UI/BackpackUI/BackpackUI.html', { tooltipState: { visible: true, item: { cfgId: 3 } } })");
        page.Lookup();

        Assert.Equal(InWindow("/zh/go/?item=3"), page.Text("guideFrame().src"));
    }

    [Fact]
    public void The_hotkey_over_nothing_toggles_the_window()
    {
        var page = Ready();

        page.Lookup();
        Assert.True(page.Is("guideWindow().classList.contains('is-open')"));

        page.Lookup();
        Assert.False(page.Is("guideWindow().classList.contains('is-open')"));
    }

    [Fact]
    public void The_offline_copy_opens_plain_addresses_since_it_has_no_analytics()
    {
        var page = Ready(Local);

        page.Toggle();

        Assert.Equal(Local + "/en/", page.Text("guideFrame().src"));
    }

    // ===== The phone app =====

    private static PageScript WithPhone(string? siteBase = Online, OfflineState? state = null)
    {
        var page = Ready(siteBase, state);

        page.Run("openPhone()");
        page.Poll();

        return page;
    }

    [Fact]
    public void The_phone_home_screen_gets_one_field_guide_app()
    {
        var page = WithPhone();

        page.Poll();

        Assert.Equal(1, page.Count("phonePage().querySelectorAll('.fg-app')"));
        Assert.Equal("Field Guide", page.Text("phonePage().querySelector('.fg-app').querySelector('.app-label').textContent"));
    }

    [Fact]
    public void The_phone_app_shows_the_site_in_the_phone_and_holds_the_keyboard()
    {
        var page = WithPhone();

        page.Click("phonePage().querySelector('.fg-app')");

        Assert.Equal(OnPhone("/en/"), page.Text("phonePage().querySelector('.fg-phone-frame').src"));
        Assert.Equal("field-guide-phone", page.Text("phonePage().querySelector('.fg-phone-frame').name"));
        Assert.Contains(new PageCommand.Phone(true), page.Sent);
    }

    [Fact]
    public void Back_closes_the_phone_app_and_gives_the_keyboard_back()
    {
        var page = WithPhone();

        page.Click("phonePage().querySelector('.fg-app')");
        page.Click("phonePage().querySelector('.fg-phone-back')");

        Assert.False(page.Is("phonePage().querySelector('.fg-phone')"));
        Assert.Equal(new PageCommand.Phone(false), page.Sent[^1]);
    }

    [Fact]
    public void The_phone_close_button_closes_the_app_and_gives_the_keyboard_back()
    {
        var page = WithPhone();

        page.Click("phonePage().querySelector('.fg-app')");
        page.Click("phonePage().querySelector('.fg-phone-x')");

        Assert.False(page.Is("phonePage().querySelector('.fg-phone')"));
        Assert.Equal(new PageCommand.Phone(false), page.Sent[^1]);
    }

    [Fact]
    public void A_minimized_phone_app_opens_again_on_the_same_page()
    {
        var page = WithPhone();

        page.Click("phonePage().querySelector('.fg-app')");
        page.Run("phonePage().querySelector('.fg-phone-frame').marked = true");
        page.Click("phonePage().querySelector('.fg-phone-minimize')");

        Assert.Equal("none", page.Text("phonePage().querySelector('.fg-phone').style.display"));
        Assert.Equal(new PageCommand.Phone(false), page.Sent[^1]);

        page.Click("phonePage().querySelector('.fg-app')");

        Assert.Equal("", page.Text("phonePage().querySelector('.fg-phone').style.display"));
        Assert.True(page.Is("phonePage().querySelector('.fg-phone-frame').marked"));
        Assert.Equal(new PageCommand.Phone(true), page.Sent[^1]);
    }

    [Fact]
    public void Expand_moves_the_phone_page_to_the_large_window()
    {
        var page = WithPhone();

        page.Click("phonePage().querySelector('.fg-app')");
        page.Run("siteSays('field-guide-phone', '/en/map/pre-market/?utm_source=field-guide&utm_medium=phone')");
        page.Click("phonePage().querySelector('.fg-phone-expand')");

        Assert.False(page.Is("phonePage().querySelector('.fg-phone')"));
        Assert.Contains(new PageCommand.Phone(false), page.Sent);
        Assert.True(page.Is("guideWindow().classList.contains('is-open')"));
        Assert.Equal(InWindow("/en/map/pre-market/"), page.Text("guideFrame().src"));
    }

    [Fact]
    public void A_message_naming_another_site_is_ignored()
    {
        var page = WithPhone();

        page.Click("phonePage().querySelector('.fg-app')");
        page.Run("siteSays('field-guide-phone', '//elsewhere.example/')");
        page.Click("phonePage().querySelector('.fg-phone-expand')");

        Assert.Equal(InWindow("/en/"), page.Text("guideFrame().src"));
    }

    [Fact]
    public void A_phone_put_away_with_the_app_open_gives_the_keyboard_back()
    {
        var page = WithPhone();

        page.Click("phonePage().querySelector('.fg-app')");
        page.Run("dropPage(/PhoneSMS/)");
        page.Forget();
        page.Poll();

        Assert.Contains(new PageCommand.Phone(false), page.Sent);
    }

    [Fact]
    public void The_phone_app_without_a_copy_offers_the_download_and_shows_the_site_once_it_lands()
    {
        var page = WithPhone(null, new OfflineState.Missing(0));

        page.Click("phonePage().querySelector('.fg-app')");
        page.Click("phonePage().querySelector('.fg-phone-get')");

        Assert.Contains(new PageCommand.Download(), page.Sent);

        page.Setup(ViewOf(Local));

        Assert.Equal(Local + "/en/", page.Text("phonePage().querySelector('.fg-phone-frame').src"));
        Assert.False(page.Is("phonePage().querySelector('.fg-phone-get')"));
    }
}
