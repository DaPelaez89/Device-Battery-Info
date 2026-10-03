namespace DeviceBatteryInfo.Sources.Bluetooth;

internal sealed class BlueZBatteryReader : IBluetoothBatteryReader, IDisposable
{
    private readonly Func<CancellationToken, Task<string>> _runBusctl;
    private readonly BluetoothSnapshot _snapshot;

    public BlueZBatteryReader()
        : this(RunBusctlAsync) { }

    internal BlueZBatteryReader(
        Func<CancellationToken, Task<string>> runBusctl,
        TimeProvider? timeProvider = null
    )
    {
        _runBusctl = runBusctl;
        _snapshot = new BluetoothSnapshot(FetchAsync, timeProvider);
    }

    public Task<string?> ReadRawAsync(string friendlyName, CancellationToken cancellationToken) =>
        _snapshot.ReadRawAsync(friendlyName, cancellationToken);

    public Task<IReadOnlyList<(string Name, string? RawBattery)>> ListDevicesAsync(
        CancellationToken cancellationToken
    ) => FetchAsync(cancellationToken);

    private async Task<IReadOnlyList<(string Name, string? RawBattery)>> FetchAsync(
        CancellationToken cancellationToken
    ) =>
    [
        .. BlueZDeviceParser
            .ParseConnected(await _runBusctl(cancellationToken))
            .DistinctBy(d => d.Name, StringComparer.Ordinal),
    ];

    public void Dispose() => _snapshot.Dispose();

    private static Task<string> RunBusctlAsync(CancellationToken cancellationToken) =>
        ExternalProcess.RunAsync(
            "/usr/bin/busctl",
            "busctl",
            [
                "--system",
                "--json=short",
                "call",
                "org.bluez",
                "/",
                "org.freedesktop.DBus.ObjectManager",
                "GetManagedObjects",
            ],
            cancellationToken
        );
}
