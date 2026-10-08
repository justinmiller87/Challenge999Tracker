using System;
using System.Collections.Generic;
using Challenge999Tracker.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Challenge999Tracker;

public sealed class ModEntry : Mod
{
  private ModConfig _config = null!;
  private Dictionary<string, List<StaticItemIndex.Entry>>? _staticIndex;
  private ChecklistDocument? _checklist;
  private RequirementGraph? _graph;

  public override void Entry(IModHelper helper)
  {
    _config = helper.ReadConfig<ModConfig>();
    helper.Events.GameLoop.GameLaunched += OnGameLaunched;
    helper.Events.Input.ButtonPressed += OnButtonPressed;
  }

  private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
  {
    GenericModConfigMenuIntegration.Register(Helper, ModManifest, _config, () => Helper.WriteConfig(_config));
  }

  private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
  {
    if (!Context.IsWorldReady || e.Button != _config.OpenTrackerKey)
    {
      return;
    }

    if (Game1.activeClickableMenu is TrackerMenu)
    {
      Helper.Input.Suppress(e.Button);
      Game1.exitActiveMenu();
      return;
    }

    if (Game1.activeClickableMenu != null)
    {
      return; // don't steal another open menu's hotkeys
    }

    Helper.Input.Suppress(e.Button);
    OpenTracker();
  }

  private void OpenTracker()
  {
    // The bundled checklist data and its parsed requirement graph are static (no .ods, no live state involved),
    // so they're loaded/built once and reused forever. The static item catalog is likewise session-constant.
    // Only the live stock scan - and the Have/Total numbers derived from it - are redone every time the
    // tracker opens, so newly picked-up items show up immediately.
    _staticIndex ??= StaticItemIndex.Build();

    List<ProgressEntry> entries;
    try
    {
      _checklist ??= ChecklistLoader.Load(Helper);
      _graph ??= new RequirementGraph(_checklist);
      var stock = LiveScanner.Collect();
      entries = ProgressCalculator.Calculate(_checklist, _graph, stock, _staticIndex);
    }
    catch (Exception ex)
    {
      Monitor.Log($"Couldn't open the 999 Challenge Tracker: {ex}", LogLevel.Error);
      Game1.addHUDMessage(new HUDMessage("999 Challenge Tracker: couldn't load the checklist data - see the SMAPI log for details.", HUDMessage.error_type));
      return;
    }

    var totalsPage = ProgressCalculator.ComputeTotalsPage(_checklist.Sheets, entries);

    Game1.playSound("bigSelect");
    Game1.activeClickableMenu = new TrackerMenu(entries, totalsPage, _config, () => Helper.WriteConfig(_config), Helper.Translation);
  }
}
