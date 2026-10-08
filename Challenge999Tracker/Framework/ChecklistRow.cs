namespace Challenge999Tracker.Framework;

/// <summary>
///   One row of the "999 Challenge" checklist, from the bundled <c>assets/checklist999.json</c> (pre-extracted
///   and validated from the user's own spreadsheet - see RequirementGraph for how RequiredLiteral/RequiredExpr
///   combine into a row's true, live Required/Total).
/// </summary>
/// <param name="Sheet">The checklist tab this row is on.</param>
/// <param name="Label">The item's checklist label.</param>
/// <param name="RequiredLiteral">This row's own flat base target (almost always 999).</param>
/// <param name="RequiredExpr">
///   The extra amount this item is consumed elsewhere in the workbook (e.g. Ancient Fruit needs enough for its
///   own Jelly/Wine/Dried too), or null for a pure leaf row where Required is just <see cref="RequiredLiteral" />.
/// </param>
public sealed record ChecklistRow(string Sheet, string Label, double RequiredLiteral, RequirementExpr? RequiredExpr);
