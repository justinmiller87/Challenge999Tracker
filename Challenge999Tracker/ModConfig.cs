using System.Collections.Generic;
using StardewModdingAPI;

namespace Challenge999Tracker;

public sealed class ModConfig
{
  public SButton OpenTrackerKey { get; set; } = SButton.U;

  /// <summary>Last All/Incomplete/Complete filter choice per category tab, keyed by sheet name.</summary>
  public Dictionary<string, string> ChecklistVisibilityBySheet { get; set; } = new();
}
