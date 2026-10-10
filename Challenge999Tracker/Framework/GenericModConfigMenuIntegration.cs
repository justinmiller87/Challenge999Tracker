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
  }
}
