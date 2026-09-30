using Serilog;

namespace DeviceBatteryInfo.Sources.Bluetooth;

internal sealed class MacBluetoothBatteryReader : IBluetoothBatteryReader, IDisposable
{
    private static readonly TimeSpan SnapshotLifetime = TimeSpan.FromSeconds(5);

    private readonly ILogger _logger;
    private readonly Func<CancellationToken, Task<string>> _runSystemProfiler;
    private readonly Func<CancellationToken, Task<string>> _runPmsetAccessories;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _accessoriesFailed;
    private IReadOnlyList<(string Name, string? RawBattery)>? _snapshot;
    private long _snapshotTimestamp;

    public MacBluetoothBatteryReader(ILogger logger)
        : this(logger, RunSystemProfilerAsync, RunPmsetAccessoriesAsync) { }

    internal MacBluetoothBatteryReader(
        ILogger logger,
        Func<CancellationToken, Task<string>> runSystemProfiler,
        Func<CancellationToken, Task<string>> runPmsetAccessories,
        TimeProvider? timeProvider = null
    )
    {
        _logger = logger;
        _runSystemProfiler = runSystemProfiler;
        _runPmsetAccessories = runPmsetAccessories;
        _time = timeProvider ?? TimeProvider.System;
    }

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
            Interlocked.Exchange(ref _accessoriesFailed, 0);
            return accessories
                .DistinctBy(a => a.Name, StringComparer.Ordinal)
                .ToDictionary(a => a.Name, a => a.RawBattery, StringComparer.Ordinal);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // The picker fetches outside the snapshot gate, so two failures can race to log.
            if (Interlocked.Exchange(ref _accessoriesFailed, 1) == 0)
            {
                _logger.Warning(exception, "The Bluetooth accessory battery levels could not be read.");
            }

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
