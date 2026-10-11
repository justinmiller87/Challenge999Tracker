using System;
using System.Collections.Generic;
using System.Linq;
using Challenge999Tracker.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

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
    // Loaded here (not lazily on first open, like the rest of this field) purely so GMCM has the sheet list
    // to build per-category toggles from before the player ever opens the tracker - it's static JSON with no
    // game-state dependency, so there's no harm starting it early.
    _checklist ??= ChecklistLoader.Load(Helper);
    GenericModConfigMenuIntegration.Register(Helper, ModManifest, _config, () => Helper.WriteConfig(_config), _checklist.Sheets);
  }

  private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
  {
    if (!Context.IsWorldReady)
    {
      return;
    }

    // While the tracker is open, every other keyboard key is suppressed before anything else gets a look at
    // it - not just the vanilla menu-close binding (see TrackerMenu.receiveKeyPress for that half), but any
    // other mod's own hotkey too. A search term can contain any letter, and this mod has no way to know which
    // ones double as some other mod's toggle (e.g. a mod that pauses/resumes something on "L") - suppressing
    // here consumes the keystroke before that mod's own ButtonPressed handler can act on it.
    if (Game1.activeClickableMenu is TrackerMenu && e.Button != _config.OpenTrackerKey && e.Button != SButton.Escape
        && e.Button.TryGetKeyboard(out _))
    {
      Helper.Input.Suppress(e.Button);
    }

    if (e.Button == _config.ToggleLocationTrackedKey && Game1.activeClickableMenu is null)
    {
      Helper.Input.Suppress(e.Button);
      ToggleLocationTracked();
    }
    else if (e.Button == _config.ToggleChestTrackedKey && Game1.activeClickableMenu is ItemGrabMenu { sourceItem: Chest chest })
    {
      Helper.Input.Suppress(e.Button);
      ToggleChestTracked(chest);
    }

    if (e.Button != _config.OpenTrackerKey)
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

  /// <summary>Tags/untags the player's current location as dedicated storage - see <see cref="ModConfig.TrackedLocations" />.</summary>
  private void ToggleLocationTracked()
  {
    var name = Game1.currentLocation.NameOrUniqueName;
    if (_config.TrackedLocations.Remove(name))
    {
      Helper.WriteConfig(_config);
      Game1.addHUDMessage(new HUDMessage($"999 Challenge Tracker: '{name}' is no longer dedicated storage.") { noIcon = true });
    }
    else
    {
      _config.TrackedLocations.Add(name);
      Helper.WriteConfig(_config);
      Game1.addHUDMessage(new HUDMessage($"999 Challenge Tracker: '{name}' is now dedicated storage - every chest and fridge here counts.") { noIcon = true });
    }
  }

  /// <summary>Tags/untags the open chest as dedicated storage - see <see cref="ModConfig.TrackedChests" />.</summary>
  private void ToggleChestTracked(Chest chest)
  {
    if (chest.SpecialChestType == Chest.SpecialChestTypes.JunimoChest)
    {
      Game1.addHUDMessage(new HUDMessage("999 Challenge Tracker: Junimo chests share one pool across the whole farm and can't be tagged individually.") { noIcon = true });
      return;
    }

    var location = Game1.currentLocation.NameOrUniqueName;
    var tile = chest.TileLocation;
    var existing = _config.TrackedChests.FirstOrDefault(t => t.Location == location && t.X == (int)tile.X && t.Y == (int)tile.Y);
    if (existing != null)
    {
      _config.TrackedChests.Remove(existing);
      Helper.WriteConfig(_config);
      Game1.addHUDMessage(new HUDMessage("999 Challenge Tracker: this chest is no longer dedicated storage.") { noIcon = true });
    }
    else
    {
      _config.TrackedChests.Add(new TrackedChestRef { Location = location, X = (int)tile.X, Y = (int)tile.Y });
      Helper.WriteConfig(_config);
      Game1.addHUDMessage(new HUDMessage("999 Challenge Tracker: this chest is now dedicated storage.") { noIcon = true });
    }
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
      var stock = LiveScanner.Collect(_config.ScanOnlyDedicatedStorage, _config.TrackedLocations, _config.TrackedChests);
      entries = ProgressCalculator.Calculate(_checklist, _graph, stock, _staticIndex, _config.IgnoreQuality);
    }
    catch (Exception ex)
    {
      Monitor.Log($"Couldn't open the 999 Challenge Tracker: {ex}", LogLevel.Error);
      Game1.addHUDMessage(new HUDMessage("999 Challenge Tracker: couldn't load the checklist data - see the SMAPI log for details.", HUDMessage.error_type));
      return;
    }

    // Applied after Calculate(), not before: Required/Total above were computed from the *full* requirement
    // graph, so a hidden row (a disabled sheet, an excluded legendary-fish Roe row, a Qi Fruit artisan good)
    // still correctly feeds any other row's cross-sheet formula that references it. This filter only decides
    // what's shown and what counts toward the Totals dashboard/grand total, same as the per-sheet
    // All/Incomplete/Complete filter.
    entries = entries.Where(IsEntryTracked).ToList();

    var totalsPage = ProgressCalculator.ComputeTotalsPage(_checklist.Sheets, entries);

    Game1.playSound("bigSelect");
    Game1.activeClickableMenu = new TrackerMenu(entries, totalsPage, _config, () => Helper.WriteConfig(_config), Helper.Translation);
  }

  private bool IsEntryTracked(ProgressEntry entry)
  {
    if (!(_config.SheetEnabled.TryGetValue(entry.Sheet, out var enabled) ? enabled : true))
    {
      return false;
    }

    if (_config.ExcludeLegendaryFishRoe && OptionalItemSets.LegendaryFishRoe.Contains((entry.Sheet, entry.Item)))
    {
      return false;
    }

    if (_config.ExcludeQiFruitArtisanGoods && OptionalItemSets.QiFruitArtisanGoods.Contains((entry.Sheet, entry.Item)))
    {
      return false;
    }

    return true;
  }
}
