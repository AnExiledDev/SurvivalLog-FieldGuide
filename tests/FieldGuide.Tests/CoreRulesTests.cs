using FieldGuide.Core;
using Xunit;

namespace FieldGuide.Tests;

public class PageCommandTests
{
    [Fact]
    public void The_window_place_is_read_from_page_fractions()
    {
        Assert.Equal(new PageCommand.Shown(new NormalRect(0.6, 0.08, 0.36, 0.8)), PageCommand.Parse("slmod|fieldguide|shown|0.6,0.08,0.36,0.8"));
    }

    [Fact]
    public void A_moved_window_is_kept_on_screen_and_above_the_minimum_size()
    {
        var moved = Assert.IsType<PageCommand.Moved>(PageCommand.Parse("slmod|fieldguide|moved|0.95,-0.2,0.01,0.5"));

        Assert.Equal(new WindowLayout(1 - WindowLayout.MinW, 0, WindowLayout.MinW, 0.5), moved.Layout);
    }

    [Theory]
    [InlineData("slmod|fieldguide|phone|1", true)]
    [InlineData("slmod|fieldguide|phone|0", false)]
    public void The_phone_app_says_whether_it_is_open(string message, bool isOpen)
    {
        Assert.Equal(new PageCommand.Phone(isOpen), PageCommand.Parse(message));
    }

    [Theory]
    [InlineData("slmod|fieldguide|textsize|90", 90)]
    [InlineData("slmod|fieldguide|textsize|10", TextSize.Min)]
    [InlineData("slmod|fieldguide|textsize|900", TextSize.Max)]
    public void A_text_size_is_kept_within_what_stays_readable(string message, int percent)
    {
        Assert.Equal(new PageCommand.TextSized(percent), PageCommand.Parse(message));
    }

    [Theory]
    [InlineData("slmod|fieldguide|textsize|-20")]
    [InlineData("slmod|fieldguide|textsize|big")]
    [InlineData("slmod|fieldguide|shown|a,b,c,d")]
    [InlineData("slmod|fieldguide|shown|NaN,0,1,1")]
    [InlineData("slmod|fieldguide|phone|yes")]
    [InlineData("slmod|fieldguide|diag|HUD button placed")]
    [InlineData("slmod|fieldguide|toggle|extra")]
    [InlineData("slmod|pkgbank|toggle")]
    public void Anything_malformed_or_not_ours_is_logged_not_acted_on(string message)
    {
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse(message));
    }
}

public class WindowLayoutTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("left side")]
    [InlineData("0.1,0.1,0.5")]
    public void A_saved_place_that_cannot_be_read_gives_the_default(string? saved)
    {
        Assert.Equal(WindowLayout.Default, WindowLayout.Parse(saved));
    }

    [Fact]
    public void A_saved_place_off_the_screen_is_pulled_back_on()
    {
        Assert.Equal(new WindowLayout(0.5, 0, 0.5, 1), WindowLayout.Parse("0.9,-3,0.5,2"));
    }

    [Fact]
    public void The_saved_place_reads_back_as_written()
    {
        var layout = new WindowLayout(0.1234, 0.2, 0.4, 0.55);

        Assert.Equal(layout, WindowLayout.Parse(layout.Format()));
    }
}

public class HitAreaTests
{
    private static readonly NormalRect RightSide = new(0.6, 0.08, 0.36, 0.8);

    [Fact]
    public void A_point_in_unity_space_is_flipped_into_page_space()
    {
        // A 1000x500 view pivoted at its centre: (300, 200) is right and near the top of the page.
        Assert.True(RightSide.ContainsLocalPoint(300, 200, 1000, 500, 0.5, 0.5));
        Assert.False(RightSide.ContainsLocalPoint(300, -240, 1000, 500, 0.5, 0.5));
        Assert.False(RightSide.ContainsLocalPoint(-300, 200, 1000, 500, 0.5, 0.5));
    }

    [Fact]
    public void The_window_stops_taking_clicks_when_the_page_stops_reporting_it()
    {
        var area = new ClickArea();

        area.Heard(RightSide, now: 10);

        Assert.Equal(RightSide, area.At(10 + ClickArea.StaleSeconds));
        Assert.Null(area.At(10 + ClickArea.StaleSeconds + 0.1));
    }

