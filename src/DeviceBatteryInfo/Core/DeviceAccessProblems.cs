using System.Collections.Concurrent;

namespace DeviceBatteryInfo.Core;

// Device entries whose hardware is connected but that the operating system refuses to open, keyed by device id.
public sealed class DeviceAccessProblems
{
    private readonly ConcurrentDictionary<string, byte> _blocked = new(StringComparer.Ordinal);

    // True only when the entry was not blocked before, so a caller logs once per episode.
    internal bool MarkBlocked(string deviceId) => _blocked.TryAdd(deviceId, 0);

    internal void MarkReachable(string deviceId) => _blocked.TryRemove(deviceId, out _);

    internal bool IsBlocked(string deviceId) => _blocked.ContainsKey(deviceId);
}
