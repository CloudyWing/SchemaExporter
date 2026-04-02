using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CloudyWing.SchemaExporter.Cli;

/// <summary>
/// 管理 CLI 模式下的主控台工作階段，負責附加或配置主控台並初始化串流編碼。
/// </summary>
internal sealed partial class CliConsoleSession : IDisposable {
    private const int AttachParentProcess = -1;
    private readonly bool hasConsole;

    private CliConsoleSession(bool hasConsole) {
        this.hasConsole = hasConsole;
    }

    /// <summary>
    /// 附加至父處理序的主控台或配置新主控台，並初始化 UTF-8 編碼。
    /// </summary>
    /// <returns>表示主控台工作階段的 <see cref="CliConsoleSession"/> 執行個體。</returns>
    public static CliConsoleSession Attach() {
        bool attached = AttachConsole(AttachParentProcess) || AllocConsole();
        if (attached) {
            InitializeConsoleStreams();
        }

        return new CliConsoleSession(attached);
    }

    /// <summary>
    /// 釋放此工作階段所使用的資源。
    /// </summary>
    public void Dispose() {
        _ = hasConsole;
    }

    private static void InitializeConsoleStreams() {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
        Console.SetIn(new StreamReader(Console.OpenStandardInput(), Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: false));
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true });
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllocConsole();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int processId);

}
