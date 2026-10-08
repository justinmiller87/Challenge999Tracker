using System.Collections.Generic;
using System.Linq;

namespace Challenge999Tracker.Framework;

/// <summary>The bundled checklist: every item row plus the sheet display order, indexed by (sheet, label) for RequirementExpr's cross-sheet refs.</summary>
public sealed class ChecklistDocument
{
  public ChecklistDocument(IReadOnlyList<string> sheets, List<ChecklistRow> rows)
  {
    Sheets = sheets;
    Rows = rows;
    ByKey = rows.ToDictionary(r => (r.Sheet, r.Label));
  }

  public IReadOnlyList<string> Sheets { get; }
  public List<ChecklistRow> Rows { get; }
  public Dictionary<(string Sheet, string Label), ChecklistRow> ByKey { get; }
}
