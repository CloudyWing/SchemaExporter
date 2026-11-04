using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Infrastructure.Configuration;
using CloudyWing.SchemaExporter.Infrastructure.Shell;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CloudyWing.SchemaExporter.Infrastructure.Tests;

[TestFixture]
public sealed class ServiceCollectionExtensionsTests {
    [Test]
    public void AddSchemaExporterInfrastructure_WithCore_ResolvesSharedHostServices() {
        ServiceCollection services = new();
        services.AddSchemaExporterCore(new ConfigurationBuilder().Build());
        services.AddSchemaExporterInfrastructure();

        using ServiceProvider provider = services.BuildServiceProvider();

        using (Assert.EnterMultipleScope()) {
            Assert.That(provider.GetRequiredService<ISettingsService>(), Is.InstanceOf<JsonSettingsService>());
            Assert.That(provider.GetRequiredService<IOutputFolderLauncher>(), Is.InstanceOf<ShellOutputFolderLauncher>());
            Assert.That(provider.GetRequiredService<SchemaExportOrchestrator>(), Is.Not.Null);
        }
    }
}
