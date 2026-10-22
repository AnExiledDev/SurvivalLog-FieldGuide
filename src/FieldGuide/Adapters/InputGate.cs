using System;
using FieldGuide.Core;
using GameCore.HotUpdate.Battle.Show;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using UnityEngine;

namespace FieldGuide.Adapters;

/// <summary>
/// Sends clicks over the window to the web UI, and notes whether the pointer is over it. The game only passes a click to
/// the web UI where one of its own pages reported a button, which the window is not. While a drag or resize runs every
/// click goes to the page, so the pointer can leave the window without dropping it. Also stops the game's hotkeys while
/// the session says so. Static because a Harmony patch has nowhere else to look; main thread only, like the raycasts.
/// </summary>
internal static class InputGate
{
    private static NormalRect? _area;
    private static bool _isDragging;
    private static bool _isBlockingHotkeys;
    private static int _blockedThroughFrame = -1;
    private static bool _hasReportedFailure;
    private static Action<string> _log = _ => { };

    /// <summary>Whether the last pointer check found the pointer over the window. Kept until the next check says otherwise.</summary>
    public static bool IsPointerOver { get; private set; }

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;
        Patch(harmony, typeof(HotKeyManager), "Update", prefix: nameof(SkipWhileBlocked), done: "hotkeys are blocked while typing in the Field Guide", consequence: "keys typed in the Field Guide will also reach the game");
        Patch(harmony, typeof(WebUILayer), "ValidateRaycast", postfix: nameof(RouteClicksToWindow), done: "clicks over the Field Guide go to it", consequence: "clicks on the Field Guide window will fall through to the game");
    }

    /// <summary>Where the window is, or null when it is closed or the page stopped saying.</summary>
    public static void SetArea(NormalRect? area)
    {
        _area = area;

        if (area == null) IsPointerOver = false;
    }

    public static void SetDragging(bool isDragging) => _isDragging = isDragging;

    /// <summary>On a change to off, hotkeys stay blocked through the next frame so the key that ended the block is not seen by the game too.</summary>
    public static void BlockHotkeys(bool isBlocking)
    {
        if (_isBlockingHotkeys && !isBlocking) _blockedThroughFrame = Time.frameCount + 1;

        _isBlockingHotkeys = isBlocking;
    }

    private static void Patch(Harmony harmony, Type type, string method, string? prefix = null, string? postfix = null, string done = "", string consequence = "")
    {
        try
        {
            harmony.Patch(
                AccessTools.Method(type, method),
                prefix: prefix == null ? null : new HarmonyMethod(typeof(InputGate), prefix),
                postfix: postfix == null ? null : new HarmonyMethod(typeof(InputGate), postfix));
            _log(done);
        }
        catch (Exception ex)
        {
            _log($"could not hook {type.Name}.{method}, {consequence}: {ex.Message}");
        }
    }

    private static bool SkipWhileBlocked() => !(_isBlockingHotkeys || Time.frameCount <= _blockedThroughFrame);

    /// <summary>Fails soft: any error leaves the game's own answer standing.</summary>
    private static void RouteClicksToWindow(WebUILayer __instance, Vector2 screenPoint, Camera eventCamera, ref bool __result)
    {
        if (_isDragging)
        {
            __result = true;

            return;
        }

        if (_area is not { } area) return;

        try
        {
            IsPointerOver = IsOver(__instance, screenPoint, eventCamera, area);

            if (IsPointerOver) __result = true;
        }
        catch (Exception ex)
        {
            ReportFailureOnce(ex);
        }
    }

    private static bool IsOver(WebUILayer layer, Vector2 screenPoint, Camera eventCamera, NormalRect area)
    {
        var rect = layer.cachedStaticMask?.Rect;

        if (rect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, eventCamera, out var local)) return false;

        var size = rect.rect;
        var pivot = rect.pivot;

        return area.ContainsLocalPoint(local.x, local.y, size.width, size.height, pivot.x, pivot.y);
    }

    private static void ReportFailureOnce(Exception ex)
    {
        if (_hasReportedFailure) return;

        _hasReportedFailure = true;
        _log("checking a click against the Field Guide window failed (reported once), the game decides where it goes: " + ex.Message);
    }
}
