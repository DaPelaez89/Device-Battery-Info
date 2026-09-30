using System.Runtime.Versioning;
using HidSharp;
using Microsoft.Win32.SafeHandles;

namespace DeviceBatteryInfo.Sources.Hid;

// The report buffers hold the report id at index 0.
internal interface IFeatureChannel : IDisposable
{
    void Set(byte[] report);

    void Get(byte[] report);
}

[SupportedOSPlatform("windows")]
internal sealed class NativeFeatureChannel(SafeFileHandle handle) : IFeatureChannel
{
    public void Set(byte[] report) => NativeHid.SetFeature(handle, report);

    public void Get(byte[] report) => NativeHid.GetFeature(handle, report);

    public void Dispose() => handle.Dispose();
}

internal sealed class HidSharpFeatureChannel : IFeatureChannel
{
    private readonly HidStream _stream;

    public HidSharpFeatureChannel(string devicePath)
    {
        var device =
            DeviceList
                .Local.GetHidDevices()
                .FirstOrDefault(d =>
                    string.Equals(d.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase)
                ) ?? throw new InvalidOperationException($"HID device {devicePath} is not present.");
        _stream = device.Open();
    }

    public void Set(byte[] report) => _stream.SetFeature(report);

    public void Get(byte[] report) => _stream.GetFeature(report);

    public void Dispose() => _stream.Dispose();
}
