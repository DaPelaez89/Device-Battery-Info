using DeviceBatteryInfo.Core;

namespace DeviceBatteryInfo.Sources.SystemBattery;

internal interface ISystemPowerReader
{
    ValueTask<BatteryReading?> ReadAsync(CancellationToken cancellationToken);
}
