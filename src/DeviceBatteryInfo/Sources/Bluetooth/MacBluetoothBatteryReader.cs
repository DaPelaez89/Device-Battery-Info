using Serilog;

namespace DeviceBatteryInfo.Sources.Bluetooth;

internal sealed class MacBluetoothBatteryReader(
    ILogger logger,
    Func<CancellationToken, Task<string>>? runSystemProfiler = null,
    Func<CancellationToken, Task<string>>? runPmsetAccessories = null,
    TimeProvider? timeProvider = null
) : IBluetoothBatteryReader, IDisposable
{
    private static readonly TimeSpan SnapshotLifetime = TimeSpan.FromSeconds(5);

    private readonly Func<CancellationToken, Task<string>> _runSystemProfiler =
        runSystemProfiler ?? RunSystemProfilerAsync;
    private readonly Func<CancellationToken, Task<string>> _runPmsetAccessories =
        runPmsetAccessories ?? RunPmsetAccessoriesAsync;
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _accessoriesFailed;
    private IReadOnlyList<(string Name, string? RawBattery)>? _snapshot;
    private long _snapshotTimestamp;

    public async Task<string?> ReadRawAsync(
        string friendlyName,
        CancellationToken cancellationToken
    )
    {
        var devices = await GetSnapshotAsync(cancellationToken);
        return devices.FirstOrDefault(d => d.Name == friendlyName).RawBattery;
    }

    public Task<IReadOnlyList<(string Name, string? RawBattery)>> ListDevicesAsync(
        CancellationToken cancellationToken
    ) => FetchAsync(cancellationToken);

    private async Task<IReadOnlyList<(string Name, string? RawBattery)>> GetSnapshotAsync(
        CancellationToken cancellationToken
    )
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (
                _snapshot is not null
                && _time.GetElapsedTime(_snapshotTimestamp) < SnapshotLifetime
            )
            {
                return _snapshot;
            }

            var fresh = await FetchAsync(cancellationToken);
            _snapshot = fresh;
            _snapshotTimestamp = _time.GetTimestamp();
            return fresh;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<(string Name, string? RawBattery)>> FetchAsync(
        CancellationToken cancellationToken
    )
    {
        var accessories = await ReadAccessoriesAsync(cancellationToken);
        return
        [
            .. SystemProfilerBluetoothParser
                .ParseConnected(await _runSystemProfiler(cancellationToken))
                .DistinctBy(d => d.Name, StringComparer.Ordinal)
                .Select(d => (d.Name, d.RawBattery ?? accessories.GetValueOrDefault(d.Name))),
        ];
    }

    private async Task<Dictionary<string, string?>> ReadAccessoriesAsync(
        CancellationToken cancellationToken
    )
    {
        try
        {
            var accessories = PmsetAccessoryParser.Parse(await _runPmsetAccessories(cancellationToken));
            _accessoriesFailed = false;
            return accessories
                .DistinctBy(a => a.Name, StringComparer.Ordinal)
                .ToDictionary(a => a.Name, a => a.RawBattery, StringComparer.Ordinal);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (!_accessoriesFailed)
            {
                logger.Warning(exception, "The Bluetooth accessory battery levels could not be read.");
            }

            _accessoriesFailed = true;
            return [];
        }
    }

    public void Dispose() => _gate.Dispose();

    private static Task<string> RunSystemProfilerAsync(CancellationToken cancellationToken) =>
        ExternalProcess.RunAsync(
            "/usr/sbin/system_profiler",
            "system_profiler",
            ["SPBluetoothDataType", "-json"],
            cancellationToken
        );

    private static Task<string> RunPmsetAccessoriesAsync(CancellationToken cancellationToken) =>
        ExternalProcess.RunAsync(
            "/usr/bin/pmset",
            "pmset",
            ["-g", "accps"],
            cancellationToken
        );
}
