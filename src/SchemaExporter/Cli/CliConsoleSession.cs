using System.IO;
using System.Text;

namespace CloudyWing.SchemaExporter.Cli;

internal sealed class CliConsoleSession : IDisposable {
    private const int AttachParentProcess = -1;
    private readonly bool hasConsole;

    private CliConsoleSession(bool hasConsole) {
        this.hasConsole = hasConsole;
    }

    public static CliConsoleSession Attach() {
        bool attached = AttachConsole(AttachParentProcess) || AllocConsole();
        if (attached) {
            InitializeConsoleStreams();
        }

        return new CliConsoleSession(attached);
    }

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

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool AllocConsole();

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool AttachConsole(int processId);

}
