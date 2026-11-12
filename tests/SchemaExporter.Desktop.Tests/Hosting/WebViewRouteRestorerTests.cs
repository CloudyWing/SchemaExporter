using CloudyWing.SchemaExporter.Desktop.Hosting;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CloudyWing.SchemaExporter.Desktop.Tests.Hosting;

[TestFixture]
public sealed class WebViewRouteRestorerTests {
    private const string Route = "settings?tab=2";

    private ManualTimeProvider time = null!;
    private List<string> events = null!;
    private RecordingNavigationManager navigation = null!;
    private ServiceProvider services = null!;
    private WebViewRouteRestorer sut = null!;

    [SetUp]
    public void SetUp() {
        time = new ManualTimeProvider();
        events = [];
        navigation = new RecordingNavigationManager();
        services = new ServiceCollection()
            .AddSingleton<NavigationManager>(navigation)
            .BuildServiceProvider();
        sut = new WebViewRouteRestorer(time, (name, detail) => events.Add(detail is null ? name : $"{name} {detail}"));
    }

    [TearDown]
    public void TearDown() {
        services.Dispose();
    }

    [Test]
    public async Task RestoreAsync_FirstDispatchSucceeds_NavigatesOnceAndLogsRestoredAsync() {
        int dispatchCount = 0;

        await sut.RestoreAsync(
            Route,
            work => {
                dispatchCount++;
                work(services);
                return Task.FromResult(true);
            },
            () => true
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(dispatchCount, Is.EqualTo(1));
            Assert.That(navigation.Navigations, Is.EqualTo(new[] { Route }));
            Assert.That(events, Is.EqualTo(new[] { $"RouteRestored {Route}" }));
        }
    }

    [Test]
    public async Task RestoreAsync_ThirdDispatchSucceeds_RetriesWithIntervalAsync() {
        int dispatchCount = 0;

        Task restore = sut.RestoreAsync(
            Route,
            work => {
                dispatchCount++;
                if (dispatchCount < 3) {
                    return Task.FromResult(false);
                }

                work(services);
                return Task.FromResult(true);
            },
            () => true
        );
        await CompleteAsync(restore);

        using (Assert.EnterMultipleScope()) {
            Assert.That(dispatchCount, Is.EqualTo(3));
            Assert.That(navigation.Navigations, Is.EqualTo(new[] { Route }));
            Assert.That(events, Is.EqualTo(new[] { $"RouteRestored {Route}" }));
        }
    }

    [Test]
    public async Task RestoreAsync_DispatchNeverCompletes_TimesOutEachAttemptAndEndsAtDeadlineAsync() {
        int dispatchCount = 0;
        long startTimestamp = time.GetTimestamp();

        Task restore = sut.RestoreAsync(
            Route,
            _ => {
                dispatchCount++;
                return new TaskCompletionSource<bool>().Task;
            },
            () => true
        );
        await CompleteAsync(restore);
        TimeSpan elapsed = time.GetElapsedTime(startTimestamp, time.GetTimestamp());

        using (Assert.EnterMultipleScope()) {
            Assert.That(restore.IsCompletedSuccessfully, Is.True);
            Assert.That(dispatchCount, Is.InRange(2, WebViewRouteRestorer.MaxAttempts - 1));
            Assert.That(elapsed, Is.LessThanOrEqualTo(WebViewRouteRestorer.RestoreDeadline));
            Assert.That(navigation.Navigations, Is.Empty);
            Assert.That(events, Is.EqualTo(new[] { "RouteRestoreFailed deadline-exceeded" }));
        }
    }

    [Test]
    public async Task RestoreAsync_RemainingTimeBelowRetryInterval_WaitsOnlyRemainingTimeThenFailsAsync() {
        int dispatchCount = 0;
        long startTimestamp = time.GetTimestamp();
        Task restore = sut.RestoreAsync(
            Route,
            _ => {
                dispatchCount++;
                // 模擬第一次派送耗去幾乎整段期限，剩下 63 毫秒，小於 100 毫秒的重試間隔。
                time.Advance(TimeSpan.FromSeconds(9.9375));
                return Task.FromResult(false);
            },
            () => true
        );

        time.Advance(TimeSpan.FromSeconds(0.0625));
        await Task.Yield();
        bool completedBeforeRemaining = restore.IsCompleted;
        time.Advance(TimeSpan.FromSeconds(0.001));
        await restore.WaitAsync(TimeSpan.FromSeconds(10));
        TimeSpan elapsed = time.GetElapsedTime(startTimestamp, time.GetTimestamp());

        using (Assert.EnterMultipleScope()) {
            Assert.That(completedBeforeRemaining, Is.False);
            Assert.That(dispatchCount, Is.EqualTo(1));
            Assert.That(elapsed, Is.EqualTo(WebViewRouteRestorer.RestoreDeadline));
            Assert.That(navigation.Navigations, Is.Empty);
            Assert.That(events, Is.EqualTo(new[] { "RouteRestoreFailed deadline-exceeded" }));
        }
    }

    [Test]
    public async Task RestoreAsync_DispatchNeverCompletes_FirstAttemptWaitsOneSecondAsync() {
        int dispatchCount = 0;
        Task restore = sut.RestoreAsync(
            Route,
            _ => {
                dispatchCount++;
                return new TaskCompletionSource<bool>().Task;
            },
            () => true
        );

        time.Advance(TimeSpan.FromSeconds(0.9));
        await Task.Yield();
        int beforeTimeout = dispatchCount;
        time.Advance(TimeSpan.FromSeconds(0.1));
        await Task.Yield();
        time.Advance(WebViewRouteRestorer.RetryInterval);
        await Task.Yield();

        using (Assert.EnterMultipleScope()) {
            Assert.That(beforeTimeout, Is.EqualTo(1));
            Assert.That(dispatchCount, Is.EqualTo(2));
            Assert.That(restore.IsCompleted, Is.False);
        }
    }

