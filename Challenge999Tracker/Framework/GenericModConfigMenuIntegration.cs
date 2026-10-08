using System;
using StardewModdingAPI;

namespace Challenge999Tracker.Framework;

internal static class GenericModConfigMenuIntegration
{
  private const string GmcmUniqueId = "spacechase0.GenericModConfigMenu";

  public static void Register(IModHelper helper, IManifest manifest, ModConfig config, Action save)
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
  }

  private static void Reset(ModConfig config)
  {
    var defaults = new ModConfig();
    config.OpenTrackerKey = defaults.OpenTrackerKey;
  }
}
