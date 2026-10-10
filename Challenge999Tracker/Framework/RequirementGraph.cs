using System;
using System.Collections.Generic;
using System.Linq;

namespace Challenge999Tracker.Framework;

/// <summary>
///   Evaluates each checklist row's true, cross-sheet Required and live Total, per the workbook's own formula
///   network (now sourced from the bundled JSON's RequirementExpr trees instead of the .ods itself).
///   <para>
///   Required(row) = RequiredLiteral + eval(RequiredExpr, resolve refs via Required) - purely structural, so
///   it's memoized forever once computed (this instance is built once and reused for the mod's whole session).
///   </para>
///   <para>
///   Total(row) = Have(row) + eval(RequiredExpr, resolve refs via Total) - depends on live game state, so it's
///   recomputed from scratch (via <see cref="ComputeAllTotals" />) every time the tracker menu opens, sharing one
///   memo across that single pass only (never cached across opens).
///   </para>
/// </summary>
public sealed class RequirementGraph
{
  private readonly ChecklistDocument _document;
  private readonly Dictionary<(string Sheet, string Label), double> _requiredCache = new();

  public RequirementGraph(ChecklistDocument document)
  {
    _document = document;
  }

  public double Required(ChecklistRow row) => RequiredInternal((row.Sheet, row.Label), new HashSet<(string, string)>());

  private double RequiredInternal((string Sheet, string Label) key, HashSet<(string, string)> visiting)
  {
    if (_requiredCache.TryGetValue(key, out var cached))
    {
      return cached;
    }

    if (!visiting.Add(key))
    {
      return 0; // cycle guard - not expected in the real data, but never hang/crash over it
    }

    if (!_document.ByKey.TryGetValue(key, out var row))
    {
      visiting.Remove(key);
      return 0; // a ref that doesn't resolve - also not expected, but degrade rather than crash
    }

    var value = row.RequiredLiteral + (row.RequiredExpr is null ? 0 : EvalRequired(row.RequiredExpr, visiting));
    visiting.Remove(key);
    _requiredCache[key] = value;
    return value;
  }

  private double EvalRequired(RequirementExpr expr, HashSet<(string, string)> visiting) => expr switch
  {
    ConstExpr c => c.Value,
    RefExpr r => RequiredInternal((r.Sheet, r.Label), visiting),
    SumExpr s => s.Args.Sum(a => EvalRequired(a, visiting)),
    MulExpr m => m.Factor * EvalRequired(m.Arg, visiting),
    DivExpr d => EvalRequired(d.Arg, visiting) / d.Divisor,
    MinExpr mn => mn.Args.Select(a => EvalRequired(a, visiting)).Min(),
    MaxExpr mx => mx.Args.Select(a => EvalRequired(a, visiting)).Max(),
    _ => 0,
  };

  /// <summary>Computes every row's live Total in one pass, sharing a single memo (never persisted beyond this call).</summary>
  public Dictionary<(string Sheet, string Label), double> ComputeAllTotals(IReadOnlyDictionary<(string Sheet, string Label), double> haveByKey)
  {
    var memo = new Dictionary<(string, string), double>();
    var visiting = new HashSet<(string, string)>();
    foreach (var row in _document.Rows)
    {
      TotalInternal((row.Sheet, row.Label), haveByKey, memo, visiting);
    }

    return memo;
  }

  private double TotalInternal(
    (string Sheet, string Label) key,
    IReadOnlyDictionary<(string Sheet, string Label), double> haveByKey,
    Dictionary<(string, string), double> memo,
    HashSet<(string, string)> visiting)
  {
    if (memo.TryGetValue(key, out var cached))
    {
      return cached;
    }

    if (!visiting.Add(key))
    {
      return 0;
    }

    if (!_document.ByKey.TryGetValue(key, out var row))
    {
      visiting.Remove(key);
      return 0;
    }

    var baseHave = haveByKey.TryGetValue(key, out var h) ? h : 0;
    var rawValue = baseHave + (row.RequiredExpr is null ? 0 : EvalTotal(row.RequiredExpr, haveByKey, memo, visiting));
    // Overflow protection: a row can never contribute more toward dependent totals (or its own
    // percentage) than its own Required - otherwise stockpiling one item way past 999 (e.g. a huge
    // pile of Stone from staircases) inflates every row that references it, sometimes past 100%.
    var value = Math.Min(rawValue, RequiredInternal(key, new HashSet<(string, string)>()));
    visiting.Remove(key);
    memo[key] = value;
    return value;
  }

