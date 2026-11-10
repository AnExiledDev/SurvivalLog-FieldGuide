using System;
using System.Collections.Generic;
using FieldGuide.Core;
using GameCore.HotUpdate;
using GameList = Il2CppSystem.Collections.Generic.List<int>;

namespace FieldGuide.Adapters;

/// <summary>
/// Reads, once, the item behind each Survival Codex entry that is not an item itself: a dish's item, a plant's first
/// harvest, a craft's first product, a decor style's item. These are the same items the Codex takes the entry's icon
/// from. Only read once the game has loaded its tables; nothing here changes the game.
/// </summary>
internal sealed class CodexItems
{
    private readonly Action<string> _log;
    private string? _json;
    private bool _hasFailed;

    public CodexItems(Action<string> log)
    {
        _log = log;
    }

    /// <summary>The maps as JSON, or null while the tables are not loaded (or reading them failed).</summary>
    public string? Json()
    {
        if (_json != null || _hasFailed) return _json;

        var config = GameSingleton.Existing<ConfigManager>();

        if (config?._Config_CookingRecipe_Dict is not { Count: > 0 } dishes || config._Config_Plant_Dict is not { } plants ||
            config._Config_ProductionList_Dict is not { Count: > 0 } crafts || config._Config_RenovationStyle_Dict is not { } styles)
            return null;

        try
        {
            var byTab = new Dictionary<int, IReadOnlyDictionary<int, int>>();
            var dishItems = new Dictionary<int, int>();
            var plantItems = new Dictionary<int, int>();
            var craftItems = new Dictionary<int, int>();
            var styleItems = new Dictionary<int, int>();

            foreach (var row in dishes.Values) dishItems[row.ID] = row.NormalItemID;
            foreach (var row in plants.Values) plantItems[row.ID] = First(row.Gain);
            foreach (var row in crafts.Values) craftItems[row.ID] = First(row.ProductID);
            foreach (var row in styles.Values) styleItems[row.ID] = row.ItemId;

            byTab[CodexPages.Dish] = dishItems;
            byTab[CodexPages.Plant] = plantItems;
            byTab[CodexPages.Craft] = craftItems;
            byTab[CodexPages.Decor] = styleItems;

            _json = CodexPages.Json(byTab);
            _log("read the Survival Codex's items: " + dishItems.Count + " dishes, " + plantItems.Count + " plants, " + craftItems.Count + " crafts, " + styleItems.Count + " decor styles");
        }
        catch (Exception ex)
        {
            _hasFailed = true;
            _log("could not read the Survival Codex's items, / over a dish, plant, craft or decor entry will not find its page: " + ex.Message);
        }

        return _json;
    }

    private static int First(GameList? list) => list is { Count: > 0 } ? list[0] : 0;
}
