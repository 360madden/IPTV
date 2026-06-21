using Iptv.Persistence;

namespace Iptv.App.Services;

public readonly record struct AppearancePresetSettings(
    AppTheme Theme,
    AppUiScale UiScale,
    ChannelViewDensity ChannelViewDensity,
    bool LargeLibraryMode);

public static class AppearancePresetCatalog
{
    public static AppAppearancePreset Normalize(AppAppearancePreset preset)
    {
        return Enum.IsDefined(preset) ? preset : AppAppearancePreset.Custom;
    }

    public static bool TryGetSettings(AppAppearancePreset preset, out AppearancePresetSettings settings)
    {
        settings = Normalize(preset) switch
        {
            AppAppearancePreset.Desktop => new AppearancePresetSettings(
                AppTheme.Dark,
                AppUiScale.Normal,
                ChannelViewDensity.Comfortable,
                LargeLibraryMode: false),
            AppAppearancePreset.LivingRoom => new AppearancePresetSettings(
                AppTheme.Dark,
                AppUiScale.Tv,
                ChannelViewDensity.Comfortable,
                LargeLibraryMode: true),
            AppAppearancePreset.HighContrast => new AppearancePresetSettings(
                AppTheme.HighContrast,
                AppUiScale.Large,
                ChannelViewDensity.Compact,
                LargeLibraryMode: true),
            _ => default
        };

        return Normalize(preset) != AppAppearancePreset.Custom;
    }
}
