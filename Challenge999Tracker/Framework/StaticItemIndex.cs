using System.Collections.Generic;
using StardewValley;

namespace Challenge999Tracker.Framework;

/// <summary>
///   A reverse normalized-name -> item lookup built once from the game's own object/big-craftable catalogs
///   (<see cref="Game1.objectData" />/<see cref="Game1.bigCraftableData" />). tracker999.py can only resolve a
///   checklist label to a game item once you've actually held one (it only ever sees your save's current
///   stock); this index lets the tracker show the right icon and quality ceiling for a row before you've
///   collected a single one, for any item with a fixed catalog name.
///   <para>
///   This does NOT cover items whose name is generated at creation time from a base ingredient - wine, jelly,
///   juice, pickles, roe/aged roe, smoked fish, dried fruit, flavored honey and flavored bait (e.g. "Blackberry
///   Wine" doesn't exist as its own catalog entry; it's "Wine" + a preserved fruit). Those rows fall back to the
///   same "only known once held" behavior as tracker999.py until this index is extended to reconstruct that
///   naming - see ItemMatching's flavored-name handling notes.
///   </para>
/// </summary>
public static class StaticItemIndex
{
  public sealed record Entry(string Id, int Category, bool BigCraftable);

  public static Dictionary<string, List<Entry>> Build()
  {
    var byNormName = new Dictionary<string, List<Entry>>();

    void Add(string? name, Entry entry)
    {
      if (string.IsNullOrEmpty(name))
      {
        return;
      }

      var key = ItemMatching.Norm(name);
      if (!byNormName.TryGetValue(key, out var list))
      {
        byNormName[key] = list = new List<Entry>();
      }

      list.Add(entry);
    }

    foreach (var (id, data) in Game1.objectData)
    {
      Add(data.Name, new Entry(id, data.Category, false));
    }

    foreach (var (id, data) in Game1.bigCraftableData)
    {
      Add(data.Name, new Entry("(BC)" + id, 0, true));
    }

    return byNormName;
  }
}
