namespace CloudyWing.SchemaExporter.Desktop.Tests.Hosting;

/// <summary>
/// 由測試手動推進的時間來源，單調時鐘、UTC 時間與計時器同步前進。
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider {
    private readonly DateTimeOffset startUtc;
    private readonly List<ManualTimer> timers = [];
    private long timestamp;

    public ManualTimeProvider(DateTimeOffset? startUtc = null) {
        this.startUtc = startUtc ?? new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
    }

    public override long TimestampFrequency => 1000;

    public void Advance(TimeSpan duration) {
        timestamp += (long)(duration.TotalSeconds * TimestampFrequency);
        foreach (ManualTimer timer in timers.ToArray()) {
            timer.FireIfDue();
        }
    }

    public override long GetTimestamp() {
        return timestamp;
    }

    public override DateTimeOffset GetUtcNow() {
        return startUtc.AddSeconds((double)timestamp / TimestampFrequency);
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) {
        ManualTimer timer = new(this, callback, state);
        timers.Add(timer);
        timer.Change(dueTime, period);
        return timer;
    }

    private sealed class ManualTimer : ITimer {
        private readonly ManualTimeProvider owner;
        private readonly TimerCallback callback;
        private readonly object? state;
        private long? dueTimestamp;
        private long periodTicks;

        public ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) {
            this.owner = owner;
            this.callback = callback;
            this.state = state;
        }

        public bool Change(TimeSpan dueTime, TimeSpan period) {
            dueTimestamp = dueTime == Timeout.InfiniteTimeSpan
                ? null
                : owner.timestamp + (long)(dueTime.TotalSeconds * owner.TimestampFrequency);
            periodTicks = period == Timeout.InfiniteTimeSpan || period == TimeSpan.Zero
                ? 0
                : (long)(period.TotalSeconds * owner.TimestampFrequency);
            return true;
        }

        public void FireIfDue() {
            if (dueTimestamp is not long due || owner.timestamp < due) {
                return;
            }

            dueTimestamp = periodTicks > 0 ? due + periodTicks : null;
            callback(state);
        }

        public void Dispose() {
            dueTimestamp = null;
            owner.timers.Remove(this);
        }

        public ValueTask DisposeAsync() {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
