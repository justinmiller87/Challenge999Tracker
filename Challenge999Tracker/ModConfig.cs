using System.Collections.Generic;
using StardewModdingAPI;

namespace Challenge999Tracker;

public sealed class ModConfig
{
  public SButton OpenTrackerKey { get; set; } = SButton.U;

  /// <summary>Last All/Incomplete/Complete filter choice per category tab, keyed by sheet name.</summary>
  public Dictionary<string, string> ChecklistVisibilityBySheet { get; set; } = new();

  /// <summary>Whether each sheet counts toward the Totals dashboard and grand total; a sheet missing from this dictionary is enabled (the default for every sheet).</summary>
  public Dictionary<string, bool> SheetEnabled { get; set; } = new();

  /// <summary>If true, Have/999 counts an item at any quality instead of requiring its own highest-reachable quality (e.g. iridium-aged Wine) - for players who don't want to quality-grind everything.</summary>
  public bool IgnoreQuality { get; set; }

  /// <summary>Excludes the five one-time-catch legendary fish's Roe/Aged Roe rows from Required/Total - for players who caught (and didn't keep) one before they started tracking, so a Fish Pond can never breed it. See <see cref="Framework.OptionalItemSets.LegendaryFishRoe" />.</summary>
  public bool ExcludeLegendaryFishRoe { get; set; }

  /// <summary>Excludes the three Qi Fruit artisan goods (Jelly/Wine/Dried) from Required/Total - Qi Fruit only grows from the seasonal, limited-time Qi's Crop quest beans, so a player who didn't stockpile 999 beans before that quest ended can never make any more. See <see cref="Framework.OptionalItemSets.QiFruitArtisanGoods" />.</summary>
  public bool ExcludeQiFruitArtisanGoods { get; set; }
}
