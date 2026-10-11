using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;

namespace Challenge999Tracker.Framework;

/// <summary>
///   Checklist-label-to-game-item matching rules, ported from tracker999.py (OVERRIDES, candidates(), norm(),
///   max_quality() and their supporting sets). Keep this in sync with tracker999.py by hand if the checklist's
///   matching needs change - there's no shared source of truth between the Python tool and this mod.
/// </summary>
public static class ItemMatching
{
  /// <summary>
  ///   Checklist label -> item ID(s) or exact game name(s), where the label can't be derived from the game name
  ///   by the generic rules in Candidates(). Big craftables are prefixed "(BC)" since their IDs overlap with
  ///   objects'. An empty array means "the game can't tell this item apart from another source" (shown as unknown).
  /// </summary>
  public static readonly Dictionary<string, string[]> Overrides = new()
  {
    ["Egg: White"] = new[] { "176" }, ["Egg: Brown"] = new[] { "180" },
    ["Egg: White, Large"] = new[] { "174" }, ["Egg: Brown, Large"] = new[] { "182" },
    ["Egg: Duck"] = new[] { "442" }, ["Egg: Dinosaur"] = new[] { "107" }, ["Egg: Void"] = new[] { "305" },
    ["Egg: Ostrich"] = new[] { "289" }, ["Egg: Golden"] = new[] { "928" },
    ["Milk: Cow"] = new[] { "184" }, ["Milk: Cow, Large"] = new[] { "186" },
    ["Milk: Goat"] = new[] { "436" }, ["Milk: Goat, Large"] = new[] { "438" },
    ["Mayonnaise"] = new[] { "306" }, ["Mayonnaise: Duck"] = new[] { "447" },
    ["Mayonnaise: Dino"] = new[] { "807" }, ["Mayonnaise: Void"] = new[] { "308" },
    ["Honey: Wild"] = new[] { "Wild Honey" }, ["Loom: Cloth"] = new[] { "Cloth" },
    ["Aged Caviar"] = new[] { "Caviar" }, ["Tree: Mushroom"] = new[] { "Mushroom Tree Seed" },
    ["Tree: Mossy Seed"] = new[] { "Mossy Seed" }, ["Tree: Mystic Seed"] = new[] { "Mystic Tree Seed" },
    ["Tree: Tea Sapling"] = new[] { "Tea Sapling" },
    ["Cranberries seeds"] = new[] { "Cranberry Seeds" }, ["Coffee Bean"] = new[] { "Coffee Bean" },
    ["Wild Seeds: Spring"] = new[] { "Spring Seeds" }, ["Wild Seeds: Summer"] = new[] { "Summer Seeds" },
    ["Wild Seeds: Fall"] = new[] { "Fall Seeds" }, ["Wild Seeds: Winter"] = new[] { "Winter Seeds" },
    ["Jelly: Cave"] = new[] { "Cave Jelly" }, ["Jelly: River"] = new[] { "River Jelly" }, ["Jelly: Sea"] = new[] { "Sea Jelly" },
    ["Bomb: Bomb"] = new[] { "Bomb" }, ["Bomb: Explosive Ammo"] = new[] { "Explosive Ammo" },
    ["Totem: Rain"] = new[] { "Rain Totem" }, ["Totem: Treasure"] = new[] { "Treasure Totem" },
    ["Totem: Beach"] = new[] { "Warp Totem: Beach" }, ["Totem: Desert"] = new[] { "Warp Totem: Desert" },
    ["Totem: Farm"] = new[] { "Warp Totem: Farm" }, ["Totem: Island"] = new[] { "Warp Totem: Island" },
    ["Totem: Mountains"] = new[] { "Warp Totem: Mountains" },
    ["Firework: Green"] = new[] { "Fireworks (Green)" }, ["Firework: Purple"] = new[] { "Fireworks (Purple)" },
    ["Firework: Red"] = new[] { "Fireworks (Red)" },
    ["Sprinkler: Basic"] = new[] { "Sprinkler" }, ["Fence: Gate"] = new[] { "Gate" },
    ["Bouquet: Wilted"] = new[] { "Wilted Bouquet" },
    ["Golden Coconuts"] = new[] { "Golden Coconut" }, ["Star Shards"] = new[] { "Star Shards" },
    ["Strange Doll(Green)"] = new[] { "126" }, ["Strange Doll(Pink)"] = new[] { "127" },
    ["Rotten Plant 1"] = new[] { "747" }, ["Rotten Plant 2"] = new[] { "748" },
    ["Rarecrow: 1"] = new[] { "(BC)110" }, ["Rarecrow: 2"] = new[] { "(BC)113" }, ["Rarecrow: 3"] = new[] { "(BC)126" },
    ["Rarecrow: 4"] = new[] { "(BC)136" }, ["Rarecrow: 5"] = new[] { "(BC)137" }, ["Rarecrow: 6"] = new[] { "(BC)138" },
    ["Rarecrow: 7"] = new[] { "(BC)139" }, ["Rarecrow: 8"] = new[] { "(BC)140" },
    // Two distinct big-craftables that happen to share a display name ("Stone Owl") - one bought from the
    // Night Market, one that randomly spawns on the farm. Matched by ID instead of name so each is counted
    // separately rather than one bucket silently summing both.
    ["Stone Owl"] = new[] { "(BC)54" }, ["Stone Owl(Spawn)"] = new[] { "(BC)95" },
  };

