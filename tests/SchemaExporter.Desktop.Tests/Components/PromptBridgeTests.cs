using AngleSharp.Dom;
using Bunit;
using CloudyWing.SchemaExporter.Desktop.Components;
using CloudyWing.SchemaExporter.Desktop.Services;
using CloudyWing.SchemaExporter.Desktop.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;

namespace CloudyWing.SchemaExporter.Desktop.Tests.Components;

[TestFixture]
public sealed class PromptBridgeTests {
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    private BunitContext? context;
    private PromptService? promptService;
    private IRenderedComponent<MudDialogProvider>? dialogProvider;
    private IRenderedComponent<MudSnackbarProvider>? snackbarProvider;

    private BunitContext Context => context ?? throw new InvalidOperationException("bUnit 環境尚未建立。");

    private PromptService Service => promptService ?? throw new InvalidOperationException("PromptService 尚未建立。");

    private IRenderedComponent<MudDialogProvider> DialogProvider => dialogProvider
        ?? throw new InvalidOperationException("MudDialogProvider 尚未渲染。");

    private IRenderedComponent<MudSnackbarProvider> SnackbarProvider => snackbarProvider
        ?? throw new InvalidOperationException("MudSnackbarProvider 尚未渲染。");

    [SetUp]
    public void SetUp() {
        context = new BunitContext();
        // MudBlazor 元件會呼叫 JS interop，測試只驗證 Blazor 端行為。
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices(options => {
            // 測試只渲染受測元件，不渲染 MainLayout 中的 MudPopoverProvider。
            options.PopoverOptions.CheckForPopoverProvider = false;
        });
        promptService = new PromptService(NullLogger<PromptService>.Instance);
        context.Services.AddSingleton(promptService);

        context.Render<MudPopoverProvider>();
        dialogProvider = context.Render<MudDialogProvider>();
        snackbarProvider = context.Render<MudSnackbarProvider>();
        context.Render<PromptBridge>();
    }

    [TearDown]
    public async Task TearDownAsync() {
        if (context is not null) {
            // MudBlazor 部分服務只實作 IAsyncDisposable，必須非同步釋放。
            await context.DisposeAsync();
            context = null;
        }
    }

    [TestCase(StatusKind.Warning, Severity.Warning)]
    [TestCase(StatusKind.Error, Severity.Error)]
    [TestCase(StatusKind.Success, Severity.Success)]
    [TestCase(StatusKind.Info, Severity.Info)]
    public void Notify_WhenCalled_ShowsSnackbarWithMappedSeverity(StatusKind kind, Severity expectedSeverity) {
        ISnackbar snackbar = Context.Services.GetRequiredService<ISnackbar>();

        Service.Notify(kind, "操作結果訊息");
        SnackbarProvider.WaitForState(() => snackbar.ShownSnackbars.Any(), WaitTimeout);

        using (Assert.EnterMultipleScope()) {
            Assert.That(snackbar.ShownSnackbars.Select(x => x.Severity), Is.EqualTo(new[] { expectedSeverity }));
            Assert.That(SnackbarProvider.Markup, Does.Contain("操作結果訊息"));
        }
    }

    [Test]
    public async Task ShowMessageAsync_WhenOkClicked_ShowsSingleButtonDialogAndReturnsTrue() {
        Task<bool> task = Service.ShowMessageAsync("連線錯誤", "無法連線到資料庫。");
        DialogProvider.WaitForState(() => HasButton("確定"), WaitTimeout);

        using (Assert.EnterMultipleScope()) {
            Assert.That(DialogProvider.Markup, Does.Contain("連線錯誤"));
            Assert.That(DialogProvider.Markup, Does.Contain("無法連線到資料庫。"));
            Assert.That(DialogProvider.FindAll("button"), Has.Count.EqualTo(1));
        }

        FindButton("確定").Click();

        Assert.That(await WaitAsync(task), Is.True);
    }

