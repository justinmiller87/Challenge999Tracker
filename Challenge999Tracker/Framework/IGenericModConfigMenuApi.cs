using System;
using StardewModdingAPI;

namespace Challenge999Tracker.Framework;

/// <summary>The minimal subset of Generic Mod Config Menu's API used by this mod (standard SMAPI interop stub).</summary>
public interface IGenericModConfigMenuApi
{
  void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

  void AddKeybind(
    IManifest mod,
    Func<SButton> getValue,
    Action<SButton> setValue,
    Func<string> name,
    Func<string>? tooltip = null,
    string? fieldId = null
  );
}
