using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using StardewValley;

namespace Challenge999Tracker.Framework;

/// <summary>Combines checklist rows with live stock, port of tracker999.py's main() matching loop, plus the live cross-sheet Required/Total from RequirementGraph.</summary>
public static class ProgressCalculator
{
  private static readonly Dictionary<int, string> QualityNames = new() { [0] = "normal", [1] = "silver", [2] = "gold", [4] = "iridium" };
  private static readonly Regex RawIdPattern = new(@"^(?:\(BC\))?\d+$", RegexOptions.Compiled);

  private sealed record RawFacts(int? Have, string Target, int Normal, int Silver, int Gold, int Iridium, string Matched, string? IconId);

  public static List<ProgressEntry> Calculate(
    ChecklistDocument document,
    RequirementGraph graph,
    IReadOnlyDictionary<(string Id, string Name), StockEntry> stock,
    IReadOnlyDictionary<string, List<StaticItemIndex.Entry>> staticIndex)
  {
    var byId = new Dictionary<string, List<StockEntry>>();
    var byName = new Dictionary<string, List<StockEntry>>();
    foreach (var entry in stock.Values)
    {
      (byId.TryGetValue(entry.Id, out var idList) ? idList : byId[entry.Id] = new List<StockEntry>()).Add(entry);
      var nameKey = ItemMatching.Norm(entry.Name);
      (byName.TryGetValue(nameKey, out var nameList) ? nameList : byName[nameKey] = new List<StockEntry>()).Add(entry);
    }

    // Flower sheet: colored variants of one flower, split by nearest color name.
    var colorRows = new Dictionary<string, List<string>>();
    foreach (var row in document.Rows)
    {
      if (row.Sheet != "Flowers")
      {
        continue;
      }

      var m = ItemMatching.MatchParenSuffix(row.Label);
      if (m.Success)
      {
        (colorRows.TryGetValue(m.Groups[1].Value, out var list) ? list : colorRows[m.Groups[1].Value] = new List<string>())
          .Add(m.Groups[2].Value);
      }
    }

    // Pass 1: live Have/quality/matched facts per row (unchanged matching logic).
    var facts = new Dictionary<(string Sheet, string Label), RawFacts>();
    foreach (var row in document.Rows)
    {
      List<StockEntry> matched = new();
      foreach (var cand in ItemMatching.Candidates(row.Label))
      {
        if (byId.TryGetValue(cand, out var foundById))
        {
          matched = foundById;
          break;
        }

        if (byName.TryGetValue(ItemMatching.Norm(cand), out var foundByName))
        {
          matched = foundByName;
          break;
        }
      }

      var q = new Dictionary<int, int>();
      var mq = 0;
      var flower = row.Sheet == "Flowers" ? ItemMatching.MatchParenSuffix(row.Label) : null;
      var isUnknownOverride = ItemMatching.Overrides.TryGetValue(row.Label, out var overrideValues) && overrideValues.Length == 0;
      var unknown = (flower is { Success: true } && flower.Groups[2].Value == "Cart") || isUnknownOverride;

      foreach (var e in matched)
      {
        mq = Math.Max(mq, ItemMatching.MaxQuality(e.Name, e.Category));
        if (flower is { Success: true } && e.Colors.Count > 0)
        {
          var baseLabel = flower.Groups[1].Value;
          var colorName = flower.Groups[2].Value;
          var namesForBase = colorRows.GetValueOrDefault(baseLabel, new List<string>());
          var share = e.Colors.Where(kv => ItemMatching.NearestColor(kv.Key, namesForBase) == colorName).Sum(kv => kv.Value);
          var total = e.Quality.Values.Sum();
          foreach (var (quality, count) in e.Quality)
          {
            q[quality] = q.GetValueOrDefault(quality) + (total > 0 ? (int)Math.Round(count * (double)share / total) : 0);
          }
        }
        else
        {
          foreach (var (quality, count) in e.Quality)
          {
            q[quality] = q.GetValueOrDefault(quality) + count;
          }
        }
      }

      int? have = unknown ? null : mq != 0 ? q.GetValueOrDefault(mq) : q.Values.Sum();
      var target = matched.Count > 0 ? (mq != 0 ? QualityNames[mq] : "any") : "";
      var matchedNames = string.Join("; ", matched.Select(e => e.Name).Distinct().OrderBy(n => n, StringComparer.Ordinal));
      var iconId = ResolveIcon(row.Label, matched, staticIndex);

      facts[(row.Sheet, row.Label)] = new RawFacts(have, target, q.GetValueOrDefault(0), q.GetValueOrDefault(1), q.GetValueOrDefault(2), q.GetValueOrDefault(4), matchedNames, iconId);
    }

    // Pass 2: live cross-sheet Total, one shared pass over the whole requirement graph.
    var haveByKey = facts.ToDictionary(kv => kv.Key, kv => (double)(kv.Value.Have ?? 0));
    var totals = graph.ComputeAllTotals(haveByKey);

    var result = new List<ProgressEntry>(document.Rows.Count);
    foreach (var row in document.Rows)
    {
      var f = facts[(row.Sheet, row.Label)];
      var required = graph.Required(row);
      var total = totals.GetValueOrDefault((row.Sheet, row.Label));
      var breakdown = graph.DescribeBreakdown(row);
      int? toGo = f.Have is null ? null : Math.Max(0, 999 - f.Have.Value);

      result.Add(new ProgressEntry(
        row.Sheet, row.Label, required, total, breakdown, f.Target, f.Have, toGo,
        f.Normal, f.Silver, f.Gold, f.Iridium, f.Matched, f.IconId));
    }

    return result;
  }

