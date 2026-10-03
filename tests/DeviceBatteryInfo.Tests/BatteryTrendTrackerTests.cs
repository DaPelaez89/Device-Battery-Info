using DeviceBatteryInfo.Core;
using NUnit.Framework;

namespace DeviceBatteryInfo.Tests;

[TestFixture]
public sealed class BatteryTrendTrackerTests
{
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class FakeSource(string id) : IBatterySource
    {
        public string Id => id;

        public string DisplayName => id;

        public BatterySourceKind Kind => BatterySourceKind.Other;

        public ValueTask<BatteryReading> ReadAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(BatteryReading.Unavailable);
    }

    private static (
        BatteryRegistry Registry,
        BatteryTrendTracker Tracker,
        ManualTimeProvider Time
    ) Build()
    {
        var time = new ManualTimeProvider();
        var registry = new BatteryRegistry(time);
        var tracker = new BatteryTrendTracker(registry);
        return (registry, tracker, time);
    }

    [Test]
    public void Returns_null_before_any_history_exists()
    {
        var (registry, tracker, _) = Build();
        var source = new FakeSource("phone");
        registry.Update(source, BatteryReading.FromPercent(80, BatteryStatus.Discharging));

        registry.TryGet("phone", out var snapshot);
        Assert.That(tracker.GetTrend(snapshot), Is.Null);
    }

    [Test]
    public void Returns_null_before_the_minimum_window_has_elapsed()
    {
        var (registry, tracker, time) = Build();
        var source = new FakeSource("phone");
        registry.Update(source, BatteryReading.FromPercent(80, BatteryStatus.Discharging));

        time.Advance(TimeSpan.FromMinutes(1));
        registry.Update(source, BatteryReading.FromPercent(78, BatteryStatus.Discharging));

        registry.TryGet("phone", out var snapshot);
        Assert.That(tracker.GetTrend(snapshot), Is.Null);
    }

    [Test]
    public void Reports_the_delta_over_the_elapsed_window_once_past_the_minimum()
    {
        var (registry, tracker, time) = Build();
        var source = new FakeSource("phone");
        registry.Update(source, BatteryReading.FromPercent(93, BatteryStatus.Discharging));

        time.Advance(TimeSpan.FromHours(1));
        registry.Update(source, BatteryReading.FromPercent(80, BatteryStatus.Discharging));

        registry.TryGet("phone", out var snapshot);
        var trend = tracker.GetTrend(snapshot);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(trend, Is.Not.Null);
            Assert.That(trend!.DeltaPercent, Is.EqualTo(-13));
            Assert.That(trend.Window, Is.EqualTo(TimeSpan.FromHours(1)));
            Assert.That(trend.IsCharging, Is.False);
        }
    }

    [Test]
    public void A_charging_state_flip_starts_a_fresh_segment()
    {
        var (registry, tracker, time) = Build();
        var source = new FakeSource("mouse");
        registry.Update(source, BatteryReading.FromPercent(50, BatteryStatus.Discharging));

        time.Advance(TimeSpan.FromMinutes(10));
        registry.Update(source, BatteryReading.FromPercent(45, BatteryStatus.Discharging));

        // Charging starts, so the discharging history must not leak into the charging rate.
        registry.Update(source, BatteryReading.FromPercent(45, BatteryStatus.Charging));
        time.Advance(TimeSpan.FromMinutes(1));
        registry.Update(source, BatteryReading.FromPercent(50, BatteryStatus.Charging));

        registry.TryGet("mouse", out var snapshot);
        Assert.That(
            tracker.GetTrend(snapshot),
            Is.Null,
            "the new segment has not reached MinWindow yet"
        );
    }

    [Test]
    public void A_reading_at_the_same_percent_does_not_move_the_reference_point()
    {
        var (registry, tracker, time) = Build();
        var source = new FakeSource("phone");
        registry.Update(source, BatteryReading.FromPercent(80, BatteryStatus.Discharging));

        // Going stale and recovering fires Changed without the percent moving; the oldest sample
        // used for the window must still be the very first reading, not this one.
        time.Advance(TimeSpan.FromMinutes(30));
        registry.RecordFailure(source, staleAfter: 1);

        time.Advance(TimeSpan.FromMinutes(30));
        registry.Update(source, BatteryReading.FromPercent(75, BatteryStatus.Discharging));

        registry.TryGet("phone", out var snapshot);
        var trend = tracker.GetTrend(snapshot);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(trend, Is.Not.Null);
            Assert.That(trend!.DeltaPercent, Is.EqualTo(-5));
            Assert.That(trend.Window, Is.EqualTo(TimeSpan.FromHours(1)));
        }
    }

    [Test]
    public void Removing_the_source_drops_its_history()
    {
        var (registry, tracker, time) = Build();
        var source = new FakeSource("phone");
        registry.Update(source, BatteryReading.FromPercent(80, BatteryStatus.Discharging));
        time.Advance(TimeSpan.FromHours(1));
        registry.Update(source, BatteryReading.FromPercent(70, BatteryStatus.Discharging));

        registry.Retain(new HashSet<string>());
        registry.Update(source, BatteryReading.FromPercent(70, BatteryStatus.Discharging));

        registry.TryGet("phone", out var snapshot);
        Assert.That(
            tracker.GetTrend(snapshot),
            Is.Null,
            "history restarted after the device dropped out"
        );
    }
}

