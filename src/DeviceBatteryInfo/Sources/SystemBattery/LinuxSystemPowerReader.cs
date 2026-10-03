using DeviceBatteryInfo.Core;

namespace DeviceBatteryInfo.Sources.SystemBattery;

internal sealed class LinuxSystemPowerReader : ISystemPowerReader
{
    private const string PowerSupplyDirectory = "/sys/class/power_supply";

    public async ValueTask<BatteryReading?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(PowerSupplyDirectory))
        {
            return null;
        }

        var uevents = new List<string>();
        foreach (var supply in Directory.EnumerateFileSystemEntries(PowerSupplyDirectory))
        {
            try
            {
                uevents.Add(await File.ReadAllTextAsync(Path.Combine(supply, "uevent"), cancellationToken));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A supply can vanish between the listing and the read (a peripheral that disconnected).
            }
        }

        return PowerSupplyBatteryParser.Parse(uevents);
    }
}
