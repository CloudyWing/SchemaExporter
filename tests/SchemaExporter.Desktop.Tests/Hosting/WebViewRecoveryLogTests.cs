using CloudyWing.SchemaExporter.Desktop.Hosting;

namespace CloudyWing.SchemaExporter.Desktop.Tests.Hosting;

[TestFixture]
public sealed class WebViewRecoveryLogTests {
    [Test]
    public void Write_AppendsOneLinePerEntryWithUtcTimestamp() {
        using TemporaryDirectory directory = new();
        string filePath = Path.Combine(directory.Path, "logs", "webview-recovery.log");
        ManualTimeProvider time = new(new DateTimeOffset(2026, 3, 4, 5, 6, 7, 89, TimeSpan.FromHours(8)));
        WebViewRecoveryLog sut = new(filePath, time);

        sut.Write("Trigger", "SessionUnlock");
        time.Advance(TimeSpan.FromSeconds(3));
        sut.Write("RebuildStarted");

        string[] lines = File.ReadAllLines(filePath);
        Assert.That(lines, Is.EqualTo(new[] {
            "2026-03-03T21:06:07.089Z Trigger SessionUnlock",
            "2026-03-03T21:06:10.089Z RebuildStarted"
        }));
    }

    [Test]
    public void Write_DetailWithLineBreaks_StaysOnOneLine() {
        using TemporaryDirectory directory = new();
        string filePath = Path.Combine(directory.Path, "webview-recovery.log");
        WebViewRecoveryLog sut = new(filePath, new ManualTimeProvider());

        sut.Write("ExceptionSwallowed", "first\r\nsecond\nthird");

        string[] lines = File.ReadAllLines(filePath);
        Assert.That(lines, Has.Length.EqualTo(1));
        Assert.That(lines[0], Does.EndWith("ExceptionSwallowed first second third"));
    }

    [Test]
    public void Write_FileLargerThanLimit_RotatesToDotOneAndOverwritesOldRotation() {
        using TemporaryDirectory directory = new();
        string filePath = Path.Combine(directory.Path, "webview-recovery.log");
        File.WriteAllText(filePath + ".1", "previous rotation");
        File.WriteAllText(filePath, new string('x', (int)WebViewRecoveryLog.MaxFileBytes + 1));
        WebViewRecoveryLog sut = new(filePath, new ManualTimeProvider());

        sut.Write("Trigger", "DisplaySettingsChanged");

        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllText(filePath + ".1"), Has.Length.EqualTo((int)WebViewRecoveryLog.MaxFileBytes + 1));
            Assert.That(File.ReadAllLines(filePath), Has.Length.EqualTo(1));
        }
    }

    [Test]
    public void Write_FileWithinLimit_KeepsAppendingToSameFile() {
        using TemporaryDirectory directory = new();
        string filePath = Path.Combine(directory.Path, "webview-recovery.log");
        WebViewRecoveryLog sut = new(filePath, new ManualTimeProvider());

        sut.Write("A");
        sut.Write("B");

        using (Assert.EnterMultipleScope()) {
            Assert.That(File.ReadAllLines(filePath), Has.Length.EqualTo(2));
            Assert.That(File.Exists(filePath + ".1"), Is.False);
        }
    }

    [Test]
    public void Write_WhenDirectoryCannotBeCreated_DoesNotThrow() {
        using TemporaryDirectory directory = new();
        string blocker = Path.Combine(directory.Path, "blocker");
        File.WriteAllText(blocker, "this is a file, not a directory");
        WebViewRecoveryLog sut = new(Path.Combine(blocker, "logs", "webview-recovery.log"), new ManualTimeProvider());

        Assert.DoesNotThrow(() => sut.Write("Trigger", "SessionUnlock"));
    }
}
