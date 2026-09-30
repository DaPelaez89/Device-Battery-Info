using DeviceBatteryInfo.Core;
using DeviceBatteryInfo.Sources.Bluetooth;
using DeviceBatteryInfo.Sources.SystemBattery;
using NUnit.Framework;

namespace DeviceBatteryInfo.Tests;

[TestFixture]
public sealed class PmsetBatteryParserTests
{
    private static string Output(string line, string source = "Battery Power") =>
        $"Now drawing from '{source}'\n -InternalBattery-0 (id=38273123)\t{line}\n";

    [Test]
    public void Reads_a_discharging_battery_with_time_remaining()
    {
        var reading = PmsetBatteryParser.Parse(
            Output("80%; discharging; 1:43 remaining present: true")
        );

        Assert.That(reading, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reading.Percent, Is.EqualTo(80));
            Assert.That(reading.Status, Is.EqualTo(BatteryStatus.Discharging));
            Assert.That(reading.TimeToEmpty, Is.EqualTo(new TimeSpan(1, 43, 0)));
            Assert.That(reading.TimeToFull, Is.Null);
        }
    }

    [Test]
    public void Reads_a_charging_battery_with_time_to_full()
    {
        var reading = PmsetBatteryParser.Parse(
            Output("64%; charging; 0:52 remaining present: true", "AC Power")
        );

        Assert.That(reading, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reading.Status, Is.EqualTo(BatteryStatus.Charging));
            Assert.That(reading.TimeToFull, Is.EqualTo(new TimeSpan(0, 52, 0)));
            Assert.That(reading.TimeToEmpty, Is.Null);
        }
    }

    [Test]
    public void Finishing_charge_is_still_charging()
    {
        var reading = PmsetBatteryParser.Parse(
            Output("97%; finishing charge; 0:10 remaining present: true", "AC Power")
        );

        Assert.That(reading?.Status, Is.EqualTo(BatteryStatus.Charging));
    }

    [Test]
    public void A_missing_estimate_leaves_the_time_empty()
    {
        var reading = PmsetBatteryParser.Parse(
            Output("55%; discharging; (no estimate) present: true")
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reading?.Percent, Is.EqualTo(55));
            Assert.That(reading?.TimeToEmpty, Is.Null);
        }
    }

    [Test]
    public void Charged_is_full_without_a_time()
    {
        var reading = PmsetBatteryParser.Parse(
            Output("100%; charged; 0:00 remaining present: true", "AC Power")
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reading?.Status, Is.EqualTo(BatteryStatus.Full));
            Assert.That(reading?.TimeToFull, Is.Null);
        }
    }

    [Test]
    public void A_battery_held_below_full_on_ac_is_unknown_not_charging()
    {
        var reading = PmsetBatteryParser.Parse(
            Output("80%; AC attached; not charging present: true", "AC Power")
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reading?.Percent, Is.EqualTo(80));
            Assert.That(reading?.Status, Is.EqualTo(BatteryStatus.Unknown));
        }
    }

    [Test]
    public void A_battery_at_full_on_ac_that_is_not_charging_is_full()
    {
        var reading = PmsetBatteryParser.Parse(
            Output("100%; AC attached; not charging present: true", "AC Power")
        );

        Assert.That(reading?.Status, Is.EqualTo(BatteryStatus.Full));
    }

    [Test]
    public void A_desktop_without_an_internal_battery_has_none()
    {
        Assert.That(PmsetBatteryParser.Parse("Now drawing from 'AC Power'\n"), Is.Null);
    }

    [Test]
    public void An_absent_battery_has_none()
    {
        Assert.That(
            PmsetBatteryParser.Parse(Output("0%; discharging; (no estimate) present: false")),
            Is.Null
        );
    }

    [Test]
    public void Unrecognised_output_has_none()
    {
        Assert.That(PmsetBatteryParser.Parse("pmset: command not found"), Is.Null);
    }
}

[TestFixture]
public sealed class PmsetAccessoryParserTests
{
    [Test]
    public void Reads_a_wireless_accessory_from_the_real_output()
    {
        var devices = PmsetAccessoryParser.Parse(
            "Now drawing from 'Battery Power'\n -InternalBattery-0 (id=38273123)\t66%; discharging; 2:22 remaining present: true\n"
                + " -MX Master 3S B (id=2048924894)\t15%; \n"
                + " -AirPods Pro #2 Case (id=1)\t90%; discharging present: true\n"
        );

        Assert.That(
            devices,
            Is.EqualTo(
                new[] { ("MX Master 3S B", (string?)"15%"), ("AirPods Pro #2 Case", (string?)"90%") }
            )
        );
    }

