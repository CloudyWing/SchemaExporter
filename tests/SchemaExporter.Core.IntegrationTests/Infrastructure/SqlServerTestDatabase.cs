using System.Data.Common;
using DotNet.Testcontainers.Containers;
using Testcontainers.MsSql;

namespace CloudyWing.SchemaExporter.Core.IntegrationTests.Infrastructure;

internal sealed class SqlServerTestDatabase : IAsyncDisposable {
    private const string DatabaseName = "SchemaExporterFixture";
    private const string ImageName = "mcr.microsoft.com/mssql/server:2025-CU4-GDR1-ubuntu-24.04";
    private readonly MsSqlContainer container;

    private SqlServerTestDatabase(MsSqlContainer container, string connectionString) {
        this.container = container;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<SqlServerTestDatabase> CreateAsync() {
        MsSqlContainer container = new MsSqlBuilder(ImageName).Build();

        try {
            await container.StartAsync();
            string createDatabaseScript =
                $"IF DB_ID(N'{DatabaseName}') IS NULL CREATE DATABASE [{DatabaseName}];";
            await ExecuteBatchAsync(container, createDatabaseScript);

            string connectionString = BuildDatabaseConnectionString(container.GetConnectionString());
            string script = await File.ReadAllTextAsync(ProviderFixtureFiles.GetSchemaScriptPath("sqlserver"));
            string fixtureScript =
                $"USE [{DatabaseName}];{Environment.NewLine}GO{Environment.NewLine}{script}";
            await ExecuteBatchAsync(container, fixtureScript);

            return new SqlServerTestDatabase(container, connectionString);
        } catch {
            await container.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync() {
        await container.DisposeAsync();
    }

    private static string BuildDatabaseConnectionString(string connectionString) {
        DbConnectionStringBuilder builder = new() {
            ConnectionString = connectionString
        };

        builder["Initial Catalog"] = DatabaseName;
        builder["Trust Server Certificate"] = true;
        return builder.ConnectionString;
    }

    private static async Task ExecuteBatchAsync(MsSqlContainer container, string script) {
        await container.ExecScriptAsync(script).ThrowOnFailure();
    }
}
