using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FieldGuide.Core;

/// <summary>
/// The Survival Codex names most of its entries by an id the site has no page for: a dish by its recipe, a plant by
/// its plant id, a craft by its recipe, a decor style by its style id. Each of those has an item the site does know,
/// so the page script gets one map per such tab from that id to the item, read from the game's tables. Food, Prey and
/// Furniture entries are already items or furniture and need no map.
/// </summary>
public static class CodexPages
{
    // The Codex page's tab index for each mapped category (CodexCategory minus 1).
    public const int Dish = 1;
    public const int Plant = 2;
    public const int Craft = 4;
    public const int Decor = 6;

    /// <summary>{"tab":{"entry id":item id}}, leaving out every entry with no item.</summary>
    public static string Json(IReadOnlyDictionary<int, IReadOnlyDictionary<int, int>> itemsByTab)
    {
        var json = new StringBuilder("{");
        var isFirstTab = true;

        foreach (var tab in itemsByTab)
        {
            json.Append(isFirstTab ? "" : ",").Append('"').Append(Number(tab.Key)).Append("\":{");
            isFirstTab = false;

            var isFirst = true;

            foreach (var entry in tab.Value)
            {
                if (entry.Value <= 0) continue;

                json.Append(isFirst ? "" : ",").Append('"').Append(Number(entry.Key)).Append("\":").Append(Number(entry.Value));
                isFirst = false;
            }

            json.Append('}');
        }

        return json.Append('}').ToString();
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
