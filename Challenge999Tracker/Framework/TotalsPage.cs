using System.Collections.Generic;

namespace Challenge999Tracker.Framework;

/// <summary>One row of the Totals dashboard - mirrors the workbook's own "Totals" sheet, computed from this mod's own per-row Required/Total rather than parsed from an aggregate formula.</summary>
public sealed record SheetTotals(string Sheet, int ItemCount, double RequiredSum, double TotalSum, double PercentDone, double ShareOfChallenge, bool Done);

public sealed record TotalsPage(List<SheetTotals> Sheets, SheetTotals GrandTotal);
