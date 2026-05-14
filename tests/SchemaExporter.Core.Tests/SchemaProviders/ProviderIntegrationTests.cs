using CloudyWing.SchemaExporter.Core.SchemaProviders;

namespace CloudyWing.SchemaExporter.Core.Tests.SchemaProviders;

[TestFixture]
public sealed class ProviderIntegrationTests {
    private const string SqlServerConnectionEnvironmentVariable = "SCHEMAEXPORTER_SQLSERVER_TEST_CONNECTION";
    private const string OracleConnectionEnvironmentVariable = "SCHEMAEXPORTER_ORACLE_TEST_CONNECTION";

    [Test]
    public async Task SqlServerProvider_WhenFixtureDatabaseIsConfigured_LoadsExpectedSchemaMetadataAsync() {
        string connectionString = GetConnectionStringOrIgnore(SqlServerConnectionEnvironmentVariable);
        SqlServerDatabaseSchemaProvider sut = new();

        IReadOnlyList<DatabaseObjectSchema> objects = await sut.LoadObjectsAsync(connectionString);
        IReadOnlyList<DatabaseObjectSchema> fixtureObjects = objects
            .Where(x => x.ObjectName.StartsWith("SE_", StringComparison.OrdinalIgnoreCase))
            .ToList();
        DatabaseSchemaDetails details = await sut.LoadDetailsAsync(connectionString, fixtureObjects);
        DatabaseObjectSchema customersTable = fixtureObjects.Single(x => x.ObjectName == "SE_Customers");
        DatabaseObjectSchema activeCustomersView = fixtureObjects.Single(x => x.ObjectName == "SE_ActiveCustomers");
        DatabaseColumnSchema idColumn = details.Columns.Single(x =>
            x.ObjectName == "SE_Customers" && x.ColumnName == "Id"
        );
        DatabaseColumnSchema nameColumn = details.Columns.Single(x =>
            x.ObjectName == "SE_Customers" && x.ColumnName == "Name"
        );
        DatabaseColumnSchema isActiveColumn = details.Columns.Single(x =>
            x.ObjectName == "SE_Customers" && x.ColumnName == "IsActive"
        );
        DatabaseIndexSchema primaryKeyIndex = details.Indexes.Single(x => x.IndexName == "PK_SE_Customers");
        DatabaseIndexSchema emailIndex = details.Indexes.Single(x => x.IndexName == "IX_SE_Customers_Email");
        DatabaseIndexSchema foreignKeyIndex = details.Indexes.Single(x => x.IndexName == "FK_SE_Orders_SE_Customers");
        DatabaseRoutineSchema procedure = details.Routines.Single(x => x.RoutineName == "usp_SE_GetCustomers");
        DatabaseRoutineSchema function = details.Routines.Single(x => x.RoutineName == "ufn_SE_CustomerDisplayName");

        using (Assert.EnterMultipleScope()) {
            Assert.That(customersTable.ObjectType, Is.EqualTo("BASE TABLE"));
            Assert.That(customersTable.ObjectDescription, Is.EqualTo("SchemaExporter fixture customer table"));
            Assert.That(activeCustomersView.ObjectType, Is.EqualTo("VIEW"));
            Assert.That(idColumn.ColumnType, Is.EqualTo("int"));
            Assert.That(idColumn.IsPrimaryKey, Is.EqualTo("Yes"));
            Assert.That(idColumn.IsIdentity, Is.EqualTo("Yes"));
            Assert.That(nameColumn.ColumnType, Is.EqualTo("nvarchar(100)"));
            Assert.That(nameColumn.IsNullable, Is.EqualTo("No"));
            Assert.That(nameColumn.ColumnDescription, Is.EqualTo("Customer display name"));
            Assert.That(isActiveColumn.ColumnDefault, Does.Contain("1"));
            Assert.That(primaryKeyIndex.IsPrimaryKey, Is.EqualTo("Yes"));
            Assert.That(primaryKeyIndex.IsClustered, Is.EqualTo("Yes"));
            Assert.That(emailIndex.IsUnique, Is.EqualTo("Yes"));
            Assert.That(emailIndex.Columns, Is.EqualTo("Email"));
            Assert.That(emailIndex.OtherColumns, Is.EqualTo("Name"));
            Assert.That(foreignKeyIndex.IsForeignKey, Is.EqualTo("Yes"));
            Assert.That(foreignKeyIndex.Columns, Is.EqualTo("CustomerId"));
            Assert.That(foreignKeyIndex.OtherColumns, Does.Contain("dbo.SE_Customers"));
            Assert.That(foreignKeyIndex.OtherColumns, Does.Contain("Id"));
            Assert.That(procedure.RoutineType, Is.EqualTo("PROCEDURE"));
            Assert.That(procedure.ParameterSignature, Is.EqualTo("@OnlyActive bit"));
            Assert.That(procedure.RoutineDefinition, Does.Contain("CREATE PROCEDURE"));
            Assert.That(function.RoutineType, Is.EqualTo("FUNCTION"));
            Assert.That(function.ParameterSignature, Is.EqualTo("@Name nvarchar(100), @Email nvarchar(256)"));
            Assert.That(function.ReturnType, Is.EqualTo("nvarchar(400)"));
            Assert.That(function.RoutineDefinition, Does.Contain("RETURN CONCAT"));
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
