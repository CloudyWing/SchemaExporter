using CloudyWing.SchemaExporter.Desktop.State;

namespace CloudyWing.SchemaExporter.Desktop.Tests.State;

[TestFixture]
public sealed class ThemePreferenceTests {
    [Test]
    public void Constructor_WhenFileIsMissing_UsesSystemMode() {
        using TemporaryDirectory directory = new();

        ThemePreference sut = new(Path.Combine(directory.Path, "desktop.json"));

        Assert.That(sut.Mode, Is.EqualTo(ThemeMode.System));
    }

    [Test]
    public async Task SetModeAsync_WhenModeChanges_PersistsAndNotifies() {
        using TemporaryDirectory directory = new();
        string filePath = Path.Combine(directory.Path, "nested", "desktop.json");
        ThemePreference sut = new(filePath);
        int changedCount = 0;
        sut.Changed += () => changedCount++;

        await sut.SetModeAsync(ThemeMode.Dark);

        ThemePreference reloaded = new(filePath);
        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.Mode, Is.EqualTo(ThemeMode.Dark));
            Assert.That(changedCount, Is.EqualTo(1));
            Assert.That(reloaded.Mode, Is.EqualTo(ThemeMode.Dark));
            Assert.That(sut.ErrorMessage, Is.Null);
        }
    }

    [TestCase("{ not json")]
    [TestCase("""{ "ThemeMode": "Purple" }""")]
    [TestCase("""{ "ThemeMode": 99 }""")]
    public async Task Constructor_WhenFileIsInvalid_FallsBackToSystemMode(string content) {
        using TemporaryDirectory directory = new();
        string filePath = Path.Combine(directory.Path, "desktop.json");
        await File.WriteAllTextAsync(filePath, content);

        ThemePreference sut = new(filePath);

        Assert.That(sut.Mode, Is.EqualTo(ThemeMode.System));
    }
}