    [Test]
    public async Task RestoreAsync_DispatchAlwaysReturnsFalse_StopsAfterMaxAttemptsAsync() {
        int dispatchCount = 0;

        Task restore = sut.RestoreAsync(
            Route,
            _ => {
                dispatchCount++;
                return Task.FromResult(false);
            },
            () => true
        );
        await CompleteAsync(restore);

        using (Assert.EnterMultipleScope()) {
            Assert.That(dispatchCount, Is.EqualTo(WebViewRouteRestorer.MaxAttempts));
            Assert.That(events, Is.EqualTo(new[] { "RouteRestoreFailed attempts-exhausted:50" }));
        }
    }

    [Test]
    public async Task RestoreAsync_LateDelegateRunsAfterRestoreEnded_DoesNotNavigateAsync() {
        List<Action<IServiceProvider>> delegates = [];
        Task restore = sut.RestoreAsync(
            Route,
            work => {
                delegates.Add(work);
                return new TaskCompletionSource<bool>().Task;
            },
            () => true
        );
        await CompleteAsync(restore);

        foreach (Action<IServiceProvider> work in delegates) {
            work(services);
        }

        using (Assert.EnterMultipleScope()) {
            Assert.That(delegates, Is.Not.Empty);
            Assert.That(navigation.Navigations, Is.Empty);
        }
    }

    [Test]
    public async Task RestoreAsync_LateDelegateRunsAfterTargetInvalid_DoesNotNavigateAsync() {
        bool isValid = true;
        List<Action<IServiceProvider>> delegates = [];
        Task restore = sut.RestoreAsync(
            Route,
            work => {
                delegates.Add(work);
                return new TaskCompletionSource<bool>().Task;
            },
            () => isValid
        );
        time.Advance(TimeSpan.FromSeconds(0.5));
        await Task.Yield();

        isValid = false;
        delegates[0](services);
        await CompleteAsync(restore);

        using (Assert.EnterMultipleScope()) {
            Assert.That(navigation.Navigations, Is.Empty);
            Assert.That(events, Is.EqualTo(new[] { "RouteRestoreSkipped target-invalid" }));
        }
    }

    [Test]
    public async Task RestoreAsync_TimedOutDelegateNavigatesBeforeNextAttempt_RestoresOnceWithoutRetryAsync() {
        List<Action<IServiceProvider>> delegates = [];
        Task restore = sut.RestoreAsync(
            Route,
            work => {
                delegates.Add(work);
                return new TaskCompletionSource<bool>().Task;
            },
            () => true
        );

        time.Advance(TimeSpan.FromSeconds(1));
        await Task.Yield();
        Assert.That(delegates, Has.Count.EqualTo(1));
        delegates[0](services);
        await CompleteAsync(restore);

        using (Assert.EnterMultipleScope()) {
            Assert.That(delegates, Has.Count.EqualTo(1));
            Assert.That(navigation.Navigations, Is.EqualTo(new[] { Route }));
            Assert.That(events, Is.EqualTo(new[] { $"RouteRestored {Route}" }));
        }
    }

    [Test]
    public async Task RestoreAsync_TargetInvalidFromStart_DoesNotDispatchAsync() {
        int dispatchCount = 0;

        await sut.RestoreAsync(
            Route,
            _ => {
                dispatchCount++;
                return Task.FromResult(true);
            },
            () => false
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(dispatchCount, Is.Zero);
            Assert.That(events, Is.EqualTo(new[] { "RouteRestoreSkipped target-invalid" }));
        }
    }

    [Test]
    public async Task RestoreAsync_TargetBecomesInvalidAfterFailedAttempt_StopsDispatchingAsync() {
        bool isValid = true;
        int dispatchCount = 0;

        Task restore = sut.RestoreAsync(
            Route,
            _ => {
                dispatchCount++;
                isValid = false;
                return Task.FromResult(false);
            },
            () => isValid
        );
        await CompleteAsync(restore);

        using (Assert.EnterMultipleScope()) {
            Assert.That(dispatchCount, Is.EqualTo(1));
            Assert.That(navigation.Navigations, Is.Empty);
            Assert.That(events, Is.EqualTo(new[] { "RouteRestoreSkipped target-invalid" }));
        }
    }

    [Test]
    public async Task RestoreAsync_DispatchThrows_LogsFailureAndReturnsAsync() {
        await sut.RestoreAsync(
            Route,
            _ => throw new InvalidOperationException("壞掉了"),
            () => true
        );

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0], Does.StartWith("RouteRestoreFailed System.InvalidOperationException:0x"));
    }

    /// <summary>
    /// 逐步推進時間並讓出執行緒，直到工作完成，回傳推進的虛擬時間；超過安全上限仍未完成視為測試失敗。
    /// </summary>
    private async Task<TimeSpan> CompleteAsync(Task task) {
        TimeSpan step = TimeSpan.FromMilliseconds(100);
        TimeSpan elapsed = TimeSpan.Zero;
        for (int i = 0; i < 500 && !task.IsCompleted; i++) {
            await Task.Yield();
            time.Advance(step);
            elapsed += step;
        }

        await task.WaitAsync(TimeSpan.FromSeconds(10));
        return elapsed;
    }

    private sealed class RecordingNavigationManager : NavigationManager {
        public RecordingNavigationManager() {
            Initialize("http://localhost/", "http://localhost/");
        }

        public List<string> Navigations { get; } = [];

        protected override void NavigateToCore(string uri, NavigationOptions options) {
            Navigations.Add(uri);
        }
    }
}
