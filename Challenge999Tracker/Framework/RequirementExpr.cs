using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Challenge999Tracker.Framework;

/// <summary>
///   The small cross-sheet-dependency expression tree used by the bundled checklist JSON's "requiredExpr"
///   field - e.g. Ancient Fruit's is <c>sum(ref(Jelly), ref(Wine), mul(5, ref(Dried)))</c>. This is the entire
///   grammar the JSON ever contains (confirmed against all 1,194 rows during extraction): const, ref, sum, mul,
///   div, min, max.
/// </summary>
public abstract record RequirementExpr;

public sealed record ConstExpr(double Value) : RequirementExpr;

/// <summary>Refers to another row's own Required/Total, resolved by exact (sheet, label) match.</summary>
public sealed record RefExpr(string Sheet, string Label) : RequirementExpr;

public sealed record SumExpr(List<RequirementExpr> Args) : RequirementExpr;

public sealed record MulExpr(double Factor, RequirementExpr Arg) : RequirementExpr;

public sealed record DivExpr(RequirementExpr Arg, double Divisor) : RequirementExpr;

public sealed record MinExpr(List<RequirementExpr> Args) : RequirementExpr;

public sealed record MaxExpr(List<RequirementExpr> Args) : RequirementExpr;

/// <summary>Builds a <see cref="RequirementExpr" /> tree from the raw JSON token, rather than a custom JsonConverter - simpler to get right for this small a grammar.</summary>
public static class RequirementExprJson
{
  /// <summary>Parses "requiredExpr", treating both a missing property and a JSON null (which Newtonsoft hands back as a non-null JValue of type Null, not a C# null) as "no expression".</summary>
  public static RequirementExpr? ParseOrNull(JToken? token) =>
    token is null or { Type: JTokenType.Null } ? null : Parse(token);

  public static RequirementExpr Parse(JToken token)
  {
    var op = (string?)token["op"] ?? throw new System.FormatException($"Requirement expression node has no 'op': {token}");
    switch (op)
    {
      case "const":
        return new ConstExpr((double)token["value"]!);
      case "ref":
        return new RefExpr((string)token["sheet"]!, (string)token["label"]!);
      case "sum":
        return new SumExpr(ParseArgs(token));
      case "mul":
        return new MulExpr((double)token["factor"]!, Parse(token["arg"]!));
      case "div":
        return new DivExpr(Parse(token["arg"]!), (double)token["divisor"]!);
      case "min":
        return new MinExpr(ParseArgs(token));
      case "max":
        return new MaxExpr(ParseArgs(token));
      default:
        throw new System.FormatException($"Unknown requirement expression op '{op}'.");
    }
  }

  private static List<RequirementExpr> ParseArgs(JToken token) =>
    ((JArray)token["args"]!).Select(Parse).ToList();
}
