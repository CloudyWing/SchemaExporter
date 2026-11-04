using Bunit;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;
using CloudyWing.SchemaExporter.Core.SchemaProviders;
using CloudyWing.SchemaExporter.Desktop.Services;
using CloudyWing.SchemaExporter.Desktop.State;
using CloudyWing.SchemaExporter.Infrastructure.Configuration;
using CloudyWing.SchemaExporter.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using NSubstitute;

namespace CloudyWing.SchemaExporter.Desktop.Tests.Components;

/// <summary>
/// 建立 bUnit 元件測試環境：MudBlazor 服務、真正的 State 類別，以及以替身取代的檔案、對話框與資料庫依賴。
/// </summary>
internal sealed class ComponentTestHost : IDisposable {
    private readonly TemporaryDirectory directory = new();

    public ComponentTestHost(SchemaOptions options) {
        SettingsService = Substitute.For<ISettingsService>();
        SettingsService.LoadAsync().Returns(_ => Task.FromResult(options));
        SettingsService.ReadAsync().Returns(_ => Task.FromResult(options));
        ProviderFactory = Substitute.For<IDatabaseSchemaProviderFactory>();
        DialogService = Substitute.For<IDialogService>();
        Protector = Substitute.For<IConnectionStringProtector>();
        Protector.Protect(Arg.Any<string>()).Returns(callInfo => "dpapi:" + callInfo.Arg<string>());
        IOutputFolderLauncher launcher = Substitute.For<IOutputFolderLauncher>();

        Context = new BunitContext();
        // MudBlazor 元件會呼叫 JS interop，測試中不需要實際的 JS 行為。
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.Services.AddMudServices();
        SchemaExportOrchestrator orchestrator = new(
            ProviderFactory,
            Substitute.For<ILogger<SchemaExportOrchestrator>>(),
            new SchemaSnapshotBuilder(),
            new SchemaSnapshotDiffService(),
            launcher
        );
        Context.Services.AddSingleton(new ExportWorkspace(
            SettingsService,
            orchestrator,
            new SchemaExportRequestResolver(),
            DialogService,
            Substitute.For<IFileDialogService>(),
            launcher
        ));
        Context.Services.AddSingleton(new SettingsWorkspace(SettingsService, Protector));
        Context.Services.AddSingleton(new UpdateWorkspace(Substitute.For<IUpdateService>(), DialogService));
        Context.Services.AddSingleton(new ThemePreference(Path.Combine(directory.Path, "desktop.json")));
    }

    public BunitContext Context { get; }

    public ISettingsService SettingsService { get; }

    public IDatabaseSchemaProviderFactory ProviderFactory { get; }

    public IDialogService DialogService { get; }

    public IConnectionStringProtector Protector { get; }

    public static SchemaOptions CreateOptions(string exportPath, params SchemaConnection[] connections) {
        return new SchemaOptions {
            ExportPath = exportPath,
            Connections = connections.Length > 0
                ? connections
                : [new SchemaConnection { Name = "Primary", ConnectionString = "Server=.;Database=App;" }],
            ExportProfiles = [new ExportProfile { Name = "Default" }],
            ExportResultOptions = new ExportResultOptions { GenerateManifest = true, UseTimestamp = false }
        };
    }

    public void Dispose() {
        // MudBlazor 部分服務只實作 IAsyncDisposable，必須以 DisposeAsync 釋放容器。
        Context.DisposeAsync().AsTask().GetAwaiter().GetResult();
        directory.Dispose();
    }
}
