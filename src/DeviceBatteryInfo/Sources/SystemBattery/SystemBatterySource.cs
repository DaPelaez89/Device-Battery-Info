using DeviceBatteryInfo.Core;
using Serilog;

namespace DeviceBatteryInfo.Sources.SystemBattery;

internal sealed class SystemBatterySource(ISystemPowerReader reader, BatterySlot slot)
    : IBatterySource
{
    public string Id { get; } = slot.Id;

    public string DisplayName { get; } = slot.DisplayName;

    public BatterySourceKind Kind => BatterySourceKind.System;

    public async ValueTask<BatteryReading> ReadAsync(CancellationToken cancellationToken) =>
        await reader.ReadAsync(cancellationToken)
        ?? throw new InvalidOperationException("This computer reports no battery.");
}

internal sealed class SystemBatterySourceProvider(
    ISystemPowerReader reader,
    DeviceCatalog catalog,
    ILogger logger
) : IBatterySourceProvider
{
    internal TimeSpan ProbeTimeout { get; init; } = TimeSpan.FromSeconds(5);

    private bool _hasBattery;
    private bool _probeFailed;

    public async ValueTask<IReadOnlyList<IBatterySource>> DiscoverAsync(
        CancellationToken cancellationToken
    )
    {
        if (!_hasBattery && !await ProbeAsync(cancellationToken))
        {
            return [];
        }

        return
        [
            .. catalog
                .Devices.Where(d => d.Type == DeviceType.System)
                .Select(IBatterySource (d) => new SystemBatterySource(reader, d)),
        ];
    }

    // A failing or hung reader must not stall discovery for the other providers, so it counts as no battery yet.
    private async Task<bool> ProbeAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);
        try
        {
            _hasBattery = await reader.ReadAsync(timeout.Token) is not null;
            _probeFailed = false;
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (!_probeFailed)
            {
                logger.Warning(exception, "The system battery could not be checked.");
            }

            _probeFailed = true;
        }

        return _hasBattery;
    }
}