  private double EvalTotal(
    RequirementExpr expr,
    IReadOnlyDictionary<(string Sheet, string Label), double> haveByKey,
    Dictionary<(string, string), double> memo,
    HashSet<(string, string)> visiting) => expr switch
  {
    ConstExpr c => c.Value,
    RefExpr r => TotalInternal((r.Sheet, r.Label), haveByKey, memo, visiting),
    SumExpr s => s.Args.Sum(a => EvalTotal(a, haveByKey, memo, visiting)),
    MulExpr m => m.Factor * EvalTotal(m.Arg, haveByKey, memo, visiting),
    DivExpr d => EvalTotal(d.Arg, haveByKey, memo, visiting) / d.Divisor,
    MinExpr mn => mn.Args.Select(a => EvalTotal(a, haveByKey, memo, visiting)).Min(),
    MaxExpr mx => mx.Args.Select(a => EvalTotal(a, haveByKey, memo, visiting)).Max(),
    _ => 0,
  };

  /// <summary>
  ///   One line per direct term of a row's Required expression, as "(label, contribution)" - e.g. Ancient Fruit
  ///   gets [("base", 999), ("Jelly: Ancient Fruit", 999), ("Wine: Ancient Fruit", 999), ("Dried Ancient Fruit
  ///   ×5", 4995)]. Nested sums flatten; a min/max/div subtree (a handful of rows, e.g. Coal, Omni Geode,
  ///   Slime) collapses to one opaque line naming whatever it references - not pretty, but correct, and these
  ///   are rare enough not to be worth more than that.
  /// </summary>
  public List<(string Label, double Contribution)> DescribeBreakdown(ChecklistRow row)
  {
    var lines = new List<(string, double)> { ("base", row.RequiredLiteral) };
    if (row.RequiredExpr is not null)
    {
      lines.AddRange(FlattenTerms(row.RequiredExpr, 1));
    }

    return lines;
  }

  private IEnumerable<(string Label, double Contribution)> FlattenTerms(RequirementExpr expr, double weight)
  {
    switch (expr)
    {
      case SumExpr s:
        foreach (var a in s.Args)
        {
          foreach (var t in FlattenTerms(a, weight))
          {
            yield return t;
          }
        }

        break;

      case MulExpr m:
        foreach (var t in FlattenTerms(m.Arg, weight * m.Factor))
        {
          yield return t;
        }

        break;

      case RefExpr r:
      {
        var label = weight == 1 ? r.Label : $"{r.Label} ×{weight:0.##}";
        yield return (label, weight * RequiredInternal((r.Sheet, r.Label), new HashSet<(string, string)>()));
        break;
      }

      case ConstExpr c:
        yield return ("constant", weight * c.Value);
        break;

      default:
      {
        // Min/Max/Div: not flattenable into clean weighted single terms - show one opaque line naming
        // whatever items it touches, with its evaluated contribution.
        var refs = CollectRefLabels(expr).Distinct().ToList();
        var label = refs.Count > 0 ? string.Join(", ", refs) : expr.GetType().Name;
        yield return (label, weight * EvalRequired(expr, new HashSet<(string, string)>()));
        break;
      }
    }
  }

  private static IEnumerable<string> CollectRefLabels(RequirementExpr expr)
  {
    switch (expr)
    {
      case RefExpr r:
        yield return r.Label;
        break;
      case SumExpr s:
        foreach (var a in s.Args)
        foreach (var l in CollectRefLabels(a))
        {
          yield return l;
        }

        break;
      case MinExpr mn:
        foreach (var a in mn.Args)
        foreach (var l in CollectRefLabels(a))
        {
          yield return l;
        }

        break;
      case MaxExpr mx:
        foreach (var a in mx.Args)
        foreach (var l in CollectRefLabels(a))
        {
          yield return l;
        }

        break;
      case MulExpr m:
        foreach (var l in CollectRefLabels(m.Arg))
        {
          yield return l;
        }

        break;
      case DivExpr d:
        foreach (var l in CollectRefLabels(d.Arg))
        {
          yield return l;
        }

        break;
    }
  }
}
