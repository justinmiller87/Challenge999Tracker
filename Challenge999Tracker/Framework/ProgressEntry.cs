using System.Collections.Generic;

namespace Challenge999Tracker.Framework;

/// <summary>One checklist row's live progress - the mod's equivalent of one line of tracker999.py's 999_progress.csv, plus the workbook's cross-sheet Required/Total.</summary>
public sealed class ProgressEntry
{
  public ProgressEntry(
    string sheet, string item, double required, double total, List<(string Label, double Contribution)> breakdown,
    string target, int? have, int? toGo, int normal, int silver, int gold, int iridium, string matched, string? iconQualifiedId)
  {
    Sheet = sheet;
    Item = item;
    Required = required;
    Total = total;
    Breakdown = breakdown;
    Target = target;
    Have = have;
    ToGo = toGo;
    Normal = normal;
    Silver = silver;
    Gold = gold;
    Iridium = iridium;
    Matched = matched;
    IconQualifiedId = iconQualifiedId;
  }

  public string Sheet { get; }
  public string Item { get; }

  /// <summary>The true, cross-sheet resource target (e.g. Wood's is 944,055, not a flat 999) - see RequirementGraph.</summary>
  public double Required { get; }

  /// <summary>The live, cross-sheet progress mirror of <see cref="Required" />, recomputed every time the tracker opens.</summary>
  public double Total { get; }

  /// <summary>One line per direct term of the Required expression, e.g. [("base", 999), ("Jelly: Ancient Fruit", 999), ...].</summary>
  public List<(string Label, double Contribution)> Breakdown { get; }

  /// <summary>Quality name ("normal"/"silver"/"gold"/"iridium") this item is tracked at, "any" if it has no quality, or "" if never matched to a game item.</summary>
  public string Target { get; }

  /// <summary>Live count at the tracked quality, or null if the game can't tell this item apart from another source (e.g. a flower cart purchase, a spawned Stone Owl).</summary>
  public int? Have { get; }

  public int? ToGo { get; }
  public int Normal { get; }
  public int Silver { get; }
  public int Gold { get; }
  public int Iridium { get; }

  /// <summary>Distinct matched game item name(s), semicolon-joined - same semantics as the CSV's "Matched" column.</summary>
  public string Matched { get; }

  /// <summary>A qualified item ID ("(O)176"/"(BC)110") to draw an icon for, or null if nothing could be resolved yet.</summary>
  public string? IconQualifiedId { get; }

  /// <summary>The workbook's own compound done rule: AND(Have&gt;=999, Total&gt;=Required).</summary>
  public bool IsComplete => Have is >= 999 && Total >= Required;
}
