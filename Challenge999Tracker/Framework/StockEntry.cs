using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Challenge999Tracker.Framework;

/// <summary>Everything held of one distinct (id, name) item, bucketed by quality and (for flowers) color. Port of tracker999.py's collect() entries.</summary>
public sealed class StockEntry
{
  public StockEntry(string id, string name, int category)
  {
    Id = id;
    Name = name;
    Category = category;
  }

  public string Id { get; }
  public string Name { get; }
  public int Category { get; }
  public Dictionary<int, int> Quality { get; } = new();
  public Dictionary<Color, int> Colors { get; } = new();
}
