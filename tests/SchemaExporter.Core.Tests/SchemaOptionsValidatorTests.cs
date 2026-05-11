using CloudyWing.SchemaExporter.Core.Exporting;

namespace CloudyWing.SchemaExporter.Core.Tests;

[TestFixture]
public sealed class SchemaOptionsValidatorTests {
    [Test]
    public void Validate_WhenConnectionProfileReferenceIsMissing_ThrowsValidationException() {
        SchemaOptions options = CreateValidOptions(connections: [
            new SchemaConnection {
                Name = "Primary",
                ConnectionString = "Server=.;Database=SchemaExporter;",
                ExportProfileName = "Missing"
            }
        ]);

        ExportValidationException? exception = Assert.Throws<ExportValidationException>(
            () => SchemaOptionsValidator.Validate(options)
        );

        Assert.That(exception?.Message, Does.Contain("ExportProfileName"));
    }

    [Test]
    public void Validate_WhenDiffSourceSnapshotPathIsRelative_ThrowsValidationException() {
        SchemaOptions options = CreateValidOptions();
        options.ExportResultOptions = new ExportResultOptions {
            DiffSourceSnapshotPath = "baseline.snapshot.json"
        };

        ExportValidationException? exception = Assert.Throws<ExportValidationException>(
            () => SchemaOptionsValidator.Validate(options)
        );

        Assert.That(exception?.Message, Does.Contain("DiffSourceSnapshotPath"));
    }

    [Test]
    public void Validate_WhenOptionsAreValid_DoesNotThrow() {
        SchemaOptions options = CreateValidOptions();

        Assert.DoesNotThrow(() => SchemaOptionsValidator.Validate(options));
    }

    private static SchemaOptions CreateValidOptions(IReadOnlyList<SchemaConnection>? connections = null) {
        return new SchemaOptions {
            ExportPath = @"C:\Exports",
            Connections = connections ?? [
                new SchemaConnection {
                    Name = "Primary",
                    ConnectionString = "Server=.;Database=SchemaExporter;",
                    ExportProfileName = "Default"
                }
            ],
            ExportProfiles = [
                new ExportProfile {
                    Name = "Default"
                }
            ],
            ExportResultOptions = new ExportResultOptions()
        };
    }
}
