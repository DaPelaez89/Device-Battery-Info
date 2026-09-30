using System.Runtime.InteropServices;
using DeviceBatteryInfo.Core;

namespace DeviceBatteryInfo.Sources.SystemBattery;

internal sealed class WindowsSystemPowerReader : ISystemPowerReader
{
    public ValueTask<BatteryReading?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!NativePowerStatusApi.GetSystemPowerStatus(out var status))
        {
            throw new InvalidOperationException(
                $"GetSystemPowerStatus failed (Win32 error {Marshal.GetLastPInvokeError()})."
            );
        }

        return ValueTask.FromResult<BatteryReading?>(
            SystemBatteryReadingFactory.HasBattery(status)
                ? SystemBatteryReadingFactory.Create(status)
                : null
        );
    }
}
