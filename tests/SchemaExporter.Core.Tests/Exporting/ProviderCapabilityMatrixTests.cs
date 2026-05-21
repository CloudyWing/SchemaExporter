using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;

namespace CloudyWing.SchemaExporter.Core.Tests.Exporting;

[TestFixture]
public sealed class ProviderCapabilityMatrixTests {
    [Test]
    public void GetCapabilities_WhenDatabaseTypeIsSqlServer_ReturnsExpectedSupportRows() {
        IReadOnlyList<ProviderCapability> result = ProviderCapabilityMatrix.GetCapabilities(
            DatabaseType.SqlServer.ToString()
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.Any(x => x.Area == "Tables" && x.SupportLevel == ExportSupportLevel.Full), Is.True);
            Assert.That(result.Any(x => x.Area == "Views" && x.SupportLevel == ExportSupportLevel.Partial), Is.True);
            Assert.That(
                result.Any(x => x.Area == "Routines" && x.Notes.Contains("sys.sql_modules", StringComparison.Ordinal)),
                Is.True
            );
        }
    }

    [Test]
    public void GetCapabilities_WhenDatabaseTypeIsOracle_ReturnsExpectedSupportRows() {
        IReadOnlyList<ProviderCapability> result = ProviderCapabilityMatrix.GetCapabilities(
            DatabaseType.Oracle.ToString()
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.Any(x => x.Area == "Tables" && x.SupportLevel == ExportSupportLevel.Full), Is.True);
            Assert.That(result.Any(x => x.Area == "Columns" && x.SupportLevel == ExportSupportLevel.Partial), Is.True);
            Assert.That(
                result.Any(x => x.Area == "Routines" && x.Notes.Contains("package routines", StringComparison.Ordinal)),
                Is.True
            );
        }
    }
}
