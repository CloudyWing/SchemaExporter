using CloudyWing.SchemaExporter.Core.IntegrationTests.Infrastructure;
using CloudyWing.SchemaExporter.Core.SchemaProviders;
using Microsoft.Data.SqlClient;
using Oracle.ManagedDataAccess.Client;

namespace CloudyWing.SchemaExporter.Core.IntegrationTests.SchemaProviders;

[TestFixture]
[Category(IntegrationTestCategories.Integration)]
[Explicit("Requires Docker to start provider fixture containers.")]
[NonParallelizable]
public sealed class ProviderIntegrationTests {
    private const string SqlServerDatabaseName = "SchemaExporterFixture";

    [Test]
    [Category(IntegrationTestCategories.SqlServer)]
    public async Task SqlServerProvider_WhenFixtureDatabaseIsStarted_LoadsExpectedSchemaMetadataAsync() {
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        SqlServerDatabaseSchemaProvider sut = new();

        IReadOnlyList<DatabaseObjectSchema> objects = await sut.LoadObjectsAsync(database.ConnectionString);
        IReadOnlyList<DatabaseObjectSchema> fixtureObjects = objects
            .Where(x => x.ObjectName.StartsWith("SE_", StringComparison.OrdinalIgnoreCase))
            .ToList();
        DatabaseSchemaDetails details = await sut.LoadDetailsAsync(database.ConnectionString, fixtureObjects);
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
        DatabaseForeignKeySchema relationship = details.Relationships.Single(
            x => x.ForeignKeyName == "FK_SE_Orders_SE_Customers"
        );
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
            Assert.That(emailIndex.IncludedColumns, Does.Contain("Name"));
            Assert.That(relationship.SourceSchemaName, Is.EqualTo("dbo"));
            Assert.That(relationship.SourceTableName, Is.EqualTo("SE_Orders"));
            Assert.That(relationship.SourceColumnName, Is.EqualTo("CustomerId"));
            Assert.That(relationship.ReferencedSchemaName, Is.EqualTo("dbo"));
            Assert.That(relationship.ReferencedTableName, Is.EqualTo("SE_Customers"));
            Assert.That(relationship.ReferencedColumnName, Is.EqualTo("Id"));
            Assert.That(relationship.ColumnOrder, Is.EqualTo(1));
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
    [Category(IntegrationTestCategories.Oracle)]
    public async Task OracleProvider_WhenFixtureDatabaseIsStarted_LoadsExpectedSchemaObjectsAsync() {
        await using OracleTestDatabase database = await OracleTestDatabase.CreateAsync();
        OracleDatabaseSchemaProvider sut = new();

        IReadOnlyList<DatabaseObjectSchema> objects = await sut.LoadObjectsAsync(database.ConnectionString);
        IReadOnlyList<DatabaseObjectSchema> fixtureObjects = objects
            .Where(x => x.ObjectName.StartsWith("SE_", StringComparison.OrdinalIgnoreCase))
            .ToList();
        DatabaseSchemaDetails details = await sut.LoadDetailsAsync(database.ConnectionString, fixtureObjects);

        using (Assert.EnterMultipleScope()) {
            Assert.That(fixtureObjects.Any(x => x.ObjectName == "SE_CUSTOMERS"), Is.True);
            Assert.That(fixtureObjects.Any(x => x.ObjectName == "SE_ACTIVE_CUSTOMERS"), Is.True);
            Assert.That(details.Columns.Any(x => x.ObjectName == "SE_CUSTOMERS" && x.ColumnName == "NAME"), Is.True);
            Assert.That(details.Indexes.Any(x => x.IndexName == "IX_SE_CUSTOMERS_EMAIL"), Is.True);
            Assert.That(details.Relationships.Any(x =>
                x.ForeignKeyName.Length > 0
                && x.SourceSchemaName.Length > 0
                && x.SourceTableName.Length > 0
                && x.SourceColumnName.Length > 0
                && x.ReferencedSchemaName.Length > 0
                && x.ReferencedTableName.Length > 0
                && x.ReferencedColumnName.Length > 0
                && x.ForeignKeyName == "FK_SE_ORDERS_SE_CUSTOMERS"
                && x.SourceTableName == "SE_ORDERS"
                && x.SourceColumnName == "CUSTOMER_ID"
                && x.ReferencedTableName == "SE_CUSTOMERS"
                && x.ReferencedColumnName == "ID"
                && x.ColumnOrder == 1
            ), Is.True);
            Assert.That(details.Routines.Any(x => x.RoutineName == "SE_GET_CUSTOMERS"), Is.True);
        }
    }

    [Test]
    [Category(IntegrationTestCategories.SqlServer)]
    public async Task SqlServerProvider_WhenForeignKeyCatalogPermissionIsDenied_ReturnsDiagnosticAndOtherMetadata() {
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        string restrictedConnectionString = await CreateSqlServerRestrictedConnectionAsync(database.ConnectionString);
        SqlServerDatabaseSchemaProvider sut = new();
        IReadOnlyList<DatabaseObjectSchema> tables = [
            new DatabaseObjectSchema {
                SchemaName = "dbo",
                ObjectName = "SE_Customers",
                ObjectType = "BASE TABLE"
            },
            new DatabaseObjectSchema {
                SchemaName = "dbo",
                ObjectName = "SE_Orders",
                ObjectType = "BASE TABLE"
            }
        ];

        DatabaseSchemaDetails details = await sut.LoadDetailsAsync(restrictedConnectionString, tables);

        using (Assert.EnterMultipleScope()) {
            Assert.That(details.Columns, Is.Not.Empty);
            Assert.That(details.Indexes, Is.Not.Empty);
            Assert.That(details.Relationships, Is.Empty);
            Assert.That(details.Diagnostics.Select(x => x.Message), Does.Contain(
                "FK catalog permission denied; relationships were not retrieved."
            ));
        }
    }

    [Test]
    [Category(IntegrationTestCategories.Oracle)]
    public async Task OracleProvider_WhenForeignKeyCatalogPermissionIsDenied_ReturnsDiagnosticAndOtherMetadata() {
        await using OracleTestDatabase database = await OracleTestDatabase.CreateAsync();
        (string restrictedConnectionString, string userName) = await CreateOracleRestrictedSchemaAsync(
            database.ConnectionString
        );
        await CreateOracleRestrictedSchemaObjectsAsync(restrictedConnectionString);
        OracleDatabaseSchemaProvider sut = new();

        IReadOnlyList<DatabaseObjectSchema> objects = await sut.LoadObjectsAsync(restrictedConnectionString);
        IReadOnlyList<DatabaseObjectSchema> tables = objects
            .Where(x => x.ObjectName is "SE_LIMITED_CUSTOMERS" or "SE_LIMITED_ORDERS")
            .ToList();
        DatabaseSchemaDetails details = await sut.LoadDetailsAsync(restrictedConnectionString, tables);

        using (Assert.EnterMultipleScope()) {
            Assert.That(userName, Does.StartWith("SE_LIMITED_"));
            Assert.That(details.Columns, Is.Not.Empty);
            Assert.That(details.Indexes, Is.Not.Empty);
            Assert.That(details.Relationships, Is.Empty);
            Assert.That(details.Diagnostics.Select(x => x.Message), Does.Contain(
                "FK catalog permission denied; relationships were not retrieved."
            ));
        }
    }

    private static async Task<string> CreateSqlServerRestrictedConnectionAsync(string adminConnectionString) {
        string suffix = Guid.NewGuid().ToString("N");
        string loginName = $"SchemaExporterFkLimited_{suffix}";
        string password = $"P@ss_{suffix}Aa1!";
        SqlConnectionStringBuilder adminBuilder = new(adminConnectionString) {
            InitialCatalog = "master"
        };
        string script = $"""
            CREATE LOGIN [{loginName}] WITH PASSWORD = '{password}';
            USE [{SqlServerDatabaseName}];
            CREATE USER [{loginName}] FOR LOGIN [{loginName}];
            GRANT CONNECT TO [{loginName}];
            GRANT VIEW DEFINITION ON DATABASE::[{SqlServerDatabaseName}] TO [{loginName}];
            GRANT SELECT ON OBJECT::[dbo].[SE_Customers] TO [{loginName}];
            GRANT SELECT ON OBJECT::[dbo].[SE_Orders] TO [{loginName}];
            DENY SELECT ON OBJECT::[sys].[foreign_keys] TO [{loginName}];
            DENY SELECT ON OBJECT::[sys].[foreign_key_columns] TO [{loginName}];
            """;

        await using (SqlConnection connection = new(adminBuilder.ConnectionString)) {
            await connection.OpenAsync().ConfigureAwait(false);
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = script;
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        SqlConnectionStringBuilder restrictedBuilder = new(adminConnectionString) {
            InitialCatalog = SqlServerDatabaseName,
            UserID = loginName,
            Password = password,
            IntegratedSecurity = false
        };
        return restrictedBuilder.ConnectionString;
    }

    private static async Task<(string ConnectionString, string UserName)> CreateOracleRestrictedSchemaAsync(
        string adminConnectionString
    ) {
        string suffix = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        string userName = $"SE_LIMITED_{suffix}";
        string password = $"P@ss_{suffix}Aa1!";
        await ExecuteOracleNonQueryAsync(adminConnectionString, $"CREATE USER {userName} IDENTIFIED BY \"{password}\"");
        await ExecuteOracleNonQueryAsync(adminConnectionString, $"GRANT CREATE SESSION, CREATE TABLE, CREATE VIEW TO {userName}");
        await ExecuteOracleNonQueryAsync(adminConnectionString, $"ALTER USER {userName} QUOTA 100M ON USERS");
        OracleConnectionStringBuilder restrictedBuilder = new(adminConnectionString) {
            UserID = userName,
            Password = password
        };
        return (restrictedBuilder.ConnectionString, userName);
    }

    private static async Task CreateOracleRestrictedSchemaObjectsAsync(string connectionString) {
        await ExecuteOracleNonQueryAsync(
            connectionString,
            """
            CREATE TABLE SE_LIMITED_CUSTOMERS (
              ID NUMBER NOT NULL,
              CONSTRAINT PK_SE_LIMITED_CUSTOMERS PRIMARY KEY (ID)
            )
            """
        );
        await ExecuteOracleNonQueryAsync(
            connectionString,
            """
            CREATE TABLE SE_LIMITED_ORDERS (
              ID NUMBER NOT NULL,
              CUSTOMER_ID NUMBER NOT NULL,
              CONSTRAINT PK_SE_LIMITED_ORDERS PRIMARY KEY (ID),
              CONSTRAINT FK_SE_LIMITED_ORDERS_CUSTOMERS FOREIGN KEY (CUSTOMER_ID)
                REFERENCES SE_LIMITED_CUSTOMERS (ID)
            )
            """
        );
        await ExecuteOracleNonQueryAsync(
            connectionString,
            "CREATE UNIQUE INDEX IX_SE_LIMITED_CUSTOMERS_ID ON SE_LIMITED_CUSTOMERS (ID)"
        );
        await ExecuteOracleNonQueryAsync(
            connectionString,
            "CREATE FORCE VIEW ALL_CONSTRAINTS AS SELECT * FROM SYS.DBA_CONSTRAINTS"
        );
        await ExecuteOracleNonQueryAsync(
            connectionString,
            "CREATE FORCE VIEW ALL_CONS_COLUMNS AS SELECT * FROM SYS.DBA_CONS_COLUMNS"
        );
    }

    private static async Task ExecuteOracleNonQueryAsync(string connectionString, string commandText) {
        await using OracleConnection connection = new(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using OracleCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}