[TestFixture]
public sealed class BatteryTrendFormatterTests
{
    [Test]
    public void Formats_a_discharging_delta_against_its_actual_window()
    {
        var trend = new BatteryTrend(-13, TimeSpan.FromHours(1), IsCharging: false);
        Assert.That(BatteryTrendFormatter.FormatText(trend), Is.EqualTo("-13%/1h"));
    }

    [Test]
    public void Formats_a_charging_delta_against_its_actual_window()
    {
        var trend = new BatteryTrend(28, TimeSpan.FromMinutes(28), IsCharging: true);
        Assert.That(BatteryTrendFormatter.FormatText(trend), Is.EqualTo("+28%/30m"));
    }

    [Test]
    public void Suppresses_a_flat_reading()
    {
        var trend = new BatteryTrend(0, TimeSpan.FromHours(1), IsCharging: false);
        Assert.That(BatteryTrendFormatter.FormatText(trend), Is.Null);
    }

    [Test]
    public void Returns_null_without_a_trend()
    {
        Assert.That(BatteryTrendFormatter.FormatText(null), Is.Null);
    }

    [Test]
    public void Normalizes_the_rate_to_percent_per_hour()
    {
        var trend = new BatteryTrend(-13, TimeSpan.FromMinutes(30), IsCharging: false);
        Assert.That(BatteryTrendFormatter.PercentPerHour(trend), Is.EqualTo(-26.0));
    }

    [Test]
    public void Formats_a_delta_under_an_hour_against_its_actual_window()
    {
        var trend = new BatteryTrend(-8, TimeSpan.FromMinutes(45), IsCharging: false);
        Assert.That(BatteryTrendFormatter.FormatText(trend), Is.EqualTo("-8%/45m"));
    }

    [Test]
    public void Normalizes_the_display_text_to_percent_per_hour_once_the_window_exceeds_an_hour()
    {
        var trend = new BatteryTrend(-15, TimeSpan.FromHours(3), IsCharging: false);
        Assert.That(BatteryTrendFormatter.FormatText(trend), Is.EqualTo("-5%/1h"));
    }

    [Test]
    public void Suppresses_a_normalized_rate_that_rounds_to_zero()
    {
        var trend = new BatteryTrend(-1, TimeSpan.FromHours(3), IsCharging: false);
        Assert.That(BatteryTrendFormatter.FormatText(trend), Is.Null);
    }
}
