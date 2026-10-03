using System.ComponentModel;
using System.Diagnostics;
using MacroDeck.Sdk.Issues;

namespace DeviceBatteryInfo;

public sealed partial class BatteryIntegration : IIntegrationIssueProvider
{
    internal const string LinuxDeviceAccessIssueId = "linux-device-access";

    internal const string LinuxSetupGuideUrl =
        "https://github.com/PyFlat/Device-Battery-Info/blob/main/docs/linux-setup.md";

    // Polled by the host to render the badge, so it only reads state the poll loop already recorded.
    public Task<IReadOnlyList<IntegrationIssue>> GetIssuesAsync(CancellationToken cancellationToken = default)
    {
        var blocked = OperatingSystem.IsLinux()
            ? _catalog.Devices.Where(d => _accessProblems.IsBlocked(d.Id)).Select(d => d.DisplayName).ToArray()
            : [];

        IReadOnlyList<IntegrationIssue> issues =
            blocked.Length == 0
                ? []
                :
                [
                    new IntegrationIssue
                    {
                        Id = LinuxDeviceAccessIssueId,
                        Title = Strings.Issues.LinuxDeviceAccess.Title(),
                        Description = Strings.Issues.LinuxDeviceAccess.Description(string.Join(", ", blocked)),
                        Severity = IntegrationIssueSeverity.Error,
                        ActionLabel = Strings.Issues.LinuxDeviceAccess.Action(),
                    },
                ];
        return Task.FromResult(issues);
    }

    // Tests replace it so resolving the issue does not open a real browser.
    internal Action<string> OpenInBrowser { get; set; } = OpenWithDesktop;

    // The SDK has no follow-up that opens a link, but the plugin runs in the user's desktop session, so it
    // opens the guide itself. The issue clears on the next poll once the device opens.
    public Task<IssueResolution> ResolveIssueAsync(string issueId, CancellationToken cancellationToken = default)
    {
        if (issueId != LinuxDeviceAccessIssueId)
        {
            return Task.FromResult(IssueResolution.Failed(Strings.Issues.Unknown()));
        }

        try
        {
            OpenInBrowser(LinuxSetupGuideUrl);
            return Task.FromResult(IssueResolution.Ok(Strings.Issues.LinuxDeviceAccess.Opened()));
        }
        catch (Exception exception)
            when (exception is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            _logger.Warning(exception, "Could not open the Linux setup guide {Url}.", LinuxSetupGuideUrl);
            return Task.FromResult(
                IssueResolution.Failed(Strings.Issues.LinuxDeviceAccess.OpenFailed(LinuxSetupGuideUrl))
            );
        }
    }

    // No shell and no PATH lookup: the URL is one argument to an absolute xdg-open, like every other tool.
    private static void OpenWithDesktop(string url)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("The setup guide is only opened on Linux.");
        }

        var start = new ProcessStartInfo("/usr/bin/xdg-open") { UseShellExecute = false };
        start.ArgumentList.Add(url);
        using var browser = Process.Start(start);
    }
}
