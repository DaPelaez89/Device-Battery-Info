using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeviceBatteryInfo.Core;
using DeviceBatteryInfo.Sources.Hid;

namespace DeviceBatteryInfo.Sources.Trust;

internal sealed class TrustProtocol() : HidProtocol("Trust", vendorId: 0x145F, reportLength: 64)
{
    public override IReadOnlyList<HidDeviceInfo> Devices { get; } =
    [
        new("BAYO II", 0x031E),
    ];

    public override async Task<BatteryReading> ReadAsync(
        HidChannel channel, HidDeviceInfo device, CancellationToken cancellationToken)
    {
        var response = await channel.ExchangeAsync(
            request: BuildBatteryRequest(),
            isComplete: IsResponseComplete,
            cancellationToken);

        return BatteryReading.FromPercent(response[4]);
    }

    private static byte[] BuildBatteryRequest() => [0x00, 0x02, 0x00];

    private static bool IsResponseComplete(byte[] response) => response.Length > 4;
}