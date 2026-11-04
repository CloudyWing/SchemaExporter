using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Conditions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace CloudyWing.SchemaExporter.Desktop.UITests;

/// <summary>
/// 以暫時的 LOCALAPPDATA 啟動 Desktop 執行檔，提供 FlaUI 操作與失敗時截圖，結束時關閉程式並清除暫存資料。
/// </summary>
internal sealed class DesktopAppSession : IDisposable {
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private readonly UIA3Automation automation = new();
    private Application? application;

    private DesktopAppSession(string rootDirectory) {
        RootDirectory = rootDirectory;
        LocalAppDataDirectory = Path.Combine(rootDirectory, "LocalAppData");
        ExportDirectory = Path.Combine(rootDirectory, "exports");
        Directory.CreateDirectory(Path.Combine(LocalAppDataDirectory, "SchemaExporter"));
        Directory.CreateDirectory(ExportDirectory);
    }

    public string RootDirectory { get; }

    public string LocalAppDataDirectory { get; }

    public string ExportDirectory { get; }

    public string UserConfigDirectory => Path.Combine(LocalAppDataDirectory, "SchemaExporter");

    public Window MainWindow { get; private set; } = null!;

    public ConditionFactory Conditions => automation.ConditionFactory;

    /// <summary>
    /// 建立工作階段並寫入使用者設定檔；連線指向不存在的本機埠，連線會立即失敗，不需要資料庫。
    /// </summary>
    public static DesktopAppSession Create() {
        string rootDirectory = Path.Combine(Path.GetTempPath(), "SchemaExporter.Desktop.UITests", Guid.NewGuid().ToString("N"));
        DesktopAppSession session = new(rootDirectory);
        session.WriteUserConfig();
        return session;
    }

    public static string ResolveExePath() {
        string? overridePath = Environment.GetEnvironmentVariable("SCHEMAEXPORTER_DESKTOP_EXE");
        string? path = !string.IsNullOrWhiteSpace(overridePath)
            ? overridePath
            : typeof(DesktopAppSession).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(x => x.Key == "DesktopExePath")?.Value;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
            Assert.Inconclusive($"找不到 Desktop 執行檔：{path}。請先建置 SchemaExporter.Desktop，或以 SCHEMAEXPORTER_DESKTOP_EXE 指定路徑。");
        }

        return Path.GetFullPath(path!);
    }

    public ProcessStartInfo CreateStartInfo(params string[] arguments) {
        ProcessStartInfo startInfo = new(ResolveExePath()) {
            UseShellExecute = false,
            WorkingDirectory = RootDirectory
        };
        foreach (string argument in arguments) {
            startInfo.ArgumentList.Add(argument);
        }

        // AppPaths 優先讀取 LOCALAPPDATA，測試因此不會碰到使用者的真實設定。
        startInfo.Environment["LOCALAPPDATA"] = LocalAppDataDirectory;
        return startInfo;
    }

    public void Launch() {
        application = Application.Launch(CreateStartInfo());
        MainWindow = application.GetMainWindow(automation, DefaultTimeout)
            ?? throw new AssertionException("主視窗未在時限內出現。");
    }

    public void Close() {
        if (application is null) {
            return;
        }

        int processId = application.ProcessId;
        try {
            if (!application.HasExited) {
                application.Close();
            }
        } finally {
            application.Dispose();
            application = null;
        }

        // 正常關閉後等待處理序結束，逾時才強制終止，避免殘留的程式影響下一個測試。
        try {
            using Process process = Process.GetProcessById(processId);
            if (!process.WaitForExit(TimeSpan.FromSeconds(10))) {
                process.Kill(entireProcessTree: true);
            }
        } catch (ArgumentException) {
            // 處理序已結束。
        }
    }

    /// <summary>
    /// 在主視窗（含 WebView2 內容）中等待指定名稱的元素出現。
    /// </summary>
    public AutomationElement WaitForElement(string name, TimeSpan? timeout = null) {
        RetryResult<AutomationElement?> result = Retry.WhileNull(
            () => MainWindow.FindFirstDescendant(Conditions.ByName(name)),
            timeout ?? DefaultTimeout,
            TimeSpan.FromMilliseconds(250)
        );

        return result.Result ?? throw new AssertionException($"找不到名稱為「{name}」的元素。");
    }

    /// <summary>
    /// 等待指定名稱的按鈕出現且可以按下。
    /// </summary>
    public Button WaitForEnabledButton(string name, TimeSpan? timeout = null) {
        RetryResult<AutomationElement?> result = Retry.WhileNull(
            () => MainWindow
                .FindAllDescendants(Conditions.ByControlType(FlaUI.Core.Definitions.ControlType.Button))
                .FirstOrDefault(x => x.Name.Contains(name, StringComparison.Ordinal) && x.IsEnabled),
            timeout ?? DefaultTimeout,
            TimeSpan.FromMilliseconds(250)
        );

        return result.Result?.AsButton() ?? throw new AssertionException($"找不到可按下的按鈕「{name}」。");
    }

    /// <summary>
    /// 等待主視窗出現原生的強制回應視窗（MessageBox 或檔案對話框）。
    /// </summary>
    public Window WaitForModalWindow(TimeSpan? timeout = null) {
        RetryResult<Window?> result = Retry.WhileNull(
            () => MainWindow.ModalWindows.FirstOrDefault(),
            timeout ?? DefaultTimeout,
            TimeSpan.FromMilliseconds(250)
        );

        return result.Result ?? throw new AssertionException("強制回應視窗未在時限內出現。");
    }

    /// <summary>
    /// 擷取整個螢幕並加入測試附件，方便判斷 UI 測試失敗的原因。
    /// </summary>
    public void CaptureScreen(string name) {
        string path = Path.Combine(RootDirectory, $"{name}.png");
        Capture.Screen().ToFile(path);
        TestContext.AddTestAttachment(path, name);
    }

    public void Dispose() {
        try {
            Close();
        } finally {
            automation.Dispose();
            TryDeleteDirectory(RootDirectory);
        }
    }

    private void WriteUserConfig() {
        var config = new {
            Schema = new {
                ExportPath = ExportDirectory,
                Connections = new[] {
                    new {
                        Name = "Unreachable",
                        DatabaseType = "SqlServer",
                        ConnectionString = "Server=tcp:127.0.0.1,1;Database=None;User Id=sa;Password=UiTestSecret;Connect Timeout=2;TrustServerCertificate=True",
                        ExportProfileName = "Default"
                    }
                },
                ExportProfiles = new[] {
                    new { Name = "Default", IncludeViews = true }
                },
                ExportResultOptions = new {
                    UseTimestamp = false,
                    GenerateManifest = true
                }
            }
        };
        File.WriteAllText(
            Path.Combine(UserConfigDirectory, "appsettings.json"),
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })
        );
    }

    private static void TryDeleteDirectory(string path) {
        // WebView2 的使用者資料夾可能在處理序結束後短暫被鎖定，刪不掉時保留給系統暫存清理。
        try {
            if (Directory.Exists(path)) {
                Directory.Delete(path, recursive: true);
            }
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }
}