    [Test]
    public void Output_without_accessories_is_empty()
    {
        Assert.That(PmsetAccessoryParser.Parse("Now drawing from 'AC Power'\n"), Is.Empty);
    }
}

[TestFixture]
public sealed class SystemProfilerBluetoothParserTests
{
    private const string Nbsp = " ";

    private static string Json(string connected, string notConnected = "[]") =>
        $$$"""
        {"SPBluetoothDataType":[{"controller_properties":{"controller_state":"attrib_on"},
        "device_connected":{{{connected}}},"device_not_connected":{{{notConnected}}}}]}
        """;

    [Test]
    public void Reads_the_main_battery_of_a_connected_device()
    {
        var devices = SystemProfilerBluetoothParser.ParseConnected(
            Json($$$"""[{"Headset":{"device_batteryLevelMain":"85{{{Nbsp}}}%","device_minorType":"Headphones"}}]""")
        );

        Assert.That(devices, Is.EqualTo(new[] { ("Headset", (string?)$"85{Nbsp}%") }));
        Assert.That(BluetoothBatteryParser.ParsePercent(devices[0].RawBattery), Is.EqualTo(85));
    }

    [Test]
    public void The_lower_earbud_is_the_battery_and_the_case_is_ignored()
    {
        var devices = SystemProfilerBluetoothParser.ParseConnected(
            Json(
                """[{"Buds":{"device_batteryLevelLeft":"80%","device_batteryLevelRight":"65%","device_batteryLevelCase":"20%"}}]"""
            )
        );

        Assert.That(BluetoothBatteryParser.ParsePercent(devices.Single().RawBattery), Is.EqualTo(65));
    }

    [Test]
    public void An_unreadable_main_level_falls_back_to_the_earbuds()
    {
        var devices = SystemProfilerBluetoothParser.ParseConnected(
            Json("""[{"Buds":{"device_batteryLevelMain":"n/a","device_batteryLevelLeft":"40%"}}]""")
        );

        Assert.That(BluetoothBatteryParser.ParsePercent(devices.Single().RawBattery), Is.EqualTo(40));
    }

    [Test]
    public void A_case_alone_is_no_battery()
    {
        var devices = SystemProfilerBluetoothParser.ParseConnected(
            Json("""[{"Buds":{"device_batteryLevelCase":"90%"}}]""")
        );

        Assert.That(devices.Single().RawBattery, Is.Null);
    }

    [Test]
    public void A_connected_device_without_battery_keys_is_still_listed()
    {
        var devices = SystemProfilerBluetoothParser.ParseConnected(
            Json("""[{"Speaker":{"device_address":"AA:BB:CC:DD:EE:FF"}}]""")
        );

        Assert.That(devices, Is.EqualTo(new[] { ("Speaker", (string?)null) }));
    }

    [Test]
    public void Disconnected_devices_keep_a_stale_battery_that_is_never_read()
    {
        var devices = SystemProfilerBluetoothParser.ParseConnected(
            Json("[]", """[{"Old buds":{"device_batteryLevelLeft":"80%"}}]""")
        );

        Assert.That(devices, Is.Empty);
    }

    [Test]
    public void No_connected_key_means_no_devices()
    {
        Assert.That(
            SystemProfilerBluetoothParser.ParseConnected(
                """{"SPBluetoothDataType":[{"device_not_connected":[]}]}"""
            ),
            Is.Empty
        );
    }

    [Test]
    public void Malformed_json_throws()
    {
        Assert.Catch<System.Text.Json.JsonException>(() =>
            SystemProfilerBluetoothParser.ParseConnected("not json")
        );
    }
}

[TestFixture]
public sealed class MacBluetoothBatteryReaderTests
{
    private const string Connected =
        """{"SPBluetoothDataType":[{"device_connected":[{"Headset":{"device_batteryLevelMain":"70%"}}]}]}""";

    private static Task<string> NoAccessories(CancellationToken cancellationToken) =>
        Task.FromResult("Now drawing from 'Battery Power'\n");

    private sealed class Runner(Func<int, CancellationToken, Task<string>> run)
    {
        private int _calls;

        public int Calls => _calls;

        public Task<string> RunAsync(CancellationToken cancellationToken) =>
            run(Interlocked.Increment(ref _calls), cancellationToken);
    }

    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;

