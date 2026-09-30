using System.Text.Json;

namespace DeviceBatteryInfo.Sources.Bluetooth;

internal static class SystemProfilerBluetoothParser
{
    // device_not_connected entries keep a stale cached battery level, so they are never read.
    public static IReadOnlyList<(string Name, string? RawBattery)> ParseConnected(string json)
    {
        using var document = JsonDocument.Parse(json);
        var devices = new List<(string Name, string? RawBattery)>();

        if (!document.RootElement.TryGetProperty("SPBluetoothDataType", out var controllers))
        {
            return devices;
        }

        foreach (var controller in controllers.EnumerateArray())
        {
            if (!controller.TryGetProperty("device_connected", out var connected))
            {
                continue;
            }

            foreach (var entry in connected.EnumerateArray())
            {
                foreach (var device in entry.EnumerateObject())
                {
                    devices.Add((device.Name, PickBattery(device.Value)));
                }
            }
        }

        return devices;
    }

    private static string? PickBattery(JsonElement device)
    {
        if (Read(device, "device_batteryLevelMain") is { } main && BluetoothBatteryParser.ParsePercent(main) is not null)
        {
            return main;
        }

        var sides = new[] { Read(device, "device_batteryLevelLeft"), Read(device, "device_batteryLevelRight") }
            .Where(raw => BluetoothBatteryParser.ParsePercent(raw) is not null)
            .ToArray();

        return sides.Length == 0
            ? null
            : sides.MinBy(raw => BluetoothBatteryParser.ParsePercent(raw));
    }

    private static string? Read(JsonElement device, string property) =>
        device.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
