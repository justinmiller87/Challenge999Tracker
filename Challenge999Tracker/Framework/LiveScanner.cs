using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace Challenge999Tracker.Framework;

/// <summary>
///   Live, in-game port of tracker999.py's held_items()/collect(): sums everything the player (and any
///   farmhands) hold, by (id, name), across inventories, chests, fridges and Junimo chests.
///   <para>
///   This walks an explicit, scoped set of sources rather than <see cref="Utility.ForEachItem" /> - that method
///   looks tempting (it already recurses into chests, including nested ones) but it walks literally every
///   <see cref="Item" />-shaped thing anywhere in the game world, including static map scenery that's part of
///   a location's default content and was never placed or owned by any player (e.g. the permanent lit campfire
///   near Linus's tent, present on every save's Mountain location). Counting that as "1 held Campfire" is
///   wrong, so only things actually inside a container or a player's inventory count here - nothing just
///   sitting in the world otherwise, even if it happens to match a checklist item's name/ID.
///   </para>
/// </summary>
public static class LiveScanner
{
  /// <summary>Equipment-only categories to exclude: hats, rings, boots, weapons, tools, clothing.</summary>
  private static readonly HashSet<int> ExcludedCategories = new() { -95, -96, -97, -98, -99, -100 };

  /// <param name="scanOnlyDedicatedStorage">
  ///   If true, only count farmer inventories, chests/fridges inside a <paramref name="trackedLocations" />
  ///   location, and individually tagged <paramref name="trackedChests" /> - skipping every other chest/fridge
  ///   and Junimo chests entirely (a Junimo chest is one pool shared across the whole farm, so there's no single
  ///   placed instance to tag). If false, scans everywhere, same as always.
  /// </param>
  public static Dictionary<(string Id, string Name), StockEntry> Collect(
    bool scanOnlyDedicatedStorage = false,
    IReadOnlyCollection<string>? trackedLocations = null,
    IReadOnlyList<TrackedChestRef>? trackedChests = null)
  {
    trackedLocations ??= Array.Empty<string>();
    trackedChests ??= Array.Empty<TrackedChestRef>();
    var stock = new Dictionary<(string Id, string Name), StockEntry>();

    void Add(Item item)
    {
      var category = item.Category;
      if (ExcludedCategories.Contains(category))
      {
        return;
      }

      var name = item.Name ?? "";
      var id = item.ItemId ?? "";
      if (item is StardewValley.Object obj && obj.bigCraftable.Value)
      {
        id = "(BC)" + id;
      }

      var key = (id, name);
      if (!stock.TryGetValue(key, out var entry))
      {
        entry = new StockEntry(id, name, category);
        stock[key] = entry;
      }

      var quality = item.Quality;
      var stack = item.Stack;
      entry.Quality[quality] = entry.Quality.GetValueOrDefault(quality) + stack;

      if (item is ColoredObject colored)
      {
        var rgb = colored.color.Value;
        entry.Colors[rgb] = entry.Colors.GetValueOrDefault(rgb) + stack;
      }
    }

    void AddContainer(IEnumerable<Item?> items)
    {
      foreach (var item in items)
      {
        if (item is not null)
        {
          Add(item);
        }
      }
    }

    // Every farmer's (and offline farmhand's) own inventory.
    foreach (var farmer in Game1.getAllFarmers())
    {
      AddContainer(farmer.Items);
    }

    // Every placed chest (including one nested inside another object, e.g. an Auto-Grabber's internal output
    // chest) and every location's fridge - includeInteriors covers farm building interiors (Barns/Coops/Sheds)
    // too, so there's no separate Building.GetIndoors() walk needed.
    Utility.ForEachLocation(location =>
    {
      // A whole tagged location counts everything in it, same as the "scan everywhere" default - only a
      // specific chest tag is checked per-chest when the location itself isn't tagged.
      var wholeLocationCounts = !scanOnlyDedicatedStorage || trackedLocations.Contains(location.NameOrUniqueName);

      foreach (var placed in location.Objects.Values)
      {
        if (placed is Chest chest)
        {
          if (wholeLocationCounts || IsChestTagged(location, chest, trackedChests))
          {
            AddContainer(chest.Items);
          }
        }
        else if (placed.heldObject.Value is Chest heldChest)
        {
          // A chest nested inside another object (e.g. an Auto-Grabber) has no tile position of its own to
          // tag individually - it only counts when the whole location does.
          if (wholeLocationCounts)
          {
            AddContainer(heldChest.Items);
          }
        }
        // Anything else placed in the world (decoration, machines not holding a chest, etc.) is scenery, not
        // storage - it's never counted as "1 of itself" here, which is exactly the bug this fixes.
      }

      if (location is FarmHouse farmHouse && wholeLocationCounts) // covers IslandFarmHouse too, since it subclasses FarmHouse
      {
        AddContainer(farmHouse.fridge.Value.Items);
      }

      return true;
    }, includeInteriors: true, includeGenerated: false);

    // Junimo chests specifically - NOT the whole globalInventories dictionary, which also holds the crow
    // shop's "LostItemsShop" entry (items sold away, not held). Skipped entirely in dedicated-storage mode:
    // every placed Junimo chest shares one pool across the whole farm, so there's no single instance to tag.
    if (!scanOnlyDedicatedStorage
        && Game1.player.team.globalInventories.TryGetValue(FarmerTeam.GlobalInventoryId_JunimoChest, out var junimoInventory))
    {
      AddContainer(junimoInventory);
    }

    return stock;
  }

  private static bool IsChestTagged(GameLocation location, Chest chest, IReadOnlyList<TrackedChestRef> trackedChests)
  {
    var tile = chest.TileLocation;
    return trackedChests.Any(t => t.Location == location.NameOrUniqueName && t.X == (int)tile.X && t.Y == (int)tile.Y);
  }
}
