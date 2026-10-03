using System.Globalization;
using System.Text.Json;

namespace DeviceBatteryInfo.Sources.Bluetooth;

// The busctl --json rendering of BlueZ's GetManagedObjects. A device gains org.bluez.Battery1 next to its
// org.bluez.Device1 once BlueZ or a battery provider (PipeWire for headsets) knows a level.
internal static class BlueZDeviceParser
{
    private const string DeviceInterface = "org.bluez.Device1";
    private const string BatteryInterface = "org.bluez.Battery1";

    // Alias is the name the desktop shows and the user can rename; BlueZ fills it from Name otherwise.
    public static IReadOnlyList<(string Name, string? RawBattery)> ParseConnected(string json)
    {
        using var document = JsonDocument.Parse(json);
        var devices = new List<(string Name, string? RawBattery)>();

        if (
            !document.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array
        )
        {
            return devices;
        }

        foreach (var objects in data.EnumerateArray().Where(o => o.ValueKind == JsonValueKind.Object))
        {
            foreach (var entry in objects.EnumerateObject())
            {
                if (
                    !entry.Value.TryGetProperty(DeviceInterface, out var device)
                    || Value(device, "Connected")?.ValueKind != JsonValueKind.True
                    || (String(device, "Alias") ?? String(device, "Name")) is not { Length: > 0 } name
                )
                {
                    continue;
                }

                devices.Add((name, Percentage(entry.Value)));
            }
        }

        return devices;
    }

    private static string? Percentage(JsonElement deviceObject) =>
        deviceObject.TryGetProperty(BatteryInterface, out var battery)
        && Value(battery, "Percentage") is { ValueKind: JsonValueKind.Number } percentage
        && percentage.TryGetInt32(out var value)
            ? value.ToString(CultureInfo.InvariantCulture)
            : null;

    private static string? String(JsonElement properties, string name) =>
        Value(properties, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    // busctl wraps every variant as { "type": ..., "data": ... }.
    private static JsonElement? Value(JsonElement properties, string name) =>
        properties.TryGetProperty(name, out var variant) && variant.TryGetProperty("data", out var value)
            ? value
            : null;
}