  /// <summary>Crab-pot-only catches never have quality; with Deluxe Bait they top out at silver, and smoking keeps that.</summary>
  public static readonly HashSet<string> CrabPotOnly = new() { "Crab", "Crayfish", "Lobster", "Periwinkle", "Shrimp", "Snail" };

  /// <summary>Beach forage: iridium with Botanist even though the category isn't forage.</summary>
  public static readonly HashSet<string> BeachForage = new() { "Coral", "Sea Urchin", "Nautilus Shell", "Rainbow Shell" };

  /// <summary>Artisan goods that can reach iridium: cask-aged, plus Mayonnaise from an iridium Ostrich Egg.</summary>
  public static readonly HashSet<string> IridiumArtisan = new() { "Cheese", "Goat Cheese", "Mead", "Pale Ale", "Beer", "Mayonnaise" };

  /// <summary>Categories whose items come in normal/silver/gold/iridium: fish, eggs, milk, truffle/sweet gem berry, animal goods, veggies, fruit, flowers, forage.</summary>
  public static readonly HashSet<int> QualityCategories = new() { -4, -5, -6, -17, -18, -75, -79, -80, -81 };

  public static readonly HashSet<string> NoQuality = new() { "Tea Leaves", "Sap" };

  /// <summary>Highest quality this item can reach; 0 means "has no quality".</summary>
  public static int MaxQuality(string name, int category)
  {
    if (NoQuality.Contains(name))
    {
      return 0;
    }

    var withoutSmoked = name.StartsWith("Smoked ") ? name["Smoked ".Length..] : name;
    if (CrabPotOnly.Contains(name) || CrabPotOnly.Contains(withoutSmoked))
    {
      return 1;
    }

    // Mayonnaise can be iridium (from an iridium Ostrich Egg); Duck/Void/Dino Mayonnaise and Honey have no
    // quality and fall through to 0.
    if (QualityCategories.Contains(category) || BeachForage.Contains(name) || IridiumArtisan.Contains(name))
    {
      return 4;
    }

    if (name.EndsWith(" Wine") || name == "Wine" || name.StartsWith("Smoked "))
    {
      return 4;
    }

    return 0;
  }

  private static readonly Regex PrefixPattern = new(@"^(.+?):\s*(.+)$", RegexOptions.Compiled);
  private static readonly Regex ParenPattern = new(@"^(.+?)\s*\((.+)\)$", RegexOptions.Compiled);

