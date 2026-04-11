using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.SchemaProviders;
using CloudyWing.SchemaExporter.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CloudyWing.SchemaExporter.Tests;

[TestFixture]
public sealed class ViewModelTests {
    [Test]
    public void ViewModel_CanBeConstructedWithInternalDependencies() {
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        SchemaExportOrchestrator exportOrchestrator = new(
            Substitute.For<IDatabaseSchemaProviderFactory>(),
            Substitute.For<ILogger<SchemaExportOrchestrator>>()
        );
        ViewModel sut = new(settingsService, exportOrchestrator);

        Assert.That(sut, Is.Not.Null);
    }
}
