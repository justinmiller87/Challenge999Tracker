using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;

namespace Challenge999Tracker.Framework;

/// <summary>
///   Loads the bundled, pre-extracted checklist data (<c>assets/checklist999.json</c>) - no .ods file is ever
///   read at runtime, so the mod has no dependency on the user's personal spreadsheet. The JSON was generated
///   once, offline, by <c>tools/generate_checklist_json.py</c> (a copy of the extraction script used against the
///   real spreadsheet; not shipped in the release zip) - see that file and the README's "Updating the checklist
///   data" section if the checklist is ever rebalanced.
/// </summary>
public static class ChecklistLoader
{
  public static ChecklistDocument Load(IModHelper helper, string assetPath = "assets/checklist999.json")
  {
    var raw = helper.Data.ReadJsonFile<RawRoot>(assetPath)
      ?? throw new InvalidOperationException($"Couldn't find or parse the bundled checklist data at '{assetPath}'.");

    var rows = raw.Rows
      .Select(r => new ChecklistRow(r.Sheet, r.Label, r.RequiredLiteral, RequirementExprJson.ParseOrNull(r.RequiredExpr)))
      .ToList();

    return new ChecklistDocument(raw.Sheets, rows);
  }

  private sealed class RawRoot
  {
    public List<string> Sheets { get; set; } = new();
    public List<RawRow> Rows { get; set; } = new();
  }

  private sealed class RawRow
  {
    public string Sheet { get; set; } = "";
    public string Label { get; set; } = "";
    public double RequiredLiteral { get; set; }
    public JToken? RequiredExpr { get; set; }
  }
}