    [Fact]
    public void A_closed_window_takes_no_clicks()
    {
        var area = new ClickArea();

        area.Heard(RightSide, now: 10);
        area.Forget();

        Assert.Null(area.At(10));
    }
}

public class SitePathTests
{
    private static readonly HashSet<string> Files = new() { "index.html", "en/index.html", "en/items/rope/index.html", "en/go/index.html", "_astro/site.css", "pagefind/pagefind.wasm" };

    private static SiteFile Resolve(string target) => SitePath.Resolve(target, Files.Contains);

    [Theory]
    [InlineData("/", "index.html")]
    [InlineData("/en/", "en/index.html")]
    [InlineData("/en/items/rope/", "en/items/rope/index.html")]
    [InlineData("/en/items/rope", "en/items/rope/index.html")]
    [InlineData("/en/go/?item=1201", "en/go/index.html")]
    [InlineData("/_astro/site.css", "_astro/site.css")]
    [InlineData("/pagefind/pagefind.wasm#x", "pagefind/pagefind.wasm")]
    public void A_page_address_maps_to_its_file_as_the_live_site_serves_it(string target, string file)
    {
        Assert.Equal(new SiteFile.Found(file), Resolve(target));
    }

    [Theory]
    [InlineData("/en/items/hammer/")]
    [InlineData("/missing.css")]
    public void A_page_the_copy_does_not_have_is_not_found(string target)
    {
        Assert.IsType<SiteFile.NotFound>(Resolve(target));
    }

    [Theory]
    [InlineData("/../BepInEx/config/x.cfg")]
    [InlineData("/en/../../secret")]
    [InlineData("/%2e%2e/secret")]
    [InlineData("/en/./index.html")]
    [InlineData("/C:/Windows/win.ini")]
    [InlineData("/en\\..\\secret")]
    [InlineData("en/index.html")]
    public void Nothing_outside_the_site_folder_can_be_asked_for(string target)
    {
        Assert.IsType<SiteFile.Refused>(SitePath.Resolve(target, _ => true));
    }
}

public class OfflineTests
{
    private const string Local = "http://127.0.0.1:5123";

    [Theory]
    [InlineData(SiteSource.Auto, Local, Local)]
    [InlineData(SiteSource.Auto, null, GuideView.OnlineBase)]
    [InlineData(SiteSource.Online, Local, GuideView.OnlineBase)]
    [InlineData(SiteSource.Offline, Local, Local)]
    [InlineData(SiteSource.Offline, null, null)]
    public void The_window_reads_the_copy_the_source_setting_asks_for(SiteSource source, string? localBase, string? expected)
    {
        Assert.Equal(expected, Offline.BaseFor(source, localBase, GuideView.OnlineBase));
    }

    [Fact]
    public void The_site_bundle_file_is_read()
    {
        const string json = "{\"version\":\"2026-10-22-5be686ac3b80\",\"bytes\":149926714,\"sha256\":\"5be686ac3b80d9b832691092d854031058d995cf7efb6270310f8e6aef95a5a9\"}";

        Assert.True(BundleInfo.TryParse(json, out var info));
        Assert.Equal(new BundleInfo("2026-10-22-5be686ac3b80", 149926714, "5be686ac3b80d9b832691092d854031058d995cf7efb6270310f8e6aef95a5a9"), info);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("<html>Bad gateway</html>")]
    [InlineData("{\"version\":\"v\",\"bytes\":10,\"sha256\":\"short\"}")]
    public void A_bundle_file_without_a_checksum_is_not_trusted(string? json)
    {
        Assert.False(BundleInfo.TryParse(json, out _));
    }

    [Fact]
    public void A_copy_older_than_the_site_offers_an_update_and_a_current_one_does_not()
    {
        var site = new BundleInfo("v2", 150_000_000, new string('a', 64));

        Assert.Equal(new OfflineState.Ready("v1", 150_000_000), new OfflineState.Ready("v1", 0).WithSiteBundle(site));
        Assert.Equal(new OfflineState.Ready("v2", 0), new OfflineState.Ready("v2", 0).WithSiteBundle(site));
        Assert.Equal(new OfflineState.Missing(150_000_000), new OfflineState.Missing(0).WithSiteBundle(site));
        Assert.Equal(new OfflineState.Downloading(40), new OfflineState.Downloading(40).WithSiteBundle(site));
    }

