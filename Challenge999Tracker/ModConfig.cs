using System.Collections.Generic;
using Challenge999Tracker.Framework;
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

  /// <summary>
  ///   If true (the default), the Cooking sheet's Have only counts a dish made with Qi Seasoning (the game
  ///   always crafts it at gold quality, the only way a cooked dish ever reaches that quality) - plain-cooked
  ///   dishes don't count. If false, any cooked dish counts regardless of quality. See
  ///   <see cref="Framework.ProgressCalculator" />.
  /// </summary>
  public bool RequireQiSeasoning { get; set; } = true;

  /// <summary>Excludes the five one-time-catch legendary fish's Roe/Aged Roe rows from Required/Total - for players who caught (and didn't keep) one before they started tracking, so a Fish Pond can never breed it. See <see cref="Framework.OptionalItemSets.LegendaryFishRoe" />.</summary>
  public bool ExcludeLegendaryFishRoe { get; set; }

  /// <summary>Excludes the three Qi Fruit artisan goods (Jelly/Wine/Dried) from Required/Total - Qi Fruit only grows from the seasonal, limited-time Qi's Crop quest beans, so a player who didn't stockpile 999 beans before that quest ended can never make any more. See <see cref="Framework.OptionalItemSets.QiFruitArtisanGoods" />.</summary>
  public bool ExcludeQiFruitArtisanGoods { get; set; }

  /// <summary>
  ///   If true, the live stock scan ignores everything except farmer inventories, chests/fridges inside a
  ///   tagged <see cref="TrackedLocations" /> location, and individually tagged <see cref="TrackedChests" /> -
  ///   for players who keep a dedicated building or dedicated chests for this challenge and don't want
  ///   incidental stock elsewhere on the farm counting. Off by default: scans everywhere, as before.
  /// </summary>
  public bool ScanOnlyDedicatedStorage { get; set; }

  /// <summary>Hotkey that tags/untags the player's current location as dedicated storage - every chest and fridge in it then counts as a whole, with no need to tag individual chests inside it.</summary>
  public SButton ToggleLocationTrackedKey { get; set; } = SButton.OemComma;

  /// <summary>Hotkey that tags/untags the chest currently open (its menu must be on screen) as dedicated storage - for a chest outside any tagged location.</summary>
  public SButton ToggleChestTrackedKey { get; set; } = SButton.OemPeriod;

  /// <summary>Locations tagged whole via <see cref="ToggleLocationTrackedKey" />, by <c>GameLocation.NameOrUniqueName</c> - vanilla has no way to rename a location, so this is only ever set by that hotkey, never typed in by hand.</summary>
  public HashSet<string> TrackedLocations { get; set; } = new();

  /// <summary>Individual chests tagged via <see cref="ToggleChestTrackedKey" />, for chests outside any tagged location.</summary>
  public List<TrackedChestRef> TrackedChests { get; set; } = new();
}
