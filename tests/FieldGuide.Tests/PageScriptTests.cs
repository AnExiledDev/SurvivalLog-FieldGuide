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

    // The page check runs every 400 ms on every HUD page, so a button already in place must cost no search.
    [Fact]
    public void A_placed_hud_button_costs_the_page_check_no_search()
    {
        var page = Ready();

        page.Run("var hud = openHud()");
        page.Poll();
        page.Run("var searches = countSearches(hud)");
        page.Poll();
        page.Poll();

        Assert.Equal("0", page.Text("searches.count"));
    }

    [Fact]
    public void A_hud_column_the_game_builds_again_gets_the_button_again()
    {
        var page = Ready();

        page.Run("var hud = openHud()");
        page.Poll();
        page.Run("hud.querySelector('.vertical-tools').remove(); var column = hud.body.appendChild(new FakeElement('div', hud)); column.className = 'vertical-tools'");
        page.Poll();

        Assert.Equal(1, page.Count("hud.querySelectorAll('.fg-hud-btn')"));
        Assert.True(page.Is("hud.querySelector('.fg-hud-btn').parentNode === column"));
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

        // The 22 px title bar on a 500 px page.
        Assert.Equal(new PageCommand.Shown(new NormalRect(0.6, 0.08, 0.36, 0.044)), page.Sent[^1]);
        Assert.True(page.Is("guideWindow().classList.contains('is-min')"));

        page.Toggle();

        Assert.True(page.Is("guideWindow().classList.contains('is-open')"));
        Assert.False(page.Is("guideWindow().classList.contains('is-min')"));
        Assert.Equal(new PageCommand.Shown(new NormalRect(0.6, 0.08, 0.36, 0.8)), page.Sent[^1]);
    }

    [Fact]
    public void A_minimized_window_drags_down_to_the_bottom_edge_and_comes_back_wholly_on_screen()
    {
        var page = Opened();

        page.Click("windowButton('minimize')");
        page.Run("dragFrom(guideWindow().querySelector('.fg-bar'), 500, 100, 500, 900)");

        // Only the bar has to stay on screen: 1 - 22/500.
        Assert.Equal(new PageCommand.Shown(new NormalRect(0.6, 0.956, 0.36, 0.044)), page.Sent.OfType<PageCommand.Shown>().Last());

        page.Forget();
        page.Click("windowButton('minimize')");

        Assert.Equal(new PageCommand.Shown(new NormalRect(0.6, 0.2, 0.36, 0.8)), page.Sent.OfType<PageCommand.Shown>().Last());
        Assert.Contains(page.Sent, command => command is PageCommand.Moved moved && moved.Layout.Format() == "0.6,0.2,0.36,0.8");
    }

    [Fact]
    public void A_minimized_window_shrinks_to_its_buttons_at_its_right_edge_and_restore_brings_back_its_size()
    {
        var page = Opened();

        // The page lays the shrunk bar out 250 px wide on a 1000 px screen.
        page.Run("guideWindow().offsetWidth = 250");
        page.Click("windowButton('minimize')");

        Assert.Equal(new PageCommand.Shown(new NormalRect(0.71, 0.08, 0.25, 0.044)), page.Sent[^1]);
        Assert.Equal("auto", page.Text("guideWindow().style.width"));
        Assert.Equal("auto", page.Text("guideWindow().style.left"));

        page.Click("windowButton('minimize')");

        Assert.Equal(new PageCommand.Shown(new NormalRect(0.6, 0.08, 0.36, 0.8)), page.Sent[^1]);
        Assert.Equal("auto", page.Text("guideWindow().style.right"));
        Assert.NotEqual("auto", page.Text("guideWindow().style.width"));
    }

    [Fact]
    public void A_minimized_bar_drags_to_the_left_edge_and_the_window_comes_back_wholly_on_screen()
    {
        var page = Opened();

        page.Run("guideWindow().offsetWidth = 250");
        page.Click("windowButton('minimize')");
        page.Run("dragFrom(guideWindow().querySelector('.fg-bar'), 800, 100, 0, 100)");

        // The bar's left edge stops at the screen's, though the window's saved left edge would be past it.
        Assert.Equal(new PageCommand.Shown(new NormalRect(0, 0.08, 0.25, 0.044)), page.Sent.OfType<PageCommand.Shown>().Last());

        page.Forget();
        page.Click("windowButton('minimize')");

        Assert.Equal(new PageCommand.Shown(new NormalRect(0, 0.08, 0.36, 0.8)), page.Sent.OfType<PageCommand.Shown>().Last());
        Assert.Contains(page.Sent, command => command is PageCommand.Moved moved && moved.Layout.Format() == "0,0.08,0.36,0.8");
    }

    [Fact]
    public void A_window_closed_while_minimized_at_the_bottom_reopens_wholly_on_screen()
    {
        var page = Opened();

        page.Click("windowButton('minimize')");
        page.Run("dragFrom(guideWindow().querySelector('.fg-bar'), 500, 100, 500, 900)");
        page.Click("guideWindow().querySelector('.fg-x')");
        page.Forget();
        page.Toggle();

        Assert.Equal(new PageCommand.Shown(new NormalRect(0.6, 0.2, 0.36, 0.8)), page.Sent.OfType<PageCommand.Shown>().Last());
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

    // ===== UI size (Exiled's Toolkit) =====

    // Stands in for Exiled's Toolkit's window.ExiledUi: keeps what each call handed it, and zoomTo(z) draws every
    // handed element at z and tells its mod, as the Toolkit does after a change.
    private const string FakeUi = "var handed = []; window.ExiledUi = { size: function (el, options) { handed.push({ el: el, options: options }); } };" +
        "function zoomTo(z) { handed.forEach(function (h) { h.el.style.zoom = String(z); if (h.options.onZoom) h.options.onZoom(z); }); }";

    private static PageScript OpenedWithToolkit()
    {
        var page = Ready();

        page.Run(FakeUi);
        page.Toggle();
        page.Forget();

        return page;
    }

    [Fact]
    public void The_window_is_handed_to_the_toolkit_once_and_never_shrunk()
    {
        var page = OpenedWithToolkit();

        page.Toggle();
        page.Toggle();

        // Opening again reuses the window it built, so the Toolkit is not handed it twice.
        Assert.Equal("1", page.Text("handed.length"));
        Assert.Equal("1", page.Text("handed.filter(function (h) { return h.options.mod === 'FieldGuide'; }).length"));
        Assert.True(page.Is("handed[0].el === guideWindow()"));
        // The player sized the window, so it is never shrunk to fit.
        Assert.True(page.Is("handed[0].options.fit === false"));
    }

    [Fact]
    public void A_window_the_page_lost_is_handed_again_when_it_is_built_anew()
    {
        var page = OpenedWithToolkit();

        page.Toggle();
        page.Run("guideWindow().remove()");
        page.Toggle();

        Assert.Equal("2", page.Text("handed.length"));
        Assert.True(page.Is("handed[1].el === guideWindow()"));
        Assert.True(page.Is("handed[1].options.fit === false"));
    }

    [Fact]
    public void Before_the_toolkit_has_loaded_the_window_waits_in_the_list_it_reads()
    {
        var page = Opened();

        Assert.Equal("1", page.Text("window.__exiledUiWait.length"));
        Assert.True(page.Is("window.__exiledUiWait[0][0] === guideWindow()"));
        Assert.True(page.Is("window.__exiledUiWait[0][1].fit === false"));
    }

    [Fact]
    public void A_bigger_ui_size_keeps_the_window_where_the_player_put_it()
    {
        var page = OpenedWithToolkit();

        page.Run("zoomTo(2)");

        // Lengths inside the window are doubled on screen, so its place is written at half to land on the same spot.
        Assert.Equal("30vw", page.Text("guideWindow().style.left"));
        Assert.Equal("4vh", page.Text("guideWindow().style.top"));
        Assert.Equal("18vw", page.Text("guideWindow().style.width"));
        Assert.Equal("40vh", page.Text("guideWindow().style.height"));
        Assert.Equal(new PageCommand.Shown(new NormalRect(0.6, 0.08, 0.36, 0.8)), page.Sent[^1]);
    }

    [Fact]
    public void A_minimized_bar_at_a_bigger_ui_size_keeps_the_clicks_over_all_of_it()
    {
        var page = OpenedWithToolkit();

        // The bar lays out 125 px wide in its own px, drawn at twice that.
        page.Run("guideWindow().offsetWidth = 125");
        page.Run("zoomTo(2)");
        page.Forget();
        page.Click("windowButton('minimize')");

        // 250 of 1000 px wide and the 22 px bar drawn 44 px high on a 500 px page.
        Assert.Equal(new PageCommand.Shown(new NormalRect(0.71, 0.08, 0.25, 0.088)), page.Sent[^1]);
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
        var page = Ready(null, new OfflineState.Downloading(30, false));

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
    public void The_hotkey_over_a_workbench_item_opens_that_item_by_its_config_id()
    {
        var page = Ready();

        page.Run("var brew = workbenchPage('file:///game/WebUI/UI/BrewPanel/BrewPanel.html', [70001, 70002])");
        page.Poll();
        page.Run("unitySays('BrewPanel', 'Refresh', { bag: { tabs: [{ items: [{ itemId: 70001, configId: 1201, count: 3 }, { itemId: 70002, configId: 1305 }] }] } })");
        page.Run("fire('mouseover', itemIcon(brew, 70002))");
        page.Lookup();

        Assert.Equal(InWindow("/en/go/?item=1305"), page.Text("guideFrame().src"));
    }

    [Fact]
    public void A_workbench_item_stops_counting_once_the_pointer_leaves_it_or_its_page()
    {
        var page = Ready();

        page.Run("var brew = workbenchPage('file:///game/WebUI/UI/BrewPanel/BrewPanel.html', [70001])");
        page.Poll();
        page.Run("unitySays('BrewPanel', 'Refresh', [{ itemId: 70001, configId: 1201 }])");
        page.Run("fire('mouseover', itemIcon(brew, 70001)); fire('mouseover', brew.querySelector('.grid'))");
        page.Lookup();

        Assert.Equal(InWindow("/en/"), page.Text("guideFrame().src"));

        page.Run("fire('mouseover', itemIcon(brew, 70001)); fire('mouseout', itemIcon(brew, 70001), { relatedTarget: null })");
        page.Lookup();

        Assert.Equal(InWindow("/en/"), page.Text("guideFrame().src"));
    }

    [Fact]
    public void A_message_that_only_repeats_learned_items_is_not_parsed_again()
    {
        var page = Ready();

        page.Run("var brew = workbenchPage('file:///game/WebUI/UI/BrewPanel/BrewPanel.html', [70001, 70002])");
        page.Poll();
        page.Run("var parses = 0; var realParse = JSON.parse; JSON.parse = function (text) { parses++; return realParse(text); }");
        page.Run("unitySays('BrewPanel', 'Refresh', { items: [{ itemId: 70001, configId: 1201 }, { itemId: 70002, configId: 1305 }] })");
        page.Run("unitySays('BrewPanel', 'Refresh', { items: [{ itemId: 70002, configId: 1305, count: 4 }, { itemId: 70001, configId: 1201 }] })");
        page.Run("JSON.parse = realParse");

        Assert.Equal("1", page.Text("parses"));
    }

    [Fact]
    public void A_new_item_among_learned_ones_is_still_learned()
    {
        var page = Ready();

        page.Run("var brew = workbenchPage('file:///game/WebUI/UI/BrewPanel/BrewPanel.html', [70001, 70002])");
        page.Poll();
        page.Run("unitySays('BrewPanel', 'Refresh', [{ itemId: 70001, configId: 1201 }])");
        page.Run("unitySays('BrewPanel', 'Refresh', [{ itemId: 70001, configId: 1201 }, { itemId: '70002', configId: 1305 }])");
        page.Run("fire('mouseover', itemIcon(brew, 70002))");
        page.Lookup();

        Assert.Equal(InWindow("/en/go/?item=1305"), page.Text("guideFrame().src"));
    }

    [Fact]
    public void A_workbench_item_the_game_never_named_is_not_a_hover()
    {
        var page = Ready();

        page.Run("var brew = workbenchPage('file:///game/WebUI/UI/BrewPanel/BrewPanel.html', [70001])");
        page.Poll();
        page.Run("unitySays('BrewPanel', 'Refresh', [{ itemId: 70009, configId: 1201 }])");
        page.Run("fire('mouseover', itemIcon(brew, 70001))");
        page.Lookup();

        Assert.Equal(InWindow("/en/"), page.Text("guideFrame().src"));
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
    public void Moving_from_one_map_marker_to_the_next_looks_up_the_next()
    {
        var page = Ready();

        page.Run("var map = openMap('ExploreMap', [{ configId: 10, lockState: 0 }, { configId: 11, lockState: 0 }, { configId: 12, lockState: 0, isActive: true }])");
        page.Poll();
        page.Run("fire('mouseover', marker(map, 0)); fire('mouseover', marker(map, 1))");
        page.Lookup();

        Assert.Equal(InWindow("/en/go/?place=11"), page.Text("guideFrame().src"));

        page.Run("fire('mouseover', map.body)");
        page.Lookup();

        Assert.Equal(InWindow("/en/go/?place=12"), page.Text("guideFrame().src"));
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

    // The game shows a new phone page two frames after it starts, before the next page check: the app has to be there
    // by then, not on the next poll.
    [Fact]
    public void A_phone_that_just_opened_has_the_app_by_the_next_frame_without_a_page_check()
    {
        var page = Ready();

        page.Run("var node = addPhoneFrame(); buildPhone(phoneArrives(node)); nextFrame()");

        Assert.Equal(1, page.Count("phonePage().querySelectorAll('.fg-app')"));
    }

    [Fact]
    public void The_app_goes_in_as_soon_as_the_game_builds_the_home_screen()
    {
        var page = Ready();

        page.Run("var node = addPhoneFrame(); var doc = phoneArrives(node); nextFrame()");
        Assert.Equal(0, page.Count("phonePage().querySelectorAll('.fg-app')"));

        page.Run("buildPhone(doc); flush()");

        Assert.Equal(1, page.Count("phonePage().querySelectorAll('.fg-app')"));
    }

    [Fact]
    public void Every_new_phone_page_gets_the_app_again()
    {
        var page = Ready();

        page.Run("var node = addPhoneFrame(); buildPhone(phoneArrives(node)); nextFrame()");
        page.Run("dropPage(/PhoneSMS/); flush(); node = addPhoneFrame(); buildPhone(phoneArrives(node)); nextFrame()");

        Assert.Equal(1, page.Count("document.querySelectorAll('iframe')"));
        Assert.Equal(1, page.Count("phonePage().querySelectorAll('.fg-app')"));
    }

    [Fact]
    public void A_page_that_reloads_is_watched_again()
    {
        var page = Ready();

        page.Run("var node = addPhoneFrame(); buildPhone(phoneArrives(node)); nextFrame()");
        page.Run("var doc = phoneArrives(node); fireLoad(node); buildPhone(doc); flush()");

        Assert.Equal(1, page.Count("phonePage().querySelectorAll('.fg-app')"));
    }

    [Fact]
    public void A_frame_sent_to_the_phone_from_another_page_gets_the_app()
    {
        var page = Ready();

        page.Run("var node = document.body.appendChild(new FakeElement('iframe', document)); node.src = 'file:///game/WebUI/UI/Bag/Bag.html'; node.contentDocument = new FakeDocument(node.src); flush()");
        page.Run("navigate(node, PHONE_URL); buildPhone(phoneArrives(node)); nextFrame()");

        Assert.Equal(1, page.Count("phonePage().querySelectorAll('.fg-app')"));
    }

    [Fact]
    public void Other_pages_never_get_the_app()
    {
        var page = Ready();

        page.Run("var node = document.body.appendChild(new FakeElement('iframe', document)); node.src = 'file:///game/WebUI/UI/Bag/Bag.html'; node.contentDocument = buildPhone(new FakeDocument(node.src)); flush(); nextFrame()");

        Assert.Equal(0, page.Count("node.contentDocument.querySelectorAll('.fg-app')"));
    }

    [Fact]
    public void The_app_is_not_added_twice_when_the_watcher_and_the_page_check_both_run()
    {
        var page = Ready();

        page.Run("var node = addPhoneFrame(); buildPhone(phoneArrives(node)); nextFrame()");
        page.Poll();
        page.Run("flush(); nextFrame()");

        Assert.Equal(1, page.Count("phonePage().querySelectorAll('.fg-app')"));
    }

    // The game and other mods change the open phone page all the time. Once the app is in, a change must not search
    // the page, or every change on the phone pays for it.
    [Fact]
    public void Once_the_app_is_in_changes_on_the_phone_page_search_nothing()
    {
        var page = Ready();

        page.Run("var node = addPhoneFrame(); var doc = phoneArrives(node); buildPhone(doc); nextFrame(); var grid = doc.querySelector('.app-grid'); var searches = countSearches(doc)");
        page.Run("for (var i = 0; i < 20; i++) { grid.appendChild(new FakeElement('div', doc)); flush(); }");
        page.Poll();
        page.Run("nextFrame()");

        Assert.Equal("0", page.Text("searches.count"));
        Assert.Equal(1, page.Count("phonePage().querySelectorAll('.fg-app')"));
    }

    [Fact]
    public void An_app_the_game_takes_off_the_home_screen_goes_back_on()
    {
        var page = Ready();

        page.Run("var node = addPhoneFrame(); var doc = phoneArrives(node); buildPhone(doc); nextFrame()");
        page.Run("doc.querySelector('.fg-app').remove(); flush()");

        Assert.Equal(1, page.Count("phonePage().querySelectorAll('.fg-app')"));
        Assert.True(page.Is("doc.querySelector('.fg-app').parentNode === doc.querySelector('.app-grid')"));
    }

    [Fact]
    public void A_home_screen_the_game_builds_again_gets_the_app_again()
    {
        var page = Ready();

        page.Run("var node = addPhoneFrame(); var doc = phoneArrives(node); buildPhone(doc); nextFrame()");
        page.Run("doc.querySelector('.sms-panel').remove(); flush()");
        page.Run("buildPhone(doc); flush()");

        Assert.Equal(1, page.Count("phonePage().querySelectorAll('.fg-app')"));
    }

    // Exiled's Toolkit moves the app into the phone's dock; the app stays there and no second one goes in.
    [Fact]
    public void An_app_moved_elsewhere_on_the_phone_page_is_left_where_it_is()
    {
        var page = Ready();

        page.Run("var node = addPhoneFrame(); var doc = phoneArrives(node); buildPhone(doc); nextFrame()");
        page.Run("var dock = doc.body.appendChild(new FakeElement('div', doc)); dock.appendChild(doc.querySelector('.fg-app')); flush()");
        page.Poll();

        Assert.Equal(1, page.Count("phonePage().querySelectorAll('.fg-app')"));
        Assert.True(page.Is("doc.querySelector('.fg-app').parentNode === dock"));
    }

    // Each load of a phone frame starts a look for its page; a frame keeps only the newest, not one per load.
    [Fact]
    public void A_phone_frame_that_loads_again_keeps_one_look_for_its_page()
    {
        var page = Ready();

        page.Run("var node = addPhoneFrame(); fireLoad(node); fireLoad(node); nextFrame()");

        Assert.Equal(1, page.Count("frameCallbacks"));
    }

    [Fact]
    public void The_look_for_a_phone_page_that_never_comes_stops()
    {
        var page = Ready();

        page.Run("var node = addPhoneFrame(); for (var i = 0; i < 200; i++) nextFrame()");

        Assert.Equal(0, page.Count("frameCallbacks"));
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
    public void A_phone_the_game_hides_with_the_app_open_gives_the_keyboard_back()
    {
        var page = WithPhone();

        page.Click("phonePage().querySelector('.fg-app')");
        page.Poll();
        page.Forget();
        page.Run("document.querySelectorAll('iframe').filter(function (node) { return /PhoneSMS/.test(node.src); })[0].style.display = 'none'");
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
