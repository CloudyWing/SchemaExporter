namespace CloudyWing.SchemaExporter.Core.Tests.Infrastructure;

internal sealed class TempDirectoryScope : IDisposable {
    public TempDirectoryScope() {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"SchemaExporter.Tests.{Guid.NewGuid():N}"
        );

        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose() {
        if (Directory.Exists(Path)) {
            Directory.Delete(Path, recursive: true);
        }
    }
}
