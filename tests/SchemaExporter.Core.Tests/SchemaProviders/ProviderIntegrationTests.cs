using CloudyWing.SchemaExporter.Core.SchemaProviders;

namespace CloudyWing.SchemaExporter.Core.Tests.SchemaProviders;

[TestFixture]
public sealed class ProviderIntegrationTests {
    private const string SqlServerConnectionEnvironmentVariable = "SCHEMAEXPORTER_SQLSERVER_TEST_CONNECTION";
    private const string OracleConnectionEnvironmentVariable = "SCHEMAEXPORTER_ORACLE_TEST_CONNECTION";

    [Test]
    public async Task SqlServerProvider_WhenFixtureDatabaseIsConfigured_LoadsExpectedSchemaObjectsAsync() {
        string connectionString = GetConnectionStringOrIgnore(SqlServerConnectionEnvironmentVariable);
        SqlServerDatabaseSchemaProvider sut = new();

        IReadOnlyList<DatabaseObjectSchema> objects = await sut.LoadObjectsAsync(connectionString);
        IReadOnlyList<DatabaseObjectSchema> fixtureObjects = objects
            .Where(x => x.ObjectName.StartsWith("SE_", StringComparison.OrdinalIgnoreCase))
            .ToList();
        DatabaseSchemaDetails details = await sut.LoadDetailsAsync(connectionString, fixtureObjects);

        using (Assert.EnterMultipleScope()) {
            Assert.That(fixtureObjects.Any(x => x.ObjectName == "SE_Customers"), Is.True);
            Assert.That(fixtureObjects.Any(x => x.ObjectName == "SE_ActiveCustomers"), Is.True);
            Assert.That(details.Columns.Any(x => x.ObjectName == "SE_Customers" && x.ColumnName == "Name"), Is.True);
            Assert.That(details.Indexes.Any(x => x.IndexName == "IX_SE_Customers_Email"), Is.True);
            Assert.That(details.Routines.Any(x => x.RoutineName == "usp_SE_GetCustomers"), Is.True);
        }
    }

    [Test]
    public async Task OracleProvider_WhenFixtureDatabaseIsConfigured_LoadsExpectedSchemaObjectsAsync() {
        string connectionString = GetConnectionStringOrIgnore(OracleConnectionEnvironmentVariable);
        OracleDatabaseSchemaProvider sut = new();

        IReadOnlyList<DatabaseObjectSchema> objects = await sut.LoadObjectsAsync(connectionString);
        IReadOnlyList<DatabaseObjectSchema> fixtureObjects = objects
            .Where(x => x.ObjectName.StartsWith("SE_", StringComparison.OrdinalIgnoreCase))
            .ToList();
        DatabaseSchemaDetails details = await sut.LoadDetailsAsync(connectionString, fixtureObjects);

        using (Assert.EnterMultipleScope()) {
            Assert.That(fixtureObjects.Any(x => x.ObjectName == "SE_CUSTOMERS"), Is.True);
            Assert.That(fixtureObjects.Any(x => x.ObjectName == "SE_ACTIVE_CUSTOMERS"), Is.True);
            Assert.That(details.Columns.Any(x => x.ObjectName == "SE_CUSTOMERS" && x.ColumnName == "NAME"), Is.True);
            Assert.That(details.Indexes.Any(x => x.IndexName == "IX_SE_CUSTOMERS_EMAIL"), Is.True);
            Assert.That(details.Routines.Any(x => x.RoutineName == "SE_GET_CUSTOMERS"), Is.True);
        }
    }

    private static string GetConnectionStringOrIgnore(string environmentVariableName) {
        string? connectionString = Environment.GetEnvironmentVariable(environmentVariableName);
        if (string.IsNullOrWhiteSpace(connectionString)) {
            Assert.Ignore($"Integration test skipped because {environmentVariableName} is not set.");
        }

        return connectionString;
    }
}
