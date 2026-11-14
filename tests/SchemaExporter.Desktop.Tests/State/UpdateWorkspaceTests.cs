using CloudyWing.SchemaExporter.Desktop.Services;
using CloudyWing.SchemaExporter.Desktop.State;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Velopack;

namespace CloudyWing.SchemaExporter.Desktop.Tests.State;

[TestFixture]
[NonParallelizable]
public sealed class UpdateWorkspaceTests {
    private string? originalLocalAppData;
    private TemporaryDirectory? localAppData;

    [SetUp]
    public void SetUp() {
        // 同意更新時會呼叫 AppPaths.EnsureUserConfigExistsIfInstallConfigExists，改指向暫存目錄以免寫入使用者的真實設定。
        originalLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        localAppData = new TemporaryDirectory();
        Environment.SetEnvironmentVariable("LOCALAPPDATA", localAppData.Path);
    }

    [TearDown]
    public void TearDown() {
        Environment.SetEnvironmentVariable("LOCALAPPDATA", originalLocalAppData);
        localAppData?.Dispose();
        localAppData = null;
    }

    [Test]
    public async Task CheckForUpdatesAsync_WhenUserChoosesLater_DoesNotDownloadOrApplyAndExplainsNextPrompt() {
        IUpdateService updateService = CreateUpdateService(out UpdateInfo update);
        IPromptService promptService = Substitute.For<IPromptService>();
        promptService.ConfirmAsync("有可用更新", Arg.Any<string>(), "立即更新", "稍後").Returns(false);
        UpdateWorkspace sut = new(updateService, promptService);

        await sut.CheckForUpdatesAsync();

        using (Assert.EnterMultipleScope()) {
            await updateService.Received(0).DownloadUpdateAsync(
                Arg.Any<UpdateInfo>(),
                Arg.Any<IProgress<int>?>(),
                Arg.Any<CancellationToken>()
            );
            updateService.Received(0).ApplyUpdateAndRestart(Arg.Any<UpdateInfo>());
            await promptService.DidNotReceive().ShowMessageAsync(Arg.Any<string>(), Arg.Any<string>());
            Assert.That(sut.StatusMessage, Does.Contain("下次啟動時會再次提示"));
            Assert.That(sut.StatusMessage, Does.Contain(update.TargetFullRelease.Version.ToString()));
        }
    }

    [Test]
    public async Task CheckForUpdatesAsync_WhenUserAcceptsAndAcknowledges_DownloadsNotifiesThenRestarts() {
        IUpdateService updateService = CreateUpdateService(out UpdateInfo update);
        IPromptService promptService = Substitute.For<IPromptService>();
        promptService.ConfirmAsync("有可用更新", Arg.Any<string>(), "立即更新", "稍後").Returns(true);
        promptService.ShowMessageAsync("準備更新", Arg.Any<string>()).Returns(true);
        UpdateWorkspace sut = new(updateService, promptService);

        await sut.CheckForUpdatesAsync();

        using (Assert.EnterMultipleScope()) {
            Received.InOrder(() => {
                updateService.DownloadUpdateAsync(update, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());
                promptService.ShowMessageAsync("準備更新", Arg.Any<string>());
                updateService.ApplyUpdateAndRestart(update);
            });
            await updateService.Received(1).DownloadUpdateAsync(update, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());
            updateService.Received(1).ApplyUpdateAndRestart(update);
        }
    }

    [Test]
    public async Task CheckForUpdatesAsync_WhenCompletionNoticeIsNotAcknowledged_DoesNotRestart() {
        IUpdateService updateService = CreateUpdateService(out UpdateInfo update);
        IPromptService promptService = Substitute.For<IPromptService>();
        promptService.ConfirmAsync("有可用更新", Arg.Any<string>(), "立即更新", "稍後").Returns(true);
        promptService.ShowMessageAsync("準備更新", Arg.Any<string>()).Returns(false);
        UpdateWorkspace sut = new(updateService, promptService);

        await sut.CheckForUpdatesAsync();

        using (Assert.EnterMultipleScope()) {
            await updateService.Received(1).DownloadUpdateAsync(update, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());
            updateService.Received(0).ApplyUpdateAndRestart(Arg.Any<UpdateInfo>());
            Assert.That(sut.StatusMessage, Does.Contain("尚未套用"));
        }
    }

    [Test]
    public async Task CheckForUpdatesAsync_WhenPresenterIsDetachedWhileConfirming_CompletesWithoutDownloading() {
        IUpdateService updateService = CreateUpdateService(out _);
        PromptService promptService = new(NullLogger<PromptService>.Instance);
        IDisposable registration = promptService.Attach(new NeverCompletingPresenter());
        UpdateWorkspace sut = new(updateService, promptService);

        Task checkTask = sut.CheckForUpdatesAsync();
        // 模擬 WebView 重建：舊 presenter 登出時，等待中的確認對話框視同選擇「稍後」。
        registration.Dispose();
        Task completed = await Task.WhenAny(checkTask, Task.Delay(TimeSpan.FromSeconds(5)));

        using (Assert.EnterMultipleScope()) {
            Assert.That(completed, Is.SameAs(checkTask));
            await updateService.Received(0).DownloadUpdateAsync(
                Arg.Any<UpdateInfo>(),
                Arg.Any<IProgress<int>?>(),
                Arg.Any<CancellationToken>()
            );
            updateService.Received(0).ApplyUpdateAndRestart(Arg.Any<UpdateInfo>());
            Assert.That(sut.StatusMessage, Does.Contain("下次啟動時會再次提示"));
        }
    }

    [Test]
    public async Task CheckForUpdatesAsync_WhenNoUpdateIsAvailable_DoesNotPrompt() {
        IUpdateService updateService = Substitute.For<IUpdateService>();
        updateService.CheckForUpdatesAsync().Returns(Task.FromResult<UpdateInfo?>(null));
        IPromptService promptService = Substitute.For<IPromptService>();
        UpdateWorkspace sut = new(updateService, promptService);

        await sut.CheckForUpdatesAsync();

        using (Assert.EnterMultipleScope()) {
            await promptService.DidNotReceive().ConfirmAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>()
            );
            Assert.That(sut.StatusMessage, Is.Null);
        }
    }

    private static IUpdateService CreateUpdateService(out UpdateInfo update) {
        VelopackAsset target = new() {
            PackageId = "SchemaExporter",
            Version = new NuGet.Versioning.SemanticVersion(9, 9, 9),
            Type = VelopackAssetType.Full,
            FileName = "SchemaExporter-9.9.9-full.nupkg"
        };
        update = new UpdateInfo(target, false);
        IUpdateService updateService = Substitute.For<IUpdateService>();
        UpdateInfo created = update;
        updateService.CheckForUpdatesAsync().Returns(Task.FromResult<UpdateInfo?>(created));
        updateService.DownloadUpdateAsync(Arg.Any<UpdateInfo>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return updateService;
    }

    private sealed class NeverCompletingPresenter : IPromptPresenter {
        public void ShowSnackbar(StatusKind kind, string message) {
        }

        public Task<bool> ShowMessageAsync(string title, string message) {
            return new TaskCompletionSource<bool>().Task;
        }

        public Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText) {
            return new TaskCompletionSource<bool>().Task;
        }
    }
}
