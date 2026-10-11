namespace Challenge999Tracker.Framework;

/// <summary>
///   A specific placed chest a player has individually tagged as dedicated 999-challenge storage, identified by
///   where it sits (location + tile) rather than any in-game name - vanilla has no way to rename a chest, so
///   this is the only stable identity available without requiring a chest-naming mod.
/// </summary>
public sealed class TrackedChestRef
{
  public string Location { get; set; } = "";
  public int X { get; set; }
  public int Y { get; set; }
}