  private static string QualifiedId(string id) => id.StartsWith("(BC)") ? id : "(O)" + id;

  /// <summary>
  ///   Picks an item ID to draw an icon for: prefer a currently-held match (exactly what the row is counting),
  ///   otherwise fall back to the static catalog (see <see cref="StaticItemIndex" /> for its "flavored items"
  ///   caveat) so incomplete rows still show something other than a blank space.
  /// </summary>
  private static string? ResolveIcon(string label, List<StockEntry> matched, IReadOnlyDictionary<string, List<StaticItemIndex.Entry>> staticIndex)
  {
    if (matched.Count > 0)
    {
      return QualifiedId(matched[0].Id);
    }

    foreach (var cand in ItemMatching.Candidates(label))
    {
      if (RawIdPattern.IsMatch(cand))
      {
        var bareId = cand.StartsWith("(BC)") ? cand["(BC)".Length..] : cand;
        var isBigCraftable = cand.StartsWith("(BC)");
        if (isBigCraftable ? Game1.bigCraftableData.ContainsKey(bareId) : Game1.objectData.ContainsKey(bareId))
        {
          return QualifiedId(cand);
        }

        continue;
      }

      if (staticIndex.TryGetValue(ItemMatching.Norm(cand), out var hits) && hits.Count > 0)
      {
        return QualifiedId(hits[0].Id);
      }
    }

    return null;
  }

  /// <summary>Per-sheet rollup for the Totals dashboard page, plus a grand-total row across all sheets.</summary>
  public static TotalsPage ComputeTotalsPage(IReadOnlyList<string> sheetOrder, List<ProgressEntry> entries)
  {
    var bySheet = entries.GroupBy(e => e.Sheet).ToDictionary(g => g.Key, g => g.ToList());
    var grandRequired = entries.Sum(e => e.Required);

    var sheets = new List<SheetTotals>();
    foreach (var sheet in sheetOrder)
    {
      if (!bySheet.TryGetValue(sheet, out var rows))
      {
        continue;
      }

      var requiredSum = rows.Sum(r => r.Required);
      var totalSum = rows.Sum(r => r.Total);
      sheets.Add(new SheetTotals(
        sheet, rows.Count, requiredSum, totalSum,
        requiredSum > 0 ? totalSum / requiredSum * 100 : 0,
        grandRequired > 0 ? requiredSum / grandRequired * 100 : 0,
        requiredSum <= totalSum));
    }

    var grandTotalSum = entries.Sum(e => e.Total);
    var grandTotal = new SheetTotals(
      "The 999 Challenge", entries.Count, grandRequired, grandTotalSum,
      grandRequired > 0 ? grandTotalSum / grandRequired * 100 : 0, 100, grandRequired <= grandTotalSum);

    return new TotalsPage(sheets, grandTotal);
  }
}
