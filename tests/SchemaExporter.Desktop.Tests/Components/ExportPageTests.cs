using AngleSharp.Dom;
using Bunit;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.SchemaProviders;
using CloudyWing.SchemaExporter.Desktop.Components.Layout;
using CloudyWing.SchemaExporter.Desktop.Components.Pages;
using NSubstitute;

namespace CloudyWing.SchemaExporter.Desktop.Tests.Components;

[TestFixture]
public sealed class ExportPageTests {
    [OneTimeSetUp]
    public void OneTimeSetUp() {
        SpreadsheetExporterBootstrapper.Configure();
    }

    [Test]
    public void Render_WhenSettingsAreLoaded_ShowsArtifactSummaryAndEnablesExport() {
        using TemporaryDirectory directory = new();
        using ComponentTestHost host = new(ComponentTestHost.CreateOptions(directory.Path));

        IRenderedComponent<Export> cut = host.Context.Render<Export>();
        cut.WaitForState(() => HasEnabledButton(cut, "開始匯出"));

        Assert.That(cut.Markup, Does.Contain("Excel + Manifest"));
    }

    [Test]
    public void Render_WhenOutputPathIsBlank_DisablesExport() {
        using ComponentTestHost host = new(ComponentTestHost.CreateOptions(""));

        IRenderedComponent<Export> cut = host.Context.Render<Export>();
        cut.WaitForState(() => cut.Markup.Contains("Excel + Manifest", StringComparison.Ordinal));

        Assert.That(FindButton(cut, "開始匯出").HasAttribute("disabled"), Is.True);
    }

    [Test]
    public void Render_WhenSettingsCannotBeLoaded_ShowsErrorWithReloadAction() {
        using ComponentTestHost host = new(ComponentTestHost.CreateOptions(@"C:\Exports"));
        host.SettingsService.LoadAsync().Returns(Task.FromException<SchemaOptions>(new InvalidOperationException("格式無效")));

        IRenderedComponent<Export> cut = host.Context.Render<Export>();
        cut.WaitForState(() => cut.Markup.Contains("無法載入設定", StringComparison.Ordinal));

        using (Assert.EnterMultipleScope()) {
            Assert.That(cut.Markup, Does.Contain("無法載入設定：格式無效"));
            Assert.That(FindButton(cut, "重新讀取"), Is.Not.Null);
            Assert.That(cut.FindAll("button").Any(x => x.TextContent.Contains("開始匯出", StringComparison.Ordinal)), Is.False);
        }
    }

    [Test]
    public void ClickExport_WhenExportCompletesWithWarning_ShowsResultsAndExpandsDiagnostics() {
        using TemporaryDirectory directory = new();
        using ComponentTestHost host = new(ComponentTestHost.CreateOptions(directory.Path));
        SetupProvider(host.ProviderFactory, Task.FromResult(CreateObjects()));
        IRenderedComponent<Export> cut = host.Context.Render<Export>();
        cut.WaitForState(() => HasEnabledButton(cut, "開始匯出"));

        FindButton(cut, "開始匯出").Click();
        // 進度訊息「正在整理匯出結果...」也含「匯出結果」，以「開始匯出」重新出現判斷匯出結束。
        cut.WaitForState(() => HasEnabledButton(cut, "開始匯出"), TimeSpan.FromSeconds(10));

        using (Assert.EnterMultipleScope()) {
            Assert.That(cut.Markup, Does.Contain("開啟輸出資料夾"));
            Assert.That(cut.Markup, Does.Contain("1 警告"));
            Assert.That(cut.Markup, Does.Contain("View 欄位描述無法取得"));
            Assert.That(cut.Markup, Does.Contain("mud-panel-expanded"));
        }
    }

    [Test]
    public void ClickCancel_WhileExporting_ShowsCanceledStatus() {
        using TemporaryDirectory directory = new();
        using ComponentTestHost host = new(ComponentTestHost.CreateOptions(directory.Path));
        host.ProviderFactory.LoadObjectsAsync(Arg.Any<DatabaseType>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo => {
                await Task.Delay(Timeout.Infinite, callInfo.Arg<CancellationToken>());
                return (IReadOnlyList<DatabaseObjectSchema>)[];
            });
        IRenderedComponent<Export> cut = host.Context.Render<Export>();
        cut.WaitForState(() => HasEnabledButton(cut, "開始匯出"));

        FindButton(cut, "開始匯出").Click();
        // 等到卡在載入資料庫物件的階段，之後不再有進度重新繪製，避免點擊到已被替換的按鈕。
        cut.WaitForState(() => HasEnabledButton(cut, "取消匯出") && cut.Markup.Contains("正在連線資料庫", StringComparison.Ordinal));
        FindButton(cut, "取消匯出").Click();
        cut.WaitForState(() => HasEnabledButton(cut, "開始匯出"), TimeSpan.FromSeconds(10));

        Assert.That(cut.Markup, Does.Contain("匯出已取消"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void NavMenu_WhenExportStateChanges_TogglesSettingsLink(bool isExporting) {
        using ComponentTestHost host = new(ComponentTestHost.CreateOptions(@"C:\Exports"));

        IRenderedComponent<NavMenu> cut = host.Context.Render<NavMenu>(parameters => parameters.Add(x => x.IsExporting, isExporting));

        IElement settingsLink = cut.FindAll(".mud-nav-link").Single(x => x.TextContent.Contains("設定", StringComparison.Ordinal));
        Assert.That(settingsLink.ClassList.Contains("mud-nav-link-disabled"), Is.EqualTo(isExporting));
    }

    // bUnit 的 WaitForAssertion 會重試失敗的斷言，但 NUnit 4 會記錄每次失敗，因此改以 WaitForState 等待後再斷言。
    private static bool HasEnabledButton(IRenderedComponent<Export> cut, string text) {
        return cut.FindAll("button").Any(x => x.TextContent.Contains(text, StringComparison.Ordinal) && !x.HasAttribute("disabled"));
    }

    private static IElement FindButton(IRenderedComponent<Export> cut, string text) {
        return cut.FindAll("button").Single(x => x.TextContent.Contains(text, StringComparison.Ordinal));
    }

    private static IReadOnlyList<DatabaseObjectSchema> CreateObjects() {
        return [
            new DatabaseObjectSchema { SchemaName = "dbo", ObjectName = "Users", ObjectType = "TABLE", ObjectDescription = "" }
        ];
    }

    private static void SetupProvider(IDatabaseSchemaProviderFactory providerFactory, Task<IReadOnlyList<DatabaseObjectSchema>> objects) {
        providerFactory.LoadObjectsAsync(Arg.Any<DatabaseType>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(objects);
        providerFactory.LoadDetailsAsync(
            Arg.Any<DatabaseType>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<DatabaseObjectSchema>>(),
            Arg.Any<CancellationToken>()
        ).Returns(Task.FromResult(new DatabaseSchemaDetails {
            Columns = [],
            Indexes = [],
            Routines = [],
            Diagnostics = [
                new ExportDiagnostic {
                    Severity = DiagnosticSeverity.Warning,
                    Category = ExportDiagnosticCategory.ViewSupport,
                    Message = "View 欄位描述無法取得"
                }
            ]
        }));
    }
}
