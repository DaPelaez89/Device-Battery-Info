using DeviceBatteryInfo.Core;

namespace DeviceBatteryInfo.Sources.SystemBattery;

internal sealed class MacSystemPowerReader : ISystemPowerReader
{
    public async ValueTask<BatteryReading?> ReadAsync(CancellationToken cancellationToken) =>
        PmsetBatteryParser.Parse(
            await ExternalProcess.RunAsync(
                "/usr/bin/pmset",
                "pmset",
                ["-g", "batt"],
                cancellationToken
            )
        );
}
