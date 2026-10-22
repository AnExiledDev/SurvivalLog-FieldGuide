using System;
using System.IO;
using FieldGuide.Core;
using GameCore.HotUpdate.ReduxUI;

namespace FieldGuide.Adapters;

/// <summary>
/// The mod's half that lives in the game's web UI: page.js, run in the root document the game's own pages sit in. It
/// draws the window, puts the Field Guide button in the HUD's tool column, adds the app to the phone and reads what the
/// pointer is over. The web view can reload at any time and lose the script, so it is checked for every couple of
/// seconds and put back when the page says it is missing.
/// </summary>
internal sealed class WebPage
{
    private const float CheckSeconds = 2f;
    private const string ScriptResource = "FieldGuide.page.js";
    private const string NoScriptReply = "window.vuplex&&window.vuplex.postMessage('" + PageCommand.Prefix + "noscript')";

    private readonly Action<string> _log;
    private string? _script;
    private float _nextCheckAt;
    private bool _hasReportedRunFailure;

    public WebPage(Action<string> log)
    {
        _log = log;
    }

    public void KeepInstalled(float now)
    {
        if (now < _nextCheckAt) return;

        _nextCheckAt = now + CheckSeconds;
        Run("window.__slfieldguide||" + NoScriptReply);
    }

    public void Install()
    {
        if (Run(Script())) _log("page script sent to the web UI");
    }

    public bool Setup(string viewJson, bool isChinese) => Call($"setup({viewJson})", isChinese);

    public void Toggle(bool isChinese) => Call("toggle()", isChinese);

    /// <summary>Opens the page for what the pointer is over, or toggles the window when it is over nothing the site knows.</summary>
    public void Lookup(bool isChinese) => Call("lookup()", isChinese);

    /// <summary>Every call hands the page its words first, so a switch of the game's language shows at the next redraw.</summary>
    private bool Call(string expression, bool isChinese) =>
        Run($"window.__slfieldguideWords={Texts.Json(isChinese)};window.__slfieldguide?window.__slfieldguide.{expression}:{NoScriptReply}");

    private bool Run(string javaScript)
    {
        var webView = GameSingleton.Existing<ReduxUISystem>()?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;

        if (webView == null) return false;

        try
        {
            webView.ExecuteJavaScript(javaScript, null);

            return true;
        }
        catch (Exception ex)
        {
            if (!_hasReportedRunFailure) _log("could not run script in the web UI (reported once): " + ex.Message);

            _hasReportedRunFailure = true;

            return false;
        }
    }

    private string Script()
    {
        if (_script != null) return _script;

        using var stream = typeof(WebPage).Assembly.GetManifestResourceStream(ScriptResource) ?? throw new InvalidOperationException(ScriptResource + " is not embedded");
        using var reader = new StreamReader(stream);

        return _script = reader.ReadToEnd();
    }
}