    [Fact]
    public void A_failed_download_keeps_the_copy_it_was_replacing()
    {
        Assert.True(new OfflineState.Failed("hash mismatch", KeptCopy: true).HasCopy);
        Assert.False(new OfflineState.Failed("no network", KeptCopy: false).HasCopy);
        Assert.False(new OfflineState.Downloading(50).HasCopy);
    }

    [Fact]
    public void The_bundle_is_asked_for_by_its_checksum_so_a_cached_older_zip_is_never_served()
    {
        var older = new BundleInfo("v1", 1, new string('a', 64));
        var newer = new BundleInfo("v2", 1, new string('b', 64));

        Assert.NotEqual(Offline.BundleAddress(GuideView.OnlineBase, older), Offline.BundleAddress(GuideView.OnlineBase, newer));
        Assert.StartsWith(GuideView.OnlineBase + "/offline/field-guide-site.zip?", Offline.BundleAddress(GuideView.OnlineBase, newer));
    }

    [Fact]
    public void A_download_shows_99_until_it_is_done()
    {
        Assert.Equal(99, Offline.Percent(999, 1000));
        Assert.Equal(99, Offline.Percent(1000, 1000));
        Assert.Equal(0, Offline.Percent(10, 0));
    }
}

public class GuideViewTests
{
    [Fact]
    public void The_download_button_names_its_size_once_the_site_said_it()
    {
        Assert.Equal(new OfflineButton("Download for offline (150 MB)", true), GuideView.Button(new OfflineState.Missing(149_926_714), false));
        Assert.Equal(new OfflineButton("Download for offline", true), GuideView.Button(new OfflineState.Missing(0), false));
    }

    [Fact]
    public void A_download_in_progress_cannot_be_started_again()
    {
        Assert.Equal(new OfflineButton("Downloading 40%", false), GuideView.Button(new OfflineState.Downloading(40), false));
    }

    [Fact]
    public void A_current_copy_shows_no_button_and_an_old_one_offers_the_update()
    {
        Assert.Equal(new OfflineButton(null, false), GuideView.Button(new OfflineState.Ready("v2", 0), false));
        Assert.Equal(new OfflineButton("Update offline copy (2 MB)", true), GuideView.Button(new OfflineState.Ready("v1", 2_000_000), false));
    }

    [Fact]
    public void A_failed_download_can_be_tried_again()
    {
        Assert.True(GuideView.Button(new OfflineState.Failed("timeout", false), false).CanDownload);
    }

    [Fact]
    public void The_view_carries_the_saved_text_size_kept_in_range()
    {
        Assert.Contains("\"textSize\":150", GuideView.Json(null, false, WindowLayout.Default, 400, new OfflineState.Missing(0), "/"));
    }

    [Fact]
    public void The_button_follows_the_game_language()
    {
        Assert.Equal("下载中 40%", GuideView.Button(new OfflineState.Downloading(40), true).Label);
    }

    [Fact]
    public void The_view_says_which_copy_the_window_shows()
    {
        var state = new OfflineState.Ready("v1", 0);

        Assert.Contains("\"source\":\"Online\"", GuideView.Json(GuideView.OnlineBase, false, WindowLayout.Default, TextSize.Default, state, "/"));
        Assert.Contains("\"source\":\"Offline copy\"", GuideView.Json("http://127.0.0.1:5123", false, WindowLayout.Default, TextSize.Default, state, "/"));
        Assert.Contains("\"base\":null", GuideView.Json(null, false, WindowLayout.Default, TextSize.Default, new OfflineState.Missing(0), "/"));
    }
}

public class HttpBitsTests
{
    [Fact]
    public void Only_reads_are_served()
    {
        Assert.True(RequestLine.TryParse("GET /en/ HTTP/1.1", out var get));
        Assert.True(get.IsReadable);
        Assert.True(RequestLine.TryParse("POST /en/ HTTP/1.1", out var post));
        Assert.False(post.IsReadable);
        Assert.False(RequestLine.TryParse("GET /en/", out _));
    }

    [Fact]
    public void Search_index_is_served_as_webassembly()
    {
        Assert.Equal("application/wasm", HttpBits.ContentType("pagefind/pagefind.wasm"));
    }
}
