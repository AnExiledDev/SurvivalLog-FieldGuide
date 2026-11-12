using System.IO;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using ExiledToolkit;
using ExiledToolkit.Kit;
using ExiledToolkit.Web;
using FieldGuide.Adapters;
using FieldGuide.Core;
using HarmonyLib;

namespace FieldGuide;

[BepInPlugin(Guid, "Field Guide", Version)]
[BepInDependency(ToolkitGuid, ToolkitVersion)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.fieldguide";
    private const string Version = "0.3.3";

    /// <summary>Exiled's Toolkit: its shared helpers, page messages, UI size, live settings and typing check all come from it.</summary>
    private const string ToolkitGuid = "com.dev.survivallog.exiledtoolkit";
    private const string ToolkitVersion = ">=0.19.0";

    /// <summary>The name the page script hands its window to the Toolkit's UI size with.</summary>
    private const string UiSizeName = "FieldGuide";

    public override void Load()
    {
        var log = new ModLog(Log, "field-guide.log");
        // Named in full: the Toolkit has an InputGate of its own beside PatchKit.
        var patches = new ExiledToolkit.Game.PatchKit(new Harmony(Guid), log.Write);
        var offline = new OfflineCopy(Path.Combine(Paths.BepInExRootPath, "FieldGuide"), log.Write);
        var page = new WebPage(typeof(Plugin).Assembly, "FieldGuide.page.js", "window.__slfieldguide", PageCommand.Prefix + "noscript", log.Write)
        {
            // The page reads its words from here at every redraw, so a change of the game's language shows at the next call.
            Words = isChinese => "window.__slfieldguideWords=" + Texts.Json(isChinese) + ";",
        };
        var session = new GuideSession(page, PageInbox.Listen(PageCommand.Prefix), offline, new LocalServer(offline.SiteFolder, log.Write), new GuideSettings(Config), log.Write);

        ModUiSize.Bind(Config, UiSizeName);

        log.Write($"===== Field Guide {Version} started");
        InputGate.Apply(patches);

        GuideTicker.Session = session;
        GuideTicker.Log = log.Write;
        AddComponent<GuideTicker>();
    }
}