  private static readonly Dictionary<string, string[]> PrefixFallbacks = new()
  {
    ["Pickles"] = new[] { "Pickled {b}" },
    ["Mushroom"] = new[] { "{b} Mushroom", "{b}" },
    ["Tree"] = new[] { "{b}" },
    ["Floor"] = new[] { "{b}", "{b} Floor" },
    ["Slime Egg"] = new[] { "{b} Slime Egg" },
  };

  /// <summary>Game names a checklist label might refer to.</summary>
  public static List<string> Candidates(string label)
  {
    if (Overrides.TryGetValue(label, out var overridden))
    {
      return overridden.ToList();
    }

    var outList = new List<string> { label };
    var m = PrefixPattern.Match(label);
    if (m.Success)
    {
      var a = m.Groups[1].Value;
      var b = m.Groups[2].Value;
      // Only prefixes listed here may fall back to the bare name, so that "Wine: Salmonberry" never matches
      // plain Salmonberries.
      if (PrefixFallbacks.TryGetValue(a, out var templates))
      {
        outList.AddRange(templates.Select(t => t.Replace("{b}", b)));
      }

      // "Tree: Orange Seeds" is the Orange Sapling
      if (a == "Tree" && b.EndsWith(" Seeds"))
      {
        outList.Add(b[..^" Seeds".Length] + " Sapling");
      }

      outList.Add($"{b} {a}");
      outList.Add($"{a} {b}");
    }

    var paren = ParenPattern.Match(label); // "Tulip (Red)" -> Tulip, colored
    if (paren.Success)
    {
      outList.Add(paren.Groups[1].Value);
    }

    return outList;
  }

  /// <summary>Pluralization-aware name normalization, for matching labels to game item names.</summary>
  public static string Norm(string s)
  {
    var lowered = Regex.Replace(s.ToLowerInvariant().Replace("-", " "), "[^a-z0-9 ]", "");
    var words = new List<string>();
    foreach (var w in lowered.Split(' ', StringSplitOptions.RemoveEmptyEntries))
    {
      if (w.EndsWith("ies"))
      {
        words.Add(w[..^3] + "y");
      }
      else if (w.EndsWith("s") && !w.EndsWith("ss"))
      {
        words.Add(w[..^1]);
      }
      else
      {
        words.Add(w);
      }
    }

    return string.Join(" ", words);
  }

  /// <summary>Approximate colors for the flower color variants on the Flowers sheet.</summary>
  public static readonly Dictionary<string, Color> ColorRgb = new()
  {
    ["Indigo"] = new Color(75, 0, 130), ["Periwinkle"] = new Color(140, 140, 240), ["Dark Blue"] = new Color(20, 40, 160),
    ["Blue"] = new Color(40, 90, 255), ["Light Blue"] = new Color(150, 200, 255), ["White"] = new Color(245, 245, 245),
    ["Salmon"] = new Color(250, 128, 114), ["Lavender"] = new Color(200, 160, 240), ["Purple"] = new Color(140, 40, 200),
    ["Cyan"] = new Color(0, 220, 230), ["Red"] = new Color(220, 30, 30), ["Yellow"] = new Color(250, 220, 40),
    ["Pink"] = new Color(255, 150, 190), ["Lilac"] = new Color(200, 150, 220),
  };

  public static string? NearestColor(Color rgb, IEnumerable<string> names)
  {
    var known = names.Where(ColorRgb.ContainsKey).ToList();
    if (known.Count == 0)
    {
      return null;
    }

    return known.MinBy(n =>
    {
      var c = ColorRgb[n];
      int dr = rgb.R - c.R, dg = rgb.G - c.G, db = rgb.B - c.B;
      return dr * dr + dg * dg + db * db;
    });
  }

  /// <summary>Matches a checklist label's trailing "(Color)"/"(Variant)" suffix, e.g. "Tulip (Red)".</summary>
  public static Match MatchParenSuffix(string label) => ParenPattern.Match(label);
}
