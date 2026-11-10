using System;
using ExiledToolkit.Game;
using FieldGuide.Core;
using GameCore.HotUpdate.ReduxUI;
using UnityEngine;

namespace FieldGuide.Adapters;

/// <summary>
/// Sends clicks over the window to the web UI, and notes whether the pointer is over it. The game only passes a click to
/// the web UI where one of its own pages reported a button, which the window is not. While a drag or resize runs every
/// click goes to the page, so the pointer can leave the window without dropping it. The game's hotkeys are held through
/// Exiled's Toolkit (HotkeyGate). Static because a Harmony patch has nowhere else to look; main thread only, like the
/// raycasts.
/// </summary>
internal static class InputGate
{
    private const string CheckingClick = "checking a click against the Field Guide window, the game decides where it goes";

    private static NormalRect? _area;
    private static bool _isDragging;
    private static PatchKit? _patches;

    /// <summary>Whether the last pointer check found the pointer over the window. Kept until the next check says otherwise.</summary>
    public static bool IsPointerOver { get; private set; }

    public static void Apply(PatchKit patches)
    {
        _patches = patches;
        patches.Patch(typeof(InputGate), typeof(WebUILayer), "ValidateRaycast", postfix: nameof(RouteClicksToWindow), done: "clicks over the Field Guide go to it", consequence: "clicks on the Field Guide window will fall through to the game");
    }

    /// <summary>Where the window is, or null when it is closed or the page stopped saying.</summary>
    public static void SetArea(NormalRect? area)
    {
        _area = area;

        if (area == null) IsPointerOver = false;
    }

    public static void SetDragging(bool isDragging) => _isDragging = isDragging;

    /// <summary>Fails soft: any error leaves the game's answer standing.</summary>
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
            _patches?.Fail(CheckingClick, ex);
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
}
