using System.Collections.Generic;

namespace Challenge999Tracker.Framework;

/// <summary>
///   Checklist rows a player can be permanently locked out of depending on play history before they started
///   tracking, or that are impractical for most players to reach 999 of - GMCM lets them exclude these from
///   their own Required/Total math instead of being stuck under 100% forever.
/// </summary>
public static class OptionalItemSets
{
  /// <summary>
  ///   Roe/Aged Roe from the five one-time-catch legendary fish (Crimsonfish, Angler, Legend, Glacierfish,
  ///   Mutant Carp) - a Fish Pond needs a live specimen to breed from, so catching (and not keeping) one of
  ///   these before ever intending to pond-breed it permanently forecloses its Roe/Aged Roe rows. The
  ///   second-generation Qi board fish (Son of Crimsonfish, Ms. Angler, Legend II, Glacierfish Jr., Radioactive
  ///   Carp) are deliberately excluded from this set - Qi's board lets you catch those again indefinitely.
  /// </summary>
  public static readonly HashSet<(string Sheet, string Label)> LegendaryFishRoe = new()
  {
    ("Roe", "Crimsonfish Roe"), ("Roe", "Angler Roe"), ("Roe", "Legend Roe"),
    ("Roe", "Glacierfish Roe"), ("Roe", "Mutant Carp Roe"),
    ("Artisan Goods Jar", "Aged Crimsonfish Roe"), ("Artisan Goods Jar", "Aged Angler Roe"),
    ("Artisan Goods Jar", "Aged Legend Roe"), ("Artisan Goods Jar", "Aged Glacierfish Roe"),
    ("Artisan Goods Jar", "Aged Mutant Carp Roe"),
  };

  /// <summary>
  ///   The three artisan goods made from Qi Fruit (itself grown only from the seasonal, limited-time Qi's Crop
  ///   quest reward beans, which neither the fruit nor the beans survive past). Once processed into Jelly/
  ///   Wine/Dried, the goods themselves don't expire or disappear - but if a player missed or already used up
  ///   that quest's supply before stockpiling 999 beans, they can never make any more.
  /// </summary>
  public static readonly HashSet<(string Sheet, string Label)> QiFruitArtisanGoods = new()
  {
    ("Artisan Goods Jar", "Jelly: Qi Fruit"),
    ("Artisan Goods Keg", "Wine: Qi Fruit"),
    ("Artisan Goods Dehydrator", "Dried Qi Fruit"),
  };
}
