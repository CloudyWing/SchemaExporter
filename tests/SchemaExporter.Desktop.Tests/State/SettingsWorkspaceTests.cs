using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Desktop.Models;
using CloudyWing.SchemaExporter.Desktop.State;
using CloudyWing.SchemaExporter.Infrastructure.Configuration;
using NSubstitute;

namespace CloudyWing.SchemaExporter.Desktop.Tests.State;

[TestFixture]
public sealed class SettingsWorkspaceTests {
    [Test]
    public async Task SaveAsync_WhenEdited_KeepsExportDefaultsLastSelectionsAndRedaction() {
        SchemaOptions loadedOptions = CreateOptions();
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.ReadAsync().Returns(Task.FromResult(loadedOptions));
        SchemaOptions? savedOptions = null;
        settingsService.SaveAsync(Arg.Do<SchemaOptions>(options => savedOptions = options)).Returns(Task.CompletedTask);
        SettingsWorkspace sut = new(settingsService);
        await sut.LoadAsync();

        sut.AddConnection();
        sut.SelectedConnection!.ConnectionString = "  Server=new;Database=App;  ";
        sut.RedactionReplacementText = "[HIDDEN]";
        sut.SensitiveNamePatternsText = "password\n\n  token  \nPASSWORD";

        bool result = await sut.SaveAsync();

        Assert.That(savedOptions, Is.Not.Null);
        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.True);
            Assert.That(savedOptions!.ExportPath, Is.EqualTo(@"C:\Exports"));
            Assert.That(savedOptions.ExportResultOptions.GenerateSchemaSummary, Is.True);
            Assert.That(savedOptions.LastSelectedConnectionName, Is.EqualTo("Primary"));
            Assert.That(savedOptions.LastSelectedProfileName, Is.EqualTo("Full"));
            Assert.That(savedOptions.Connections.Select(x => x.Name), Is.EqualTo(new[] { "Primary", "New Connection" }));
            Assert.That(savedOptions.Connections[1].ConnectionString, Is.EqualTo("Server=new;Database=App;"));
            Assert.That(savedOptions.Redaction.Enabled, Is.True);
            Assert.That(savedOptions.Redaction.ReplacementText, Is.EqualTo("[HIDDEN]"));
            Assert.That(savedOptions.Redaction.SensitiveNamePatterns, Is.EqualTo(new[] { "password", "token" }));
        }
    }

    [Test]
    public async Task RenameProfile_WhenConnectionsReferenceOldName_UpdatesReferences() {
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.ReadAsync().Returns(Task.FromResult(CreateOptions()));
        SettingsWorkspace sut = new(settingsService);
        await sut.LoadAsync();
        EditableExportProfile profile = sut.Profiles.Single(x => x.Name == "Full");

        sut.RenameProfile(profile, "Everything");

        using (Assert.EnterMultipleScope()) {
            Assert.That(profile.Name, Is.EqualTo("Everything"));
            Assert.That(sut.Connections.Single().ExportProfileName, Is.EqualTo("Everything"));
        }
    }

    [Test]
    public async Task RemoveSelectedProfile_WhenConnectionsReferenceIt_ClearsReferencesAndSelectsNeighbor() {
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.ReadAsync().Returns(Task.FromResult(CreateOptions()));
        SettingsWorkspace sut = new(settingsService);
        await sut.LoadAsync();
        sut.SelectedProfile = sut.Profiles.Single(x => x.Name == "Full");

        sut.RemoveSelectedProfile();

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.Connections.Single().ExportProfileName, Is.Null);
            Assert.That(sut.SelectedProfile?.Name, Is.EqualTo("TablesOnly"));
        }
    }

    [Test]
    public async Task AddConnection_WhenDefaultNameExists_CreatesUniqueName() {
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.ReadAsync().Returns(Task.FromResult(CreateOptions()));
        SettingsWorkspace sut = new(settingsService);
        await sut.LoadAsync();

        sut.AddConnection();
        sut.AddConnection();

        Assert.That(sut.Connections.Select(x => x.Name), Is.EqualTo(new[] { "Primary", "New Connection", "New Connection 1" }));
    }

    [Test]
    public async Task SaveAsync_WhenValidationFails_ReturnsFalseWithMessage() {
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.ReadAsync().Returns(Task.FromResult(CreateOptions()));
        settingsService.SaveAsync(Arg.Any<SchemaOptions>()).Returns(Task.FromException(new ExportValidationException("連線名稱不可重複。")));
        SettingsWorkspace sut = new(settingsService);
        await sut.LoadAsync();

        bool result = await sut.SaveAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.False);
            Assert.That(sut.ErrorMessage, Is.EqualTo("連線名稱不可重複。"));
        }
    }

    [Test]
    public async Task LoadAsync_WhenSettingsAreCorrupt_ReportsErrorAndBlocksSave() {
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.ReadAsync().Returns(Task.FromException<SchemaOptions>(new InvalidOperationException("格式無效")));
        SettingsWorkspace sut = new(settingsService);

        await sut.LoadAsync();
        bool saved = await sut.SaveAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.IsLoaded, Is.False);
            Assert.That(sut.ErrorMessage, Does.Contain("格式無效"));
            Assert.That(saved, Is.False);
            await settingsService.DidNotReceive().SaveAsync(Arg.Any<SchemaOptions>());
        }
    }

    [Test]
    public async Task LoadAsync_WhenSettingsFailValidation_StillLoadsForRepair() {
        SchemaOptions invalidOptions = CreateOptions();
        ISettingsService settingsService = Substitute.For<ISettingsService>();
        settingsService.ReadAsync().Returns(Task.FromResult(invalidOptions));
        settingsService.LoadAsync().Returns(Task.FromException<SchemaOptions>(new ExportValidationException("連線名稱不可重複。")));
        SettingsWorkspace sut = new(settingsService);

        await sut.LoadAsync();
        bool saved = await sut.SaveAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.IsLoaded, Is.True);
            Assert.That(saved, Is.True);
            await settingsService.DidNotReceive().LoadAsync();
        }
    }

    [Test]
    public void EditableExportProfile_WhenTextIsAssigned_NormalizesLines() {
        EditableExportProfile sut = new();

        sut.IncludeSchemasText = " dbo \r\n\r\nsales\nDBO";

        Assert.That(sut.IncludeSchemas, Is.EqualTo(new[] { "dbo", "sales" }));
    }

    private static SchemaOptions CreateOptions() {
        return new SchemaOptions {
            ExportPath = @"C:\Exports",
            Connections = [
                new SchemaConnection {
                    Name = "Primary",
                    ConnectionString = "Server=.;Database=App;",
                    ExportProfileName = "Full"
                }
            ],
            ExportProfiles = [
                new ExportProfile { Name = "Full" },
                new ExportProfile { Name = "TablesOnly", IncludeViews = false }
            ],
            ExportResultOptions = new ExportResultOptions { GenerateSchemaSummary = true },
            Redaction = new SchemaRedactionOptions { Enabled = true },
            LastSelectedConnectionName = "Primary",
            LastSelectedProfileName = "Full"
        };
    }
}
