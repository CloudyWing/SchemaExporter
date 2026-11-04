using CloudyWing.SchemaExporter.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace CloudyWing.SchemaExporter.Infrastructure.Tests.Configuration;

[TestFixture]
[NonParallelizable]
public sealed class SchemaExporterConfigurationTests {
    [Test]
    public void Build_WhenUserConfigIsNotRequired_DoesNotCreateUserConfig() {
        using AppSettingsTestScope scope = new();

        IConfiguration result = SchemaExporterConfiguration.Build(requiresUserConfig: false);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.GetChildren(), Is.Empty);
            Assert.That(File.Exists(scope.UserConfigPath), Is.False);
        }
    }

    [Test]
    public async Task Build_WhenUserConfigIsRequiredAndMissing_CopiesInstallTemplateAndReadsIt() {
        using AppSettingsTestScope scope = new();
        await File.WriteAllTextAsync(scope.InstallConfigPath, """
            {
              "Schema": {
                "ExportPath": "C:\\Template\\Exports"
              }
            }
            """);

        IConfiguration result = SchemaExporterConfiguration.Build(requiresUserConfig: true);

        using (Assert.EnterMultipleScope()) {
            Assert.That(File.Exists(scope.UserConfigPath), Is.True);
            Assert.That(result["Schema:ExportPath"], Is.EqualTo(@"C:\Template\Exports"));
        }
    }
}
