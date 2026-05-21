using CloudyWing.SchemaExporter.Core.Exporting;

namespace CloudyWing.SchemaExporter.Core.Tests.Exporting;

[TestFixture]
public sealed class ExportDiagnosticTests {
    [Test]
    public void SeverityText_WhenSeverityIsError_ReturnsLocalizedText() {
        ExportDiagnostic diagnostic = new() {
            Severity = DiagnosticSeverity.Error,
            Message = "failed"
        };

        Assert.That(diagnostic.SeverityText, Is.EqualTo("錯誤"));
    }
}
