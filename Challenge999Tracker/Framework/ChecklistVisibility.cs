namespace Challenge999Tracker.Framework;

public enum ChecklistVisibility
{
  All,
  Incomplete,
  Complete,
}

public static class ChecklistVisibilityExtensions
{
  public static ChecklistVisibility Next(this ChecklistVisibility current) => current switch
  {
    ChecklistVisibility.All => ChecklistVisibility.Incomplete,
    ChecklistVisibility.Incomplete => ChecklistVisibility.Complete,
    _ => ChecklistVisibility.All,
  };

  public static bool Matches(this ChecklistVisibility visibility, ProgressEntry entry) => visibility switch
  {
    ChecklistVisibility.Incomplete => !entry.IsComplete,
    ChecklistVisibility.Complete => entry.IsComplete,
    _ => true,
  };
}
