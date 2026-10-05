internal sealed class TrustProtocol() : HidProtocol("Trust", vendorId: 0x145F, reportLength: 20)
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
            isComplete: r => r.Length > 4 && r[1] == 0x10,
            cancellationToken);

        return BatteryReading.FromPercent(response[4]);
    }
}