        public override long GetTimestamp() => _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public void Advance(TimeSpan by) => _ticks += by.Ticks;
    }

    [Test]
    public async Task Concurrent_reads_share_one_process()
    {
        var runner = new Runner(async (_, ct) =>
        {
            await Task.Delay(50, ct);
            return Connected;
        });
        using var reader = new MacBluetoothBatteryReader(Serilog.Core.Logger.None, runner.RunAsync, NoAccessories);

        var results = await Task.WhenAll(
            Enumerable
                .Range(0, 4)
                .Select(_ => reader.ReadRawAsync("Headset", CancellationToken.None))
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(runner.Calls, Is.EqualTo(1));
            Assert.That(results, Is.All.EqualTo("70%"));
        }
    }

    [Test]
    public async Task A_snapshot_expires_after_its_lifetime()
    {
        var time = new ManualTime();
        var runner = new Runner((_, _) => Task.FromResult(Connected));
        using var reader = new MacBluetoothBatteryReader(Serilog.Core.Logger.None, runner.RunAsync, NoAccessories, time);

        await reader.ReadRawAsync("Headset", CancellationToken.None);
        await reader.ReadRawAsync("Headset", CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(6));
        await reader.ReadRawAsync("Headset", CancellationToken.None);

        Assert.That(runner.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task A_failed_fetch_is_not_cached_and_the_next_caller_retries()
    {
        var runner = new Runner((call, _) =>
            call == 1
                ? throw new InvalidOperationException("system_profiler exited with 1")
                : Task.FromResult(Connected)
        );
        using var reader = new MacBluetoothBatteryReader(Serilog.Core.Logger.None, runner.RunAsync, NoAccessories);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await reader.ReadRawAsync("Headset", CancellationToken.None)
        );
        var raw = await reader.ReadRawAsync("Headset", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(raw, Is.EqualTo("70%"));
            Assert.That(runner.Calls, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task A_cancelled_caller_does_not_fail_the_one_waiting_behind_it()
    {
        var release = new TaskCompletionSource();
        var runner = new Runner(async (call, ct) =>
        {
            if (call == 1)
            {
                await release.Task.WaitAsync(ct);
            }

            return Connected;
        });
        using var reader = new MacBluetoothBatteryReader(Serilog.Core.Logger.None, runner.RunAsync, NoAccessories);
        using var cancel = new CancellationTokenSource();

        var first = reader.ReadRawAsync("Headset", cancel.Token);
        var second = reader.ReadRawAsync("Headset", CancellationToken.None);
        await cancel.CancelAsync();

        Assert.ThrowsAsync<TaskCanceledException>(async () => await first);
        Assert.That(await second, Is.EqualTo("70%"));
    }

    [Test]
    public async Task The_picker_always_fetches_fresh()
    {
        var runner = new Runner((_, _) => Task.FromResult(Connected));
        using var reader = new MacBluetoothBatteryReader(Serilog.Core.Logger.None, runner.RunAsync, NoAccessories);

        await reader.ReadRawAsync("Headset", CancellationToken.None);
        var devices = await reader.ListDevicesAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(runner.Calls, Is.EqualTo(2));
            Assert.That(devices.Select(d => d.Name), Is.EqualTo(new[] { "Headset" }));
        }
    }

    [Test]
    public async Task A_connected_device_without_a_profiler_battery_takes_it_from_the_accessory_list()
    {
        var runner = new Runner((_, _) =>
            Task.FromResult("""{"SPBluetoothDataType":[{"device_connected":[{"Mouse":{"device_minorType":"Mouse"}}]}]}""")
        );
        using var reader = new MacBluetoothBatteryReader(
            Serilog.Core.Logger.None,
            runner.RunAsync,
            _ => Task.FromResult("Now drawing from 'Battery Power'\n -Mouse (id=2048924894)\t15%; \n")
        );

        var devices = await reader.ListDevicesAsync(CancellationToken.None);

        Assert.That(devices, Is.EqualTo(new[] { ("Mouse", (string?)"15%") }));
        Assert.That(await reader.ReadRawAsync("Mouse", CancellationToken.None), Is.EqualTo("15%"));
    }

    [Test]
    public async Task The_profiler_battery_wins_and_an_accessory_that_is_not_connected_is_never_listed()
    {
        var runner = new Runner((_, _) => Task.FromResult(Connected));
        using var reader = new MacBluetoothBatteryReader(
            Serilog.Core.Logger.None,
            runner.RunAsync,
            _ =>
                Task.FromResult(
                    " -Headset (id=1)\t99%; \n -AirPods #2 (id=2)\t80%; charging\n -AirPods #2 Case (id=3)\t90%; discharging\n"
                )
        );

        var devices = await reader.ListDevicesAsync(CancellationToken.None);

        Assert.That(devices, Is.EqualTo(new[] { ("Headset", (string?)"70%") }));
    }

    [Test]
    public async Task A_failing_accessory_command_keeps_the_profiler_list()
    {
        var runner = new Runner((_, _) => Task.FromResult(Connected));
        using var reader = new MacBluetoothBatteryReader(
            Serilog.Core.Logger.None,
            runner.RunAsync,
            _ => throw new InvalidOperationException("pmset exited with 1")
        );

        Assert.That(await reader.ReadRawAsync("Headset", CancellationToken.None), Is.EqualTo("70%"));
    }

    [Test]
    public async Task A_device_that_is_not_connected_reads_as_nothing()
    {
        var runner = new Runner((_, _) => Task.FromResult(Connected));
        using var reader = new MacBluetoothBatteryReader(Serilog.Core.Logger.None, runner.RunAsync, NoAccessories);

        Assert.That(await reader.ReadRawAsync("Other", CancellationToken.None), Is.Null);
    }
}

[TestFixture]
public sealed class SystemBatterySourceTests
{
    private sealed class FakeReader(BatteryReading? reading) : ISystemPowerReader
    {
        public int Calls { get; private set; }

        public Func<CancellationToken, ValueTask<BatteryReading?>>? Behaviour { get; set; }

        public ValueTask<BatteryReading?> ReadAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Behaviour?.Invoke(cancellationToken) ?? ValueTask.FromResult(reading);
        }
    }

    private static readonly BatterySlot Slot = new(
        "laptop",
        "Laptop",
        BatterySourceKind.System,
        DeviceType.System
    );

    private static DeviceCatalog Catalog()
    {
        var catalog = new DeviceCatalog();
        catalog.Set([Slot]);
        return catalog;
    }

    [Test]
    public void The_source_throws_when_the_computer_has_no_battery()
    {
        var source = new SystemBatterySource(new FakeReader(null), Slot);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await source.ReadAsync(CancellationToken.None)
        );
    }

    [Test]
    public async Task The_source_returns_the_reader_value()
    {
        var reading = new BatteryReading { Percent = 42, Status = BatteryStatus.Discharging };
        var source = new SystemBatterySource(new FakeReader(reading), Slot);

        Assert.That(await source.ReadAsync(CancellationToken.None), Is.SameAs(reading));
    }

    [Test]
    public async Task A_computer_without_a_battery_offers_no_source_and_is_checked_again()
    {
        var reader = new FakeReader(null);
        var provider = new SystemBatterySourceProvider(reader, Catalog(), Serilog.Core.Logger.None);

        Assert.That(await provider.DiscoverAsync(CancellationToken.None), Is.Empty);
        Assert.That(await provider.DiscoverAsync(CancellationToken.None), Is.Empty);
        Assert.That(reader.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task Once_a_battery_was_seen_discovery_no_longer_reads_it()
    {
        var reader = new FakeReader(new BatteryReading { Percent = 50 });
        var provider = new SystemBatterySourceProvider(reader, Catalog(), Serilog.Core.Logger.None);

        Assert.That(await provider.DiscoverAsync(CancellationToken.None), Has.Count.EqualTo(1));
        Assert.That(await provider.DiscoverAsync(CancellationToken.None), Has.Count.EqualTo(1));
        Assert.That(reader.Calls, Is.EqualTo(1));
    }

    [Test]
    public async Task A_failing_reader_offers_no_source_and_does_not_throw_out_of_discovery()
    {
        var reader = new FakeReader(null)
        {
            Behaviour = _ => throw new InvalidOperationException("pmset exited with 1"),
        };
        var provider = new SystemBatterySourceProvider(reader, Catalog(), Serilog.Core.Logger.None);

        Assert.That(await provider.DiscoverAsync(CancellationToken.None), Is.Empty);
    }

    [Test]
    public async Task A_hung_reader_ends_at_the_probe_timeout_instead_of_stalling_discovery()
    {
        var reader = new FakeReader(null)
        {
            Behaviour = async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return null;
            },
        };
        var provider = new SystemBatterySourceProvider(
            reader,
            Catalog(),
            Serilog.Core.Logger.None,
            TimeSpan.FromMilliseconds(100)
        );

        Assert.That(await provider.DiscoverAsync(CancellationToken.None), Is.Empty);
    }

    [Test]
    public async Task A_caller_cancellation_still_cancels_discovery()
    {
        var reader = new FakeReader(null)
        {
            Behaviour = async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return null;
            },
        };
        var provider = new SystemBatterySourceProvider(reader, Catalog(), Serilog.Core.Logger.None);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await provider.DiscoverAsync(cancel.Token)
        );
    }
}
