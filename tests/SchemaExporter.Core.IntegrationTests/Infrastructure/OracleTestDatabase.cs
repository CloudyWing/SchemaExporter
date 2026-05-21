using DotNet.Testcontainers.Containers;
using Testcontainers.Oracle;

namespace CloudyWing.SchemaExporter.Core.IntegrationTests.Infrastructure;

internal sealed class OracleTestDatabase : IAsyncDisposable {
    private const string ImageName = "gvenzl/oracle-xe:21.3.0-slim-faststart";
    private readonly OracleContainer container;

    private OracleTestDatabase(OracleContainer container) {
        this.container = container;
        ConnectionString = container.GetConnectionString();
    }

    public string ConnectionString { get; }

    public static async Task<OracleTestDatabase> CreateAsync() {
        OracleContainer container = new OracleBuilder(ImageName).Build();

        try {
            await container.StartAsync();

            string script = await File.ReadAllTextAsync(ProviderFixtureFiles.GetSchemaScriptPath("oracle"));
            await container.ExecScriptAsync(script).ThrowOnFailure();

            return new OracleTestDatabase(container);
        } catch {
            await container.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync() {
        await container.DisposeAsync();
    }
}

