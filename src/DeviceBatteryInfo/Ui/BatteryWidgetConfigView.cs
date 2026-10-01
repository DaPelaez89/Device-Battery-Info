using System.Text.Json;
using DeviceBatteryInfo.Core;
using MacroDeck.Ui.Config;
using MacroDeck.Ui.Config.Options;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Runtime;

namespace DeviceBatteryInfo.Ui;

internal static class BatteryWidgetConfigView
{
    public static UiElement Build(
        BatteryWidgetOptions current,
        IReadOnlyList<BatterySlot> devices,
        JsonElement flows = default
    )
    {
        var sourceIds = new UiState<IReadOnlyList<string>>(current.SourceIds);
        var showBar = new UiState<bool>(current.ShowBar);
        var showPercent = new UiState<bool>(current.ShowPercent);
        var showCharging = new UiState<bool>(current.ShowCharging);
        var showTimeToFull = new UiState<bool>(current.ShowTimeToFull);
        var showTrend = new UiState<bool>(current.ShowTrend);
        var lowThreshold = new UiState<double>(current.LowThreshold);
        var sort = new UiState<string>(BatteryWidgetOptions.SortValue(current.Sort));
        var title = new UiState<string>(current.Title);
        // A default JsonElement cannot be serialized into the tree.
        var flowList = new UiState<JsonElement>(
            flows.ValueKind == JsonValueKind.Array ? flows : EmptyFlows
        );

        var options = devices
            .Select(d => UiOption.Of(d.Id) with { Label = d.DisplayName })
            .ToArray();

        var sortOptions = new[]
        {
            UiOption.Of(BatteryWidgetOptions.SortManual) with
            {
                Label = Strings.Widgets.Config.Sort.Manual(),
            },
            UiOption.Of(BatteryWidgetOptions.SortLowestFirst) with
            {
                Label = Strings.Widgets.Config.Sort.LowestFirst(),
            },
            UiOption.Of(BatteryWidgetOptions.SortChargingFirst) with
            {
                Label = Strings.Widgets.Config.Sort.ChargingFirst(),
            },
            UiOption.Of(BatteryWidgetOptions.SortAlphabetical) with
            {
                Label = Strings.Widgets.Config.Sort.Alphabetical(),
            },
        };

        return new UiWidgetConfiguration
        {
            Key = "battery-widget-config",
            Properties = new UiWidgetProperties
            {
                Key = "battery-widget-config-properties",
                Children =
                [
                    new UiStringInput
                    {
                        Key = "title",
                        Label = Strings.Widgets.Config.Title.Label(),
                        Description = Strings.Widgets.Config.Title.Description(),
                        Binding = Bind.To(title),
                    },
                    new UiMultiSelectInput
                    {
                        Key = "sourceIds",
                        Label = Strings.Widgets.Config.Devices.Label(),
                        Options = UiValue.Of<IReadOnlyList<UiOption>>(options),
                        Binding = Bind.To(sourceIds),
                        Reorderable = UiValue.Of(true),
                    },
                    new UiChoiceInput
                    {
                        Key = "sort",
                        Label = Strings.Widgets.Config.Sort.Label(),
                        Options = UiValue.Of<IReadOnlyList<UiOption>>(sortOptions),
                        Binding = Bind.To(sort),
                    },
                    new UiBooleanInput
                    {
                        Key = "showBar",
                        Label = Strings.Widgets.Config.ShowBar.Label(),
                        Binding = Bind.To(showBar),
                    },
                    new UiBooleanInput
                    {
                        Key = "showPercent",
                        Label = Strings.Widgets.Config.ShowPercent.Label(),
                        Binding = Bind.To(showPercent),
                    },
                    new UiBooleanInput
                    {
                        Key = "showCharging",
                        Label = Strings.Widgets.Config.ShowCharging.Label(),
                        Binding = Bind.To(showCharging),
                    },
                    new UiBooleanInput
                    {
                        Key = "showTimeToFull",
                        Label = Strings.Widgets.Config.ShowTimeToFull.Label(),
                        Binding = Bind.To(showTimeToFull),
                    },
                    new UiBooleanInput
                    {
                        Key = "showTrend",
                        Label = Strings.Widgets.Config.ShowTrend.Label(),
                        Binding = Bind.To(showTrend),
                    },
                    new UiNumberInput
                    {
                        Key = "lowThreshold",
                        Label = Strings.Widgets.Config.LowThreshold.Label(),
                        Min = 1,
                        Max = 99,
                        Step = 1,
                        Binding = Bind.To(lowThreshold),
                    },
                ],
            },
            // The host reads flows from the top-level "flows" key, and an editor region is what makes
            // the desktop draw the split layout.
            Editor = new UiWidgetEditor
            {
                Key = "battery-widget-config-editor",
                Children =
                [
                    new UiActionsListEditor
                    {
                        Key = "flows",
                        Binding = Bind.To(flowList),
                        CanRun = true,
                    },
                ],
            },
        };
    }

    private static readonly JsonElement EmptyFlows = JsonDocument.Parse("[]").RootElement.Clone();
}
