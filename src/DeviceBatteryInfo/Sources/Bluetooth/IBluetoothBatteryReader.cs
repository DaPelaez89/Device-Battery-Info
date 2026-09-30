namespace DeviceBatteryInfo.Sources.Bluetooth;

internal interface IBluetoothBatteryReader
{
    Task<string?> ReadRawAsync(string friendlyName, CancellationToken cancellationToken);

    Task<IReadOnlyList<(string Name, string? RawBattery)>> ListDevicesAsync(
        CancellationToken cancellationToken
    );
}
