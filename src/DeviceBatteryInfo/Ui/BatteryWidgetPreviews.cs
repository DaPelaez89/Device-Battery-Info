using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Previews;
using MacroDeck.Ui.Runtime;

namespace DeviceBatteryInfo.Ui;

internal static class BatteryWidgetPreviews
{
    private const int CornerRadius = 16;

    [UiPreview("Panel", View = nameof(BatteryWidgetView), Profile = UiPreviewProfiles.Widget)]
    public static UiElement Panel() => PanelOf(BatteryWidgetSamples.Panel());

    [UiPreview(
        "Panel - names and heading",
        View = nameof(BatteryWidgetView),
        Profile = UiPreviewProfiles.Widget
    )]
    public static UiElement PanelNamed() => PanelOf(BatteryWidgetSamples.PanelNamed());

    [UiPreview(
        "Panel - names and trend",
        View = nameof(BatteryWidgetView),
        Profile = UiPreviewProfiles.Widget
    )]
    public static UiElement PanelTrend() => PanelOf(BatteryWidgetSamples.PanelTrend());

    [UiPreview("Panel - list", View = nameof(BatteryWidgetView), Profile = UiPreviewProfiles.Widget)]
    public static UiElement PanelList() => PanelOf(BatteryWidgetSamples.PanelList());

    [UiPreview(
        "Panel - low battery",
        View = nameof(BatteryWidgetView),
        Profile = UiPreviewProfiles.Widget
    )]
    public static UiElement PanelLow() => PanelOf(BatteryWidgetSamples.PanelLow());

    [UiPreview(
        "Panel - nothing configured",
        View = nameof(BatteryWidgetView),
        Profile = UiPreviewProfiles.Widget
    )]
    public static UiElement PanelEmpty() => PanelOf(BatteryWidgetSamples.PanelEmpty());

    [UiPreview(
        "Tile - charging",
        View = nameof(BatteryWidgetView),
        Profile = UiPreviewProfiles.Widget
    )]
    public static UiElement TileCharging() => TileOf(BatteryWidgetSamples.TileCharging());

    [UiPreview(
        "Tile - discharging",
        View = nameof(BatteryWidgetView),
        Profile = UiPreviewProfiles.Widget
    )]
    public static UiElement TileDischarging() => TileOf(BatteryWidgetSamples.TileDischarging());

    [UiPreview(
        "Tile - low battery",
        View = nameof(BatteryWidgetView),
        Profile = UiPreviewProfiles.Widget
    )]
    public static UiElement TileLow() => TileOf(BatteryWidgetSamples.TileLow());

    private static UiElement PanelOf(BatteryWidgetModel model) =>
        BatteryWidgetView.Build(
            BatteryWidgetTypes.PanelId,
            new UiState<BatteryWidgetModel>(model),
            CornerRadius
        );

    private static UiElement TileOf(BatteryWidgetModel model) =>
        BatteryWidgetView.Build(
            BatteryWidgetTypes.TileId,
            new UiState<BatteryWidgetModel>(model),
            CornerRadius
        );
}
