using DeviceBatteryInfo.Core;

namespace DeviceBatteryInfo.Ui;

internal static class BatteryWidgetSamples
{
    public static BatteryWidgetModel For(string localId) =>
        localId == BatteryWidgetTypes.TileId ? TileCharging() : Panel();

    public static BatteryWidgetModel Panel() =>
        new(
            [
                Row("phone", "Phone", 48, BatteryStatus.Discharging, BatterySourceKind.Phone),
                Row(
                    "earbuds",
                    "Earbuds",
                    2,
                    BatteryStatus.Charging,
                    BatterySourceKind.Earbuds,
                    charging: true
                ),
                Row(
                    "mouse",
                    "Mouse",
                    100,
                    BatteryStatus.Charging,
                    BatterySourceKind.Mouse,
                    charging: true
                ),
                Row("headset", "Headset", 20, BatteryStatus.Discharging, BatterySourceKind.Headset),
            ],
            BatteryWidgetOptions.Default
        );

    public static BatteryWidgetModel PanelNamed() =>
        new(
            [
                Row("laptop", "Laptop", 76, BatteryStatus.Discharging, BatterySourceKind.System),
                Row("keyboard", "Keyboard", 58, BatteryStatus.Discharging, BatterySourceKind.Keyboard),
                Row(
                    "controller",
                    "Controller",
                    34,
                    BatteryStatus.Charging,
                    BatterySourceKind.Controller,
                    charging: true
                ),
            ],
            BatteryWidgetOptions.Default with
            {
                Title = "Batteries",
                ShowNames = true,
            }
        );

    public static BatteryWidgetModel PanelList() =>
        new(
            [
                Row(
                    "mouse",
                    "Mouse",
                    82,
                    BatteryStatus.Charging,
                    BatterySourceKind.Mouse,
                    charging: true,
                    timeToFull: "0:35"
                ),
                Row(
                    "phone",
                    "Phone",
                    47,
                    BatteryStatus.Discharging,
                    BatterySourceKind.Phone,
                    trend: "-13%/1h"
                ),
                Row("headset", "Headset", 100, BatteryStatus.Full, BatterySourceKind.Headset),
            ],
            BatteryWidgetOptions.Default with
            {
                Layout = BatteryWidgetLayout.List,
            }
        );

    public static BatteryWidgetModel PanelLow() =>
        new(
            [
                Row("mouse", "Mouse", 12, BatteryStatus.Discharging, BatterySourceKind.Mouse),
                Row("keyboard", "Keyboard", 6, BatteryStatus.Discharging, BatterySourceKind.Keyboard),
                Row("phone", "Phone", 58, BatteryStatus.Discharging, BatterySourceKind.Phone),
                new(
                    "pen",
                    "Pen",
                    64,
                    BatteryStatus.Discharging,
                    Charging: false,
                    Stale: true,
                    TimeToFull: null,
                    Kind: BatterySourceKind.Pen
                ),
            ],
            BatteryWidgetOptions.Default
        );

    public static BatteryWidgetModel PanelEmpty() =>
        new([], BatteryWidgetOptions.Default with { Title = "Batteries" });

    public static BatteryWidgetModel TileCharging() =>
        new(
            [
                Row(
                    "mouse",
                    "Mouse",
                    82,
                    BatteryStatus.Charging,
                    BatterySourceKind.Mouse,
                    charging: true,
                    timeToFull: "0:35"
                ),
            ],
            BatteryWidgetOptions.Default
        );

    public static BatteryWidgetModel TileDischarging() =>
        new(
            [
                Row(
                    "phone",
                    "Phone",
                    47,
                    BatteryStatus.Discharging,
                    BatterySourceKind.Phone,
                    trend: "-13%/1h"
                ),
            ],
            BatteryWidgetOptions.Default
        );

    public static BatteryWidgetModel TileLow() =>
        new(
            [Row("headset", "Headset", 9, BatteryStatus.Discharging, BatterySourceKind.Headset)],
            BatteryWidgetOptions.Default
        );

    private static BatteryWidgetRow Row(
        string id,
        string name,
        int percent,
        BatteryStatus status,
        BatterySourceKind kind,
        bool charging = false,
        string? timeToFull = null,
        string? trend = null
    ) => new(id, name, percent, status, charging, Stale: false, timeToFull, trend, kind);
}
