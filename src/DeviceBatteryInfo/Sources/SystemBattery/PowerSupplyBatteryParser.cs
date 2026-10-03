using System.Globalization;
using DeviceBatteryInfo.Core;

namespace DeviceBatteryInfo.Sources.SystemBattery;

internal static class PowerSupplyBatteryParser
{
    private sealed record Battery(int? Percent, BatteryStatus Status, long? Now, long? Full, long? Rate, bool InEnergy);

    // Each text is one /sys/class/power_supply/<name>/uevent. A laptop can have more than one battery.
    public static BatteryReading? Parse(IEnumerable<string> uevents)
    {
        var batteries = uevents.Select(ParseUevent).Select(ToBattery).OfType<Battery>().ToArray();
        if (batteries.Length == 0)
        {
            return null;
        }

        var status = CombinedStatus(batteries);
        long? now = null;
        long? full = null;
        long? rate = null;

        // Energy (uWh, uW) and charge (uAh, uA) cannot be summed together, so mixed batteries skip the totals.
        if (batteries.All(b => b.Now is not null && b.Full is > 0 && b.InEnergy == batteries[0].InEnergy))
        {
            now = batteries.Sum(b => b.Now!.Value);
            full = batteries.Sum(b => b.Full!.Value);
            rate = batteries.All(b => b.Rate is not null) ? batteries.Sum(b => b.Rate!.Value) : null;
        }

        int? percent = batteries.Length == 1 && batteries[0].Percent is { } single
            ? single
            : now is not null && full is not null
                ? (int)Math.Round(100.0 * now.Value / full.Value)
                : Average(batteries.Select(b => b.Percent));
        percent = percent is { } value ? Math.Clamp(value, 0, 100) : null;

        return new BatteryReading
        {
            Percent = percent,
            Status = status == BatteryStatus.Unknown && percent >= 100 ? BatteryStatus.Full : status,
            TimeToEmpty = status == BatteryStatus.Discharging ? Hours(now, rate) : null,
            TimeToFull = status == BatteryStatus.Charging ? Hours(full - now, rate) : null,
        };
    }

    internal static Dictionary<string, string> ParseUevent(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in text.Split('\n'))
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0)
            {
                values[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }

        return values;
    }

    // SCOPE=Device is a peripheral (a mouse, a controller) that the kernel drives, not this computer's battery.
    private static Battery? ToBattery(Dictionary<string, string> values)
    {
        if (
            values.GetValueOrDefault("POWER_SUPPLY_TYPE") != "Battery"
            || values.GetValueOrDefault("POWER_SUPPLY_SCOPE") == "Device"
            || values.GetValueOrDefault("POWER_SUPPLY_PRESENT") == "0"
        )
        {
            return null;
        }

        var inEnergy = values.ContainsKey("POWER_SUPPLY_ENERGY_NOW");
        var prefix = inEnergy ? "POWER_SUPPLY_ENERGY_" : "POWER_SUPPLY_CHARGE_";
        var now = Number(values, prefix + "NOW");
        var full = Number(values, prefix + "FULL");

        // Some drivers report the discharge current or power as a negative number.
        var rate = Number(values, inEnergy ? "POWER_SUPPLY_POWER_NOW" : "POWER_SUPPLY_CURRENT_NOW") is { } raw
            ? Math.Abs(raw)
            : (long?)null;

        var percent = (int?)Number(values, "POWER_SUPPLY_CAPACITY")
            ?? (now is not null && full is > 0 ? (int)Math.Round(100.0 * now.Value / full.Value) : null);

        var status = values.GetValueOrDefault("POWER_SUPPLY_STATUS") switch
        {
            "Charging" => BatteryStatus.Charging,
            "Discharging" => BatteryStatus.Discharging,
            "Full" => BatteryStatus.Full,
            // A charge threshold or an idle dock holds the battery on AC without charging it.
            _ => BatteryStatus.Unknown,
        };

        return new Battery(percent, status, now, full, rate, inEnergy);
    }

    private static BatteryStatus CombinedStatus(Battery[] batteries) =>
        batteries.Any(b => b.Status == BatteryStatus.Charging) ? BatteryStatus.Charging
        : batteries.Any(b => b.Status == BatteryStatus.Discharging) ? BatteryStatus.Discharging
        : batteries.All(b => b.Status == BatteryStatus.Full) ? BatteryStatus.Full
        : BatteryStatus.Unknown;

    private static TimeSpan? Hours(long? amount, long? rate) =>
        amount is > 0 && rate is > 0 ? TimeSpan.FromHours((double)amount.Value / rate.Value) : null;

    private static int? Average(IEnumerable<int?> percents)
    {
        var known = percents.OfType<int>().ToArray();
        return known.Length == 0 ? null : (int)Math.Round(known.Average());
    }

    private static long? Number(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var raw)
        && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
