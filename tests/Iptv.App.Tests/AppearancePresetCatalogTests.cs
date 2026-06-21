using Iptv.App.Services;
using Iptv.Persistence;

namespace Iptv.App.Tests;

public sealed class AppearancePresetCatalogTests
{
    [Fact]
    public void BuiltInPresets_MapToReadableThemeScaleAndDensity()
    {
        Assert.True(AppearancePresetCatalog.TryGetSettings(AppAppearancePreset.Desktop, out AppearancePresetSettings desktop));
        Assert.Equal(AppTheme.Dark, desktop.Theme);
        Assert.Equal(AppUiScale.Normal, desktop.UiScale);
        Assert.Equal(ChannelViewDensity.Comfortable, desktop.ChannelViewDensity);
        Assert.False(desktop.LargeLibraryMode);

        Assert.True(AppearancePresetCatalog.TryGetSettings(AppAppearancePreset.LivingRoom, out AppearancePresetSettings livingRoom));
        Assert.Equal(AppUiScale.Tv, livingRoom.UiScale);
        Assert.True(livingRoom.LargeLibraryMode);

        Assert.True(AppearancePresetCatalog.TryGetSettings(AppAppearancePreset.HighContrast, out AppearancePresetSettings highContrast));
        Assert.Equal(AppTheme.HighContrast, highContrast.Theme);
        Assert.True(highContrast.LargeLibraryMode);
    }

    [Fact]
    public void CustomPreset_DoesNotApplyConcreteSettings()
    {
        Assert.False(AppearancePresetCatalog.TryGetSettings(AppAppearancePreset.Custom, out _));
    }
}