    [Test]
    public async Task ConfirmAsync_WhenConfirmClicked_ReturnsTrue() {
        Task<bool> task = Service.ConfirmAsync("有可用更新", "是否立即更新？", "立即更新", "稍後");
        DialogProvider.WaitForState(() => HasButton("立即更新") && HasButton("稍後"), WaitTimeout);

        Assert.That(DialogProvider.FindAll("button"), Has.Count.EqualTo(2));

        FindButton("立即更新").Click();

        Assert.That(await WaitAsync(task), Is.True);
    }

    [Test]
    public async Task ConfirmAsync_WhenCancelClicked_ReturnsFalse() {
        Task<bool> task = Service.ConfirmAsync("有可用更新", "是否立即更新？", "立即更新", "稍後");
        DialogProvider.WaitForState(() => HasButton("稍後"), WaitTimeout);

        FindButton("稍後").Click();

        Assert.That(await WaitAsync(task), Is.False);
    }

    [Test]
    public async Task ConfirmAsync_WhenEscapeCancelsDialog_ReturnsFalse() {
        Task<bool> task = Service.ConfirmAsync("有可用更新", "是否立即更新？", "立即更新", "稍後");
        DialogProvider.WaitForState(() => HasButton("稍後"), WaitTimeout);
        IMudDialogInstance dialog = DialogProvider.FindComponent<MudDialogContainer>().Instance;

        // MudBlazor 以 JS 鍵盤攔截處理 ESC，bUnit 無法觸發；改驗證選項允許 ESC，並以 ESC 實際走的取消路徑（Cancel）驗證回傳值。
        Assert.That(dialog.Options.CloseOnEscapeKey, Is.True);
        await Context.Renderer.Dispatcher.InvokeAsync(dialog.Cancel);

        Assert.That(await WaitAsync(task), Is.False);
    }

    [Test]
    public async Task ConfirmAsync_WhenBackdropClicked_KeepsDialogOpen() {
        Task<bool> task = Service.ConfirmAsync("有可用更新", "是否立即更新？", "立即更新", "稍後");
        DialogProvider.WaitForState(() => HasButton("稍後"), WaitTimeout);

        DialogProvider.Find(".mud-overlay").Click();
        await Task.Delay(100);

        using (Assert.EnterMultipleScope()) {
            Assert.That(task.IsCompleted, Is.False);
            Assert.That(HasButton("稍後"), Is.True);
        }

        FindButton("立即更新").Click();
        await WaitAsync(task);
    }

    [Test]
    public async Task ConfirmAsync_WhenBridgeDisposedWhileWaiting_ReturnsFalse() {
        Task<bool> task = Service.ConfirmAsync("有可用更新", "是否立即更新？", "立即更新", "稍後");
        DialogProvider.WaitForState(() => HasButton("稍後"), WaitTimeout);

        await Context.DisposeComponentsAsync();

        Assert.That(await WaitAsync(task), Is.False);
    }

    [Test]
    public void ShowMessageAsync_WhenShown_AppliesDialogOptionsAndButtonStyles() {
        _ = Service.ConfirmAsync("有可用更新", "是否立即更新？", "立即更新", "稍後");
        DialogProvider.WaitForState(() => HasButton("稍後"), WaitTimeout);

        IElement container = DialogProvider.Find(".mud-dialog");
        using (Assert.EnterMultipleScope()) {
            Assert.That(container.ClassList, Does.Contain("mud-dialog-width-sm"));
            Assert.That(container.ClassList, Does.Contain("mud-dialog-width-full"));
            Assert.That(FindButton("立即更新").ClassList, Does.Contain("mud-button-filled-primary"));
            Assert.That(FindButton("稍後").ClassList, Does.Contain("mud-button-text"));
        }
    }

    private bool HasButton(string text) {
        return DialogProvider.FindAll("button").Any(x => string.Equals(x.TextContent.Trim(), text, StringComparison.Ordinal));
    }

    private IElement FindButton(string text) {
        return DialogProvider.FindAll("button").Single(x => string.Equals(x.TextContent.Trim(), text, StringComparison.Ordinal));
    }

    private static async Task<bool> WaitAsync(Task<bool> task) {
        Task completed = await Task.WhenAny(task, Task.Delay(WaitTimeout));
        Assert.That(completed, Is.SameAs(task), "提示在時限內沒有完成。");
        return await task;
    }
}
