using System.Globalization;
using System.Text.RegularExpressions;
using DeviceBatteryInfo.Core;

namespace DeviceBatteryInfo.Sources.SystemBattery;

internal static partial class PmsetBatteryParser
{
    [GeneratedRegex(
        @"InternalBattery\S*\s.*?(?<percent>\d+)%;\s*(?<state>[^;]+?)(?:;\s*(?<detail>[^;]*?))?\s+present:\s*(?<present>true|false)",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex BatteryLine();

    [GeneratedRegex(@"(?<hours>\d+):(?<minutes>\d{2})\s+remaining", RegexOptions.IgnoreCase)]
    private static partial Regex Remaining();

    public static BatteryReading? Parse(string output)
    {
        var match = BatteryLine().Match(output);
        if (!match.Success || match.Groups["present"].Value != "true")
        {
            return null;
        }

        var percent = Math.Clamp(int.Parse(match.Groups["percent"].Value, CultureInfo.InvariantCulture), 0, 100);
        var state = match.Groups["state"].Value.Trim().ToLowerInvariant();
        var remaining = ParseRemaining(match.Groups["detail"].Value);

        // Optimized Battery Charging holds a battery on AC below full without charging it.
        var status = state switch
        {
            "discharging" => BatteryStatus.Discharging,
            "charging" or "finishing charge" => BatteryStatus.Charging,
            "charged" => BatteryStatus.Full,
            "ac attached" => percent >= 100 ? BatteryStatus.Full : BatteryStatus.Unknown,
            _ => BatteryStatus.Unknown,
        };

        return new BatteryReading
        {
            Percent = percent,
            Status = status,
            TimeToEmpty = status == BatteryStatus.Discharging ? remaining : null,
            TimeToFull = status == BatteryStatus.Charging ? remaining : null,
        };
    }

    private static TimeSpan? ParseRemaining(string detail)
    {
        var match = Remaining().Match(detail);
        if (!match.Success)
        {
            return null;
        }

        var time = new TimeSpan(
            int.Parse(match.Groups["hours"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture),
            0
        );
        return time == TimeSpan.Zero ? null : time;
    }
}
