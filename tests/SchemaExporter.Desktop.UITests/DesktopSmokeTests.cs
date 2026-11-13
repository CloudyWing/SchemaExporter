using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace CloudyWing.SchemaExporter.Desktop.UITests;

/// <summary>
/// 以 FlaUI 驗證 Desktop 的原生整合：視窗載入、WebView2 內容、WebView 內提示與原生檔案選擇對話框與啟動引數。
/// </summary>
/// <remarks>
/// 需要 Windows 桌面 session 與 WebView2 Runtime，預設不執行，以
/// <c>dotnet test --filter "TestCategory=DesktopUi"</c> 指定執行。
/// </remarks>
[TestFixture]
[Explicit("需要 Windows 桌面 session，請以 TestCategory=DesktopUi 指定執行。")]
[Category(DesktopUiCategories.DesktopUi)]
[NonParallelizable]
[Platform(Include = "Win")]
public sealed class DesktopSmokeTests {
    private DesktopAppSession? session;

    private DesktopAppSession Session => session ?? throw new InvalidOperationException("工作階段尚未建立。");

    [SetUp]
    public void SetUp() {
        session = DesktopAppSession.Create();
    }

    [TearDown]
    public void TearDown() {
        if (session is null) {
            return;
        }

        if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed) {
            try {
                session.CaptureScreen("failure");
            } catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception) {
                TestContext.Out.WriteLine($"無法擷取失敗畫面：{ex.Message}");
            }
        }

        session.Dispose();
        session = null;
    }

    [Test]
    public void Launch_WithCommandLineArguments_ExitsWithCodeOneWithoutWindow() {
        using Process process = Process.Start(Session.CreateStartInfo("export", "--connection", "Unreachable"))
            ?? throw new AssertionException("無法啟動 Desktop 執行檔。");

        bool exited = process.WaitForExit(TimeSpan.FromSeconds(30));

        using (Assert.EnterMultipleScope()) {
            Assert.That(exited, Is.True, "帶引數啟動時應直接結束，不應開出等待操作的視窗。");
            Assert.That(process.ExitCode, Is.EqualTo(1));
        }
    }

    [Test]
    public void Launch_WithoutArguments_ShowsExportPageInsideWebView() {
        Session.Launch();

        using (Assert.EnterMultipleScope()) {
            Assert.That(Session.MainWindow.Title, Is.EqualTo("Schema Exporter"));
            Assert.That(Session.WaitForElement("匯出 Schema"), Is.Not.Null);
            Assert.That(Session.WaitForEnabledButton("開始匯出"), Is.Not.Null);
        }
    }

    [Test]
    public void BrowseOutputFolder_WhenDialogIsCanceled_KeepsOutputPath() {
        Session.Launch();
        Session.WaitForEnabledButton("開始匯出");

        Session.WaitForEnabledButton("瀏覽").Invoke();
        Window dialog = Session.WaitForModalWindow();
        Assert.That(dialog.Title, Does.Contain("選擇匯出資料夾"));
        Keyboard.Press(VirtualKeyShort.ESCAPE);

        // 對話框關閉後主視窗仍可操作，且「開始匯出」維持可按（輸出資料夾未被清空）。
        Assert.That(Session.WaitForEnabledButton("開始匯出"), Is.Not.Null);
    }

    [Test]
    public void StartExport_WhenConnectionFails_ShowsRedactedMudDialogAndRecovers() {
        Session.Launch();

        Session.WaitForEnabledButton("開始匯出").Invoke();
        // 標題與「確定」按鈕同時出現，代表 WebView 內的 MudDialog 已顯示。
        Session.WaitForElement("連線錯誤");
        Session.WaitForEnabledButton("確定", TimeSpan.FromSeconds(60));
        IReadOnlyList<string> dialogTexts = Session.GetDialogTexts("連線錯誤");
        string allNames = Session.CollectDescendantNames();

        using (Assert.EnterMultipleScope()) {
            // WebView 內的 MudDialog 以 role=dialog 與 aria-modal 對應成 UIA 的強制回應視窗（FrameworkId 為 Chrome），
            // 因此只檢查沒有來自原生視窗框架的強制回應視窗，確認沒有 WPF MessageBox。
            Assert.That(
                Session.MainWindow.ModalWindows.Where(x => !string.Equals(x.Properties.FrameworkId.ValueOrDefault, "Chrome", StringComparison.Ordinal)),
                Is.Empty,
                "錯誤應顯示在 WebView 內，不應出現原生 MessageBox。"
            );
            Assert.That(allNames, Does.Not.Contain("UiTestSecret"));
            if (dialogTexts.Count > 0) {
                Assert.That(string.Join(" ", dialogTexts), Does.Not.Contain("UiTestSecret"));
                Assert.That(dialogTexts.Any(x => !string.IsNullOrWhiteSpace(x)), Is.True);
            }
        }

        Session.WaitForEnabledButton("確定").Invoke();
        Session.WaitForElementToDisappear("連線錯誤");
        Assert.That(Session.WaitForEnabledButton("開始匯出"), Is.Not.Null, "關閉錯誤對話框後應回到可操作狀態。");

        // 對話框關閉後，匯出頁的狀態區仍保留同一則錯誤訊息。
        string? message = dialogTexts
            .Where(x => !string.Equals(x, "連線錯誤", StringComparison.Ordinal))
            .OrderByDescending(x => x.Length)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(message)) {
            string prefix = message[..Math.Min(20, message.Length)];
            bool kept = FlaUI.Core.Tools.Retry.WhileFalse(
                () => Session.CollectDescendantNames().Contains(prefix, StringComparison.Ordinal),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromMilliseconds(250),
                ignoreException: true
            ).Result;
            Assert.That(kept, Is.True, "狀態區應保留對話框顯示過的錯誤訊息。");
        }
    }

    [Test]
    public void SettingsTheme_WhenDarkIsSelected_PersistsPreference() {
        Session.Launch();
        Session.WaitForEnabledButton("開始匯出");

        // 一律以 UI Automation 模式操作而非滑鼠或鍵盤輸入，輸入會在視窗不在前景時落到其他視窗。
        Session.WaitForElement("設定", ControlType.Hyperlink).Patterns.Invoke.Pattern.Invoke();
        Session.WaitForElement("主題", ControlType.ComboBox).Patterns.ExpandCollapse.Pattern.Expand();
        Session.WaitForElement("深色", ControlType.ListItem).Patterns.Invoke.Pattern.Invoke();

        string preferencePath = Path.Combine(Session.UserConfigDirectory, "desktop.json");
        bool persisted = FlaUI.Core.Tools.Retry.WhileFalse(
            () => File.Exists(preferencePath) && File.ReadAllText(preferencePath).Contains("Dark", StringComparison.Ordinal),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMilliseconds(250)
        ).Result;
        Assert.That(persisted, Is.True);

        Session.Close();
        Session.Launch();
        Assert.That(Session.WaitForElement("匯出 Schema"), Is.Not.Null, "重新啟動後應能以儲存的主題正常顯示。");
    }
}
