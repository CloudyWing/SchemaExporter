using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;
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
            Substitute.For<ILogger<SchemaExportOrchestrator>>(),
            new SchemaSnapshotBuilder(),
            new SchemaSnapshotDiffService()
        );
        ViewModel sut = new(settingsService, exportOrchestrator, new SchemaExportRequestResolver());

        Assert.That(sut, Is.Not.Null);
    }
}
