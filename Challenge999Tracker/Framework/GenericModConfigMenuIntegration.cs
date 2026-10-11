using System;
using System.Collections.Generic;
using StardewModdingAPI;

namespace Challenge999Tracker.Framework;

internal static class GenericModConfigMenuIntegration
{
  private const string GmcmUniqueId = "spacechase0.GenericModConfigMenu";

  public static void Register(IModHelper helper, IManifest manifest, ModConfig config, Action save, IReadOnlyList<string> sheets)
  {
    IGenericModConfigMenuApi? api = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(GmcmUniqueId);
    if (api == null)
    {
      return;
    }

    api.Register(manifest, () => Reset(config), save);

    api.AddKeybind(
      manifest,
      () => config.OpenTrackerKey,
      v => config.OpenTrackerKey = v,
      () => helper.Translation.Get("config.open-tracker-key.name"),
      () => helper.Translation.Get("config.open-tracker-key.tooltip")
    );

    api.AddBoolOption(
      manifest,
      () => config.IgnoreQuality,
      v => config.IgnoreQuality = v,
      () => helper.Translation.Get("config.ignore-quality.name"),
      () => helper.Translation.Get("config.ignore-quality.tooltip")
    );

    api.AddSectionTitle(manifest, () => helper.Translation.Get("config.optional-items.section"));

    api.AddBoolOption(
      manifest,
      () => config.ExcludeLegendaryFishRoe,
      v => config.ExcludeLegendaryFishRoe = v,
      () => helper.Translation.Get("config.exclude-legendary-roe.name"),
      () => helper.Translation.Get("config.exclude-legendary-roe.tooltip")
    );

    api.AddBoolOption(
      manifest,
      () => config.ExcludeQiFruitArtisanGoods,
      v => config.ExcludeQiFruitArtisanGoods = v,
      () => helper.Translation.Get("config.exclude-qi-fruit-goods.name"),
      () => helper.Translation.Get("config.exclude-qi-fruit-goods.tooltip")
    );

    api.AddSectionTitle(manifest, () => helper.Translation.Get("config.dedicated-storage.section"));

    api.AddBoolOption(
      manifest,
      () => config.ScanOnlyDedicatedStorage,
      v => config.ScanOnlyDedicatedStorage = v,
      () => helper.Translation.Get("config.scan-only-dedicated-storage.name"),
      () => helper.Translation.Get("config.scan-only-dedicated-storage.tooltip")
    );

    api.AddKeybind(
      manifest,
      () => config.ToggleLocationTrackedKey,
      v => config.ToggleLocationTrackedKey = v,
      () => helper.Translation.Get("config.toggle-location-tracked-key.name"),
      () => helper.Translation.Get("config.toggle-location-tracked-key.tooltip")
    );

    api.AddKeybind(
      manifest,
      () => config.ToggleChestTrackedKey,
      v => config.ToggleChestTrackedKey = v,
      () => helper.Translation.Get("config.toggle-chest-tracked-key.name"),
      () => helper.Translation.Get("config.toggle-chest-tracked-key.tooltip")
    );

    api.AddSectionTitle(manifest, () => helper.Translation.Get("config.categories.section"));

    foreach (var sheet in sheets)
    {
      api.AddBoolOption(
        manifest,
        () => config.SheetEnabled.TryGetValue(sheet, out var enabled) ? enabled : true,
        v => config.SheetEnabled[sheet] = v,
        () => sheet
      );
    }
  }

  private static void Reset(ModConfig config)
  {
    var defaults = new ModConfig();
    config.OpenTrackerKey = defaults.OpenTrackerKey;
    config.IgnoreQuality = defaults.IgnoreQuality;
    config.ExcludeLegendaryFishRoe = defaults.ExcludeLegendaryFishRoe;
    config.ExcludeQiFruitArtisanGoods = defaults.ExcludeQiFruitArtisanGoods;
    config.SheetEnabled.Clear();
    config.ScanOnlyDedicatedStorage = defaults.ScanOnlyDedicatedStorage;
    config.ToggleLocationTrackedKey = defaults.ToggleLocationTrackedKey;
    config.ToggleChestTrackedKey = defaults.ToggleChestTrackedKey;
    config.TrackedLocations.Clear();
    config.TrackedChests.Clear();
  }
}
