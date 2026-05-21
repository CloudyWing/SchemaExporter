using CloudyWing.SchemaExporter.Core.Exporting;

namespace CloudyWing.SchemaExporter.Core.Tests.Exporting;

[TestFixture]
public sealed class SchemaExportRequestResolverTests {
    [Test]
    public void Resolve_WhenOverridesAreProvided_ReturnsMergedRequest() {
        SchemaOptions options = CreateOptions();
        options.Redaction = new SchemaRedactionOptions {
            Enabled = true,
            ReplacementText = "[MASKED]",
            SensitiveNamePatterns = ["token"],
            SensitiveTextPatterns = ["AKIA[0-9A-Z]+"]
        };
        SchemaExportRequestResolver sut = new();
        ExportOptionOverrides overrides = new() {
            OutputPath = @"C:\Runtime",
            GenerateManifest = true,
            GenerateJsonSidecar = true,
            GenerateMarkdownSidecar = false,
            GenerateSchemaSummary = true,
            GenerateSchemaSnapshot = true,
            OpenOutputFolder = true,
            UseTimestamp = false,
            DiffSourceSnapshotPath = null,
            OverrideDiffSourceSnapshotPath = true
        };

        SchemaExportRequest result = sut.Resolve(options, "Analytics", "Compact", overrides);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.Connection.Name, Is.EqualTo("Analytics"));
            Assert.That(result.Profile.Name, Is.EqualTo("Compact"));
            Assert.That(result.ExportPath, Is.EqualTo(@"C:\Runtime"));
            Assert.That(result.ResultOptions.GenerateManifest, Is.True);
            Assert.That(result.ResultOptions.GenerateJsonSidecar, Is.True);
            Assert.That(result.ResultOptions.GenerateMarkdownSidecar, Is.False);
            Assert.That(result.ResultOptions.GenerateSchemaSummary, Is.True);
            Assert.That(result.ResultOptions.GenerateSchemaSnapshot, Is.True);
            Assert.That(result.ResultOptions.OpenOutputFolder, Is.True);
            Assert.That(result.ResultOptions.UseTimestamp, Is.False);
            Assert.That(result.ResultOptions.TimestampFormat, Is.EqualTo("yyyyMMdd"));
            Assert.That(result.ResultOptions.OverwriteStrategy, Is.EqualTo(OverwriteStrategy.AppendSuffix));
            Assert.That(result.ResultOptions.DiffSourceSnapshotPath, Is.Null);
            Assert.That(result.Redaction.Enabled, Is.True);
            Assert.That(result.Redaction.ReplacementText, Is.EqualTo("[MASKED]"));
            Assert.That(result.Redaction.SensitiveNamePatterns, Is.EqualTo(new[] { "token" }));
            Assert.That(result.Redaction.SensitiveTextPatterns, Is.EqualTo(new[] { "AKIA[0-9A-Z]+" }));
        }
    }

    [Test]
    public void Resolve_WhenNamesAreOmitted_UsesLastSelectedConnectionAndConnectionProfile() {
        SchemaOptions options = CreateOptions();
        SchemaExportRequestResolver sut = new();

        SchemaExportRequest result = sut.Resolve(options, null, null);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.Connection.Name, Is.EqualTo("Analytics"));
            Assert.That(result.Profile.Name, Is.EqualTo("Compact"));
        }
    }

    [Test]
    public void Resolve_WhenExplicitProfileDoesNotExist_ThrowsValidationException() {
        SchemaOptions options = CreateOptions();
        SchemaExportRequestResolver sut = new();

        ExportValidationException? exception = Assert.Throws<ExportValidationException>(
            () => sut.Resolve(options, "Primary", "Missing")
        );

        Assert.That(exception?.Message, Does.Contain("Missing"));
    }

    private static SchemaOptions CreateOptions() {
        return new SchemaOptions {
            ExportPath = @"C:\Default",
            Connections = [
                new SchemaConnection {
                    Name = "Primary",
                    ConnectionString = "Server=Primary;Database=SchemaExporter;",
                    ExportProfileName = "Default"
                },
                new SchemaConnection {
                    Name = "Analytics",
                    ConnectionString = "Server=Analytics;Database=SchemaExporter;",
                    ExportProfileName = "Compact"
                }
            ],
            LastSelectedConnectionName = "Analytics",
            ExportProfiles = [
                new ExportProfile {
                    Name = "Default"
                },
                new ExportProfile {
                    Name = "Compact"
                }
            ],
            ExportResultOptions = new ExportResultOptions {
                UseTimestamp = true,
                TimestampFormat = "yyyyMMdd",
                OverwriteStrategy = OverwriteStrategy.AppendSuffix,
                GenerateMarkdownSidecar = true,
                DiffSourceSnapshotPath = @"C:\Baseline\schema.snapshot.json"
            }
        };
    }
}
