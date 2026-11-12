using CloudyWing.SchemaExporter.Desktop.Hosting;

namespace CloudyWing.SchemaExporter.Desktop.Tests.Hosting;

[TestFixture]
public sealed class WebViewReleaserTests {
    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(5);

    private ManualTimeProvider time = null!;
    private List<string> events = null!;
    private List<string> calls = null!;
    private WebViewReleaser sut = null!;

    [SetUp]
    public void SetUp() {
        time = new ManualTimeProvider();
        events = [];
        calls = [];
        sut = new WebViewReleaser(
            DisposeTimeout,
            time,
            (name, detail) => events.Add(detail is null ? name : $"{name} {detail}")
        );
    }

    [Test]
    public async Task ReleaseAsync_DisposeCompletesWithinTimeout_DoesNotForceDisposeAsync() {
        TaskCompletionSource dispose = new();
        Task release = sut.ReleaseAsync(() => new ValueTask(dispose.Task), ForceDispose(true), Remove());
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.That(release.IsCompleted, Is.False);

        dispose.SetResult();
        await release;

        using (Assert.EnterMultipleScope()) {
            Assert.That(calls, Is.EqualTo(new[] { "remove" }));
            Assert.That(events, Is.EqualTo(new[] { "RebuildDisposed" }));
        }
    }

    [Test]
    public async Task ReleaseAsync_DisposeNeverCompletes_ForceDisposesAfterTimeoutAsync() {
        TaskCompletionSource never = new();
        Task release = sut.ReleaseAsync(() => new ValueTask(never.Task), ForceDispose(true), Remove());
        time.Advance(TimeSpan.FromSeconds(4.9));
        Assert.That(release.IsCompleted, Is.False);

        time.Advance(TimeSpan.FromSeconds(0.1));
        await release;

        using (Assert.EnterMultipleScope()) {
            Assert.That(calls, Is.EqualTo(new[] { "force", "remove" }));
            Assert.That(events, Is.EqualTo(new[] { "DisposeTimedOut 5s", "ForceDisposed" }));
        }
    }

    [Test]
    public async Task ReleaseAsync_DisposeThrows_ForceDisposesAndLogsFailureAsync() {
        await sut.ReleaseAsync(
            () => ValueTask.FromException(new InvalidOperationException("壞掉了")),
            ForceDispose(true),
            Remove()
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(calls, Is.EqualTo(new[] { "force", "remove" }));
            Assert.That(events, Has.Count.EqualTo(2));
            Assert.That(events[0], Does.StartWith("OldViewDisposeFailed System.InvalidOperationException:0x"));
            Assert.That(events[1], Is.EqualTo("ForceDisposed"));
        }
    }

    [Test]
    public async Task ReleaseAsync_DisposeThrowsSynchronously_ForceDisposesAsync() {
        await sut.ReleaseAsync(
            () => throw new InvalidOperationException("壞掉了"),
            ForceDispose(true),
            Remove()
        );

        Assert.That(calls, Is.EqualTo(new[] { "force", "remove" }));
    }

    [Test]
    public async Task ReleaseAsync_ForceDisposeThrows_StillRemovesAndDoesNotThrowAsync() {
        TaskCompletionSource never = new();
        Task release = sut.ReleaseAsync(
            () => new ValueTask(never.Task),
            () => {
                calls.Add("force");
                throw new InvalidOperationException("強制釋放失敗");
            },
            Remove()
        );
        time.Advance(DisposeTimeout);

        await release;

        using (Assert.EnterMultipleScope()) {
            Assert.That(calls, Is.EqualTo(new[] { "force", "remove" }));
            Assert.That(events, Has.Count.EqualTo(2));
            Assert.That(events[1], Does.StartWith("ForceDisposeFailed System.InvalidOperationException:0x"));
        }
    }

    [Test]
    public async Task ReleaseAsync_ControlNotFound_StillRemovesAndLogsAsync() {
        TaskCompletionSource never = new();
        Task release = sut.ReleaseAsync(() => new ValueTask(never.Task), ForceDispose(false), Remove());
        time.Advance(DisposeTimeout);

        await release;

        using (Assert.EnterMultipleScope()) {
            Assert.That(calls, Is.EqualTo(new[] { "force", "remove" }));
            Assert.That(events, Is.EqualTo(new[] { "DisposeTimedOut 5s", "ForceDisposeFailed control-not-found" }));
        }
    }

    private Func<bool> ForceDispose(bool result) {
        return () => {
            calls.Add("force");
            return result;
        };
    }

    private Action Remove() {
        return () => calls.Add("remove");
    }
}
