using CloudyWing.SchemaExporter.Desktop.Services;
using CloudyWing.SchemaExporter.Desktop.State;
using Microsoft.Extensions.Logging.Abstractions;

namespace CloudyWing.SchemaExporter.Desktop.Tests.State;

[TestFixture]
public sealed class PromptServiceTests {
    [Test]
    public async Task ShowMessageAsync_WhenNoPresenter_ReturnsFalse() {
        PromptService sut = CreateService();

        bool result = await sut.ShowMessageAsync("標題", "訊息");

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task ConfirmAsync_WhenNoPresenter_ReturnsFalse() {
        PromptService sut = CreateService();

        bool result = await sut.ConfirmAsync("標題", "訊息", "確認", "取消");

        Assert.That(result, Is.False);
    }

    [Test]
    public void Notify_WhenNoPresenter_DoesNotThrow() {
        PromptService sut = CreateService();

        Assert.DoesNotThrow(() => sut.Notify(StatusKind.Warning, "訊息"));
    }

    [Test]
    public void Notify_WhenPresenterAttached_ForwardsKindAndMessage() {
        PromptService sut = CreateService();
        FakePresenter presenter = new();
        using IDisposable registration = sut.Attach(presenter);

        sut.Notify(StatusKind.Error, "發生錯誤");

        Assert.That(presenter.Snackbars, Is.EqualTo(new[] { (StatusKind.Error, "發生錯誤") }));
    }

    [Test]
    public void Notify_WhenPresenterThrows_DoesNotThrow() {
        PromptService sut = CreateService();
        FakePresenter presenter = new() { ThrowOnShow = true };
        using IDisposable registration = sut.Attach(presenter);

        Assert.DoesNotThrow(() => sut.Notify(StatusKind.Info, "訊息"));
    }

    [Test]
    public async Task ShowMessageAsync_WhenPresenterReturnsTrue_ReturnsTrue() {
        PromptService sut = CreateService();
        FakePresenter presenter = new();
        using IDisposable registration = sut.Attach(presenter);

        Task<bool> task = sut.ShowMessageAsync("標題", "訊息");
        presenter.CompleteNext(true);

        using (Assert.EnterMultipleScope()) {
            Assert.That(await task, Is.True);
            Assert.That(presenter.Dialogs, Is.EqualTo(new[] { "標題|訊息" }));
        }
    }

    [Test]
    public async Task ConfirmAsync_WhenPresenterReturnsFalse_ReturnsFalse() {
        PromptService sut = CreateService();
        FakePresenter presenter = new();
        using IDisposable registration = sut.Attach(presenter);

        Task<bool> task = sut.ConfirmAsync("標題", "訊息", "確認", "取消");
        presenter.CompleteNext(false);

        using (Assert.EnterMultipleScope()) {
            Assert.That(await task, Is.False);
            Assert.That(presenter.Dialogs, Is.EqualTo(new[] { "標題|訊息|確認|取消" }));
        }
    }

    [Test]
    public async Task ConfirmAsync_WhenPresenterDetachedWhileWaiting_ReturnsFalse() {
        PromptService sut = CreateService();
        FakePresenter presenter = new();
        IDisposable registration = sut.Attach(presenter);
        Task<bool> task = sut.ConfirmAsync("標題", "訊息", "確認", "取消");

        registration.Dispose();

        Task completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
        using (Assert.EnterMultipleScope()) {
            Assert.That(completed, Is.SameAs(task));
            Assert.That(await task, Is.False);
        }
    }

    [Test]
    public async Task ShowMessageAsync_WhenPresenterFaultsAfterDetach_DoesNotLeaveUnobservedException() {
        PromptService sut = CreateService();
        FakePresenter presenter = new();
        IDisposable registration = sut.Attach(presenter);
        Task<bool> task = sut.ShowMessageAsync("標題", "訊息");
        registration.Dispose();
        await task;

        presenter.FailNext(new InvalidOperationException("登出後才失敗"));

        Assert.That(await task, Is.False);
    }

    [Test]
    public async Task ShowMessageAsync_WhenPresenterThrowsSynchronously_ReturnsFalse() {
        PromptService sut = CreateService();
        FakePresenter presenter = new() { ThrowOnShow = true };
        using IDisposable registration = sut.Attach(presenter);

        bool result = await sut.ShowMessageAsync("標題", "訊息");

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task ConfirmAsync_WhenPresenterTaskFaults_ReturnsFalse() {
        PromptService sut = CreateService();
        FakePresenter presenter = new();
        using IDisposable registration = sut.Attach(presenter);

        Task<bool> task = sut.ConfirmAsync("標題", "訊息", "確認", "取消");
        presenter.FailNext(new InvalidOperationException("顯示失敗"));

        Assert.That(await task, Is.False);
    }

    [Test]
    public async Task ShowMessageAsync_WhenTwoPresenters_UsesLastAttached() {
        PromptService sut = CreateService();
        FakePresenter older = new();
        FakePresenter newer = new();
        using IDisposable olderRegistration = sut.Attach(older);
        using IDisposable newerRegistration = sut.Attach(newer);

        Task<bool> task = sut.ShowMessageAsync("標題", "訊息");
        newer.CompleteNext(true);
        await task;

        using (Assert.EnterMultipleScope()) {
            Assert.That(newer.Dialogs, Has.Count.EqualTo(1));
            Assert.That(older.Dialogs, Is.Empty);
        }
    }

    [Test]
    public async Task ShowMessageAsync_WhenActivePresenterDetached_UsesPreviousPresenter() {
        PromptService sut = CreateService();
        FakePresenter older = new();
        FakePresenter newer = new();
        using IDisposable olderRegistration = sut.Attach(older);
        IDisposable newerRegistration = sut.Attach(newer);
        newerRegistration.Dispose();

        Task<bool> task = sut.ShowMessageAsync("標題", "訊息");
        older.CompleteNext(true);
        await task;

        using (Assert.EnterMultipleScope()) {
            Assert.That(older.Dialogs, Has.Count.EqualTo(1));
            Assert.That(newer.Dialogs, Is.Empty);
        }
    }

    [Test]
    public async Task ConfirmAsync_WhenOlderPresenterDetached_DoesNotAffectNewerPendingPrompt() {
        PromptService sut = CreateService();
        FakePresenter older = new();
        FakePresenter newer = new();
        IDisposable olderRegistration = sut.Attach(older);
        using IDisposable newerRegistration = sut.Attach(newer);
        Task<bool> task = sut.ConfirmAsync("標題", "訊息", "確認", "取消");

        olderRegistration.Dispose();
        await Task.Delay(50);
        bool completedEarly = task.IsCompleted;
        newer.CompleteNext(true);

        using (Assert.EnterMultipleScope()) {
            Assert.That(completedEarly, Is.False);
            Assert.That(await task, Is.True);
        }
    }

    private static PromptService CreateService() {
        return new PromptService(NullLogger<PromptService>.Instance);
    }

    private sealed class FakePresenter : IPromptPresenter {
        private readonly Queue<TaskCompletionSource<bool>> pending = new();

        public bool ThrowOnShow { get; init; }

        public List<(StatusKind Kind, string Message)> Snackbars { get; } = [];

        public List<string> Dialogs { get; } = [];

        public void ShowSnackbar(StatusKind kind, string message) {
            if (ThrowOnShow) {
                throw new InvalidOperationException("顯示 Snackbar 失敗");
            }

            Snackbars.Add((kind, message));
        }

        public Task<bool> ShowMessageAsync(string title, string message) {
            return Present($"{title}|{message}");
        }

        public Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText) {
            return Present($"{title}|{message}|{confirmText}|{cancelText}");
        }

        public void CompleteNext(bool result) {
            pending.Dequeue().SetResult(result);
        }

        public void FailNext(Exception exception) {
            pending.Dequeue().SetException(exception);
        }

        private Task<bool> Present(string description) {
            if (ThrowOnShow) {
                throw new InvalidOperationException("顯示對話框失敗");
            }

            Dialogs.Add(description);
            TaskCompletionSource<bool> source = new(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Enqueue(source);
            return source.Task;
        }
    }
}
