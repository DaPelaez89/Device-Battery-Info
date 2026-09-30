using System.Text.RegularExpressions;

namespace DeviceBatteryInfo.Sources.Bluetooth;

internal static partial class PmsetAccessoryParser
{
    [GeneratedRegex(@"^\s*-(?<name>.+?)\s+\(id=\d+\)\s+(?<percent>\d+)%", RegexOptions.Multiline)]
    private static partial Regex AccessoryLine();

    public static IReadOnlyList<(string Name, string? RawBattery)> Parse(string output) =>
        [
            .. AccessoryLine()
                .Matches(output)
                .Where(m => !m.Groups["name"].Value.StartsWith("InternalBattery", StringComparison.Ordinal))
                .Select(m => (m.Groups["name"].Value, (string?)$"{m.Groups["percent"].Value}%")),
        ];
}
