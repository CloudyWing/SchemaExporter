using CloudyWing.SchemaExporter.Desktop.Hosting;

namespace CloudyWing.SchemaExporter.Desktop.Tests.Hosting;

[TestFixture]
public sealed class WebViewRebuildSchedulerTests {
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(3);

    private ManualTimeProvider time = null!;
    private bool exporting;
    private WebViewRebuildScheduler sut = null!;

    [SetUp]
    public void SetUp() {
        time = new ManualTimeProvider();
        exporting = false;
        sut = new WebViewRebuildScheduler(time, Delay, () => exporting);
    }

    [Test]
    public void Poll_WithoutTrigger_IsIdle() {
        RebuildPollResult result = sut.Poll();

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.Kind, Is.EqualTo(RebuildPollKind.Idle));
            Assert.That(sut.IsPending, Is.False);
        }
    }

    [Test]
    public void Poll_BeforeDelayElapsed_IsWaitingWithRemainingTime() {
        sut.Trigger("SessionUnlock");
        time.Advance(TimeSpan.FromSeconds(1));

        RebuildPollResult result = sut.Poll();

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.Kind, Is.EqualTo(RebuildPollKind.Waiting));
            Assert.That(result.Wait, Is.EqualTo(TimeSpan.FromSeconds(2)));
        }
    }

    [Test]
    public void Poll_AfterDelayElapsed_IsReadyAndClearsPending() {
        sut.Trigger("SessionUnlock");
        time.Advance(Delay);

        RebuildPollResult ready = sut.Poll();
        RebuildPollResult next = sut.Poll();

        using (Assert.EnterMultipleScope()) {
            Assert.That(ready.Kind, Is.EqualTo(RebuildPollKind.Ready));
            Assert.That(ready.Reasons, Is.EqualTo("SessionUnlock"));
            Assert.That(next.Kind, Is.EqualTo(RebuildPollKind.Idle));
            Assert.That(sut.IsPending, Is.False);
        }
    }

    [Test]
    public void Poll_MultipleTriggers_RebuildsOnceCountedFromLastTrigger() {
        sut.Trigger("SessionUnlock");
        time.Advance(TimeSpan.FromSeconds(2));
        sut.Trigger("DisplaySettingsChanged");
        time.Advance(TimeSpan.FromSeconds(2));
        RebuildPollResult tooEarly = sut.Poll();
        time.Advance(TimeSpan.FromSeconds(1));

        RebuildPollResult ready = sut.Poll();
        RebuildPollResult afterRebuild = sut.Poll();

        using (Assert.EnterMultipleScope()) {
            Assert.That(tooEarly.Kind, Is.EqualTo(RebuildPollKind.Waiting));
            Assert.That(tooEarly.Wait, Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(ready.Kind, Is.EqualTo(RebuildPollKind.Ready));
            Assert.That(ready.Reasons, Is.EqualTo("SessionUnlock, DisplaySettingsChanged"));
            Assert.That(afterRebuild.Kind, Is.EqualTo(RebuildPollKind.Idle));
        }
    }

    [Test]
    public void Poll_WhileExporting_DefersAndWaitsAnotherDelay() {
        sut.Trigger("PowerResume");
        exporting = true;
        time.Advance(Delay);

        RebuildPollResult first = sut.Poll();
        time.Advance(TimeSpan.FromSeconds(1));
        RebuildPollResult stillWaiting = sut.Poll();
        time.Advance(TimeSpan.FromSeconds(2));
        RebuildPollResult second = sut.Poll();

        using (Assert.EnterMultipleScope()) {
            Assert.That(first.Kind, Is.EqualTo(RebuildPollKind.Deferred));
            Assert.That(first.IsFirstDeferral, Is.True);
            Assert.That(first.Wait, Is.EqualTo(Delay));
            Assert.That(stillWaiting.Kind, Is.EqualTo(RebuildPollKind.Waiting));
            Assert.That(second.Kind, Is.EqualTo(RebuildPollKind.Deferred));
            Assert.That(second.IsFirstDeferral, Is.False);
            Assert.That(sut.IsPending, Is.True);
        }
    }

    [Test]
    public void Poll_ExportFinishesAfterDeferral_RebuildsOnlyOnce() {
        sut.Trigger("PowerResume");
        exporting = true;
        time.Advance(Delay);
        sut.Poll();
        sut.Trigger("DisplaySettingsChanged");
        exporting = false;
        time.Advance(Delay);

        RebuildPollResult ready = sut.Poll();
        RebuildPollResult afterRebuild = sut.Poll();

        using (Assert.EnterMultipleScope()) {
            Assert.That(ready.Kind, Is.EqualTo(RebuildPollKind.Ready));
            Assert.That(ready.Reasons, Is.EqualTo("PowerResume, DisplaySettingsChanged"));
            Assert.That(afterRebuild.Kind, Is.EqualTo(RebuildPollKind.Idle));
        }
    }

    [Test]
    public void Poll_AfterReadyThenDeferral_ReportsFirstDeferralAgain() {
        sut.Trigger("A");
        exporting = true;
        time.Advance(Delay);
        sut.Poll();
        exporting = false;
        time.Advance(Delay);
        sut.Poll();
        sut.Trigger("B");
        exporting = true;
        time.Advance(Delay);

        RebuildPollResult result = sut.Poll();

        Assert.That(result.IsFirstDeferral, Is.True);
    }

    [Test]
    public void Poll_SkipDelay_IsReadyImmediately() {
        sut.Trigger("DebugShortcut", skipDelay: true);

        RebuildPollResult result = sut.Poll();

        Assert.That(result.Kind, Is.EqualTo(RebuildPollKind.Ready));
    }

    [Test]
    public void Poll_SkipDelayWhileExporting_StillDefers() {
        sut.Trigger("DebugShortcut", skipDelay: true);
        exporting = true;

        RebuildPollResult deferred = sut.Poll();
        exporting = false;
        RebuildPollResult stillWaiting = sut.Poll();
        time.Advance(Delay);
        RebuildPollResult ready = sut.Poll();

        using (Assert.EnterMultipleScope()) {
            Assert.That(deferred.Kind, Is.EqualTo(RebuildPollKind.Deferred));
            Assert.That(stillWaiting.Kind, Is.EqualTo(RebuildPollKind.Waiting));
            Assert.That(ready.Kind, Is.EqualTo(RebuildPollKind.Ready));
        }
    }

    [Test]
    public async Task RunRebuildAsync_TriggerDuringRebuild_StaysPendingAndClearsFlagAfterCompletionAsync() {
        sut.Trigger("PowerResume");
        time.Advance(Delay);
        Assert.That(sut.Poll().Kind, Is.EqualTo(RebuildPollKind.Ready));
        TaskCompletionSource rebuild = new();
        Task running = sut.RunRebuildAsync(() => rebuild.Task);
        bool isRebuildingDuring = sut.IsRebuilding;

        sut.Trigger("DisplaySettingsChanged");
        time.Advance(Delay);
        RebuildPollResult duringPoll = sut.Poll();
        bool isPendingDuring = sut.IsPending;
        rebuild.SetResult();
        await running;
        time.Advance(Delay);
        RebuildPollResult afterPoll = sut.Poll();

        using (Assert.EnterMultipleScope()) {
            Assert.That(isRebuildingDuring, Is.True);
            Assert.That(duringPoll.Kind, Is.EqualTo(RebuildPollKind.Idle));
            Assert.That(isPendingDuring, Is.True);
            Assert.That(sut.IsRebuilding, Is.False);
            Assert.That(afterPoll.Kind, Is.EqualTo(RebuildPollKind.Ready));
            Assert.That(afterPoll.Reasons, Is.EqualTo("DisplaySettingsChanged"));
        }
    }

    [Test]
    public void RunRebuildAsync_RebuildThrows_ClearsFlagAndKeepsPending() {
        sut.Trigger("PowerResume");
        time.Advance(Delay);
        sut.Poll();
        TaskCompletionSource rebuild = new();
        Task running = sut.RunRebuildAsync(() => rebuild.Task);
        sut.Trigger("DisplaySettingsChanged");

        rebuild.SetException(new InvalidOperationException("重建失敗"));
        Assert.ThrowsAsync<InvalidOperationException>(async () => await running);

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.IsRebuilding, Is.False);
            Assert.That(sut.IsPending, Is.True);
        }
    }

    [Test]
    public void RunRebuildAsync_RebuildThrowsSynchronously_ClearsFlag() {
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await sut.RunRebuildAsync(() => throw new InvalidOperationException("重建失敗"))
        );

        Assert.That(sut.IsRebuilding, Is.False);
    }

    [Test]
    public void RunRebuildAsync_WhileRebuilding_Throws() {
        TaskCompletionSource rebuild = new();
        Task running = sut.RunRebuildAsync(() => rebuild.Task);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await sut.RunRebuildAsync(() => Task.CompletedTask));
        rebuild.SetResult();

        Assert.That(running.IsCompletedSuccessfully, Is.True);
    }

    [TestCase("")]
    [TestCase(" ")]
    public void Trigger_BlankReason_Throws(string reason) {
        Assert.Throws<ArgumentException>(() => sut.Trigger(reason));
    }
}
