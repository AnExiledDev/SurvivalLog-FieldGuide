using System.IO;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using FieldGuide.Adapters;
using HarmonyLib;

namespace FieldGuide;

[BepInPlugin(Guid, "Field Guide", Version)]
[BepInDependency("com.dev.survivallog.exiledtoolkit", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.fieldguide";
    private const string Version = "0.2.1";

    public override void Load()
    {
        var log = new GuideLog(Log, Path.Combine(Paths.BepInExRootPath, "field-guide.log"));
        var harmony = new Harmony(Guid);
        var offline = new OfflineCopy(Path.Combine(Paths.BepInExRootPath, "FieldGuide"), log.Write);
        var session = new GuideSession(new WebPage(log.Write), offline, new LocalServer(offline.SiteFolder, log.Write), new GuideSettings(Config), log.Write);

        OptionalUiSize.Bind(Config);

        log.Write($"===== Field Guide {Version} started");
        InputGate.Apply(harmony, log.Write);
        PageMessages.Apply(harmony, log.Write);

        GuideTicker.Session = session;
        GuideTicker.Log = log.Write;
        AddComponent<GuideTicker>();
    }
}
