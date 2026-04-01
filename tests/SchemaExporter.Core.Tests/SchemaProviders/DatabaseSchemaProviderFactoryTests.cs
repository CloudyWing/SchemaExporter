using CloudyWing.SchemaExporter.Core.SchemaProviders;
using NSubstitute;

namespace CloudyWing.SchemaExporter.Core.Tests.SchemaProviders;

[TestFixture]
public sealed class DatabaseSchemaProviderFactoryTests {
    [Test]
    public async Task LoadObjectsAsync_WhenProviderExists_UsesMatchingProvider() {
        IDatabaseSchemaProvider sqlServerProvider = Substitute.For<IDatabaseSchemaProvider>();
        sqlServerProvider.DatabaseType.Returns(DatabaseType.SqlServer);

        IDatabaseSchemaProvider oracleProvider = Substitute.For<IDatabaseSchemaProvider>();
        oracleProvider.DatabaseType.Returns(DatabaseType.Oracle);

        IReadOnlyList<DatabaseObjectSchema> expected = [new DatabaseObjectSchema { SchemaName = "TEST", ObjectName = "T1", ObjectType = "BASE TABLE" }];
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        oracleProvider.LoadObjectsAsync("oracle-connection", cancellationToken).Returns(Task.FromResult(expected));

        DatabaseSchemaProviderFactory sut = new([sqlServerProvider, oracleProvider]);

        IReadOnlyList<DatabaseObjectSchema> result = await sut.LoadObjectsAsync(
            DatabaseType.Oracle,
            "oracle-connection",
            cancellationToken
        );

        Assert.That(result, Is.SameAs(expected));
        await oracleProvider.Received(1).LoadObjectsAsync("oracle-connection", cancellationToken);
        await sqlServerProvider.DidNotReceive().LoadObjectsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void LoadObjectsAsync_WhenProviderDoesNotExist_ThrowsNotSupportedException() {
        IDatabaseSchemaProvider sqlServerProvider = Substitute.For<IDatabaseSchemaProvider>();
        sqlServerProvider.DatabaseType.Returns(DatabaseType.SqlServer);

        DatabaseSchemaProviderFactory sut = new([sqlServerProvider]);

        NotSupportedException? exception = Assert.ThrowsAsync<NotSupportedException>(
            async () => await sut.LoadObjectsAsync(DatabaseType.Oracle, "missing-provider")
        );

        Assert.That(exception, Is.Not.Null);
        NotSupportedException assertedException = exception ?? throw new AssertionException("Expected a NotSupportedException.");
        Assert.That(assertedException.Message, Does.Contain("Oracle"));
    }

    [Test]
    public async Task LoadDetailsAsync_WhenProviderExists_UsesMatchingProvider() {
        IDatabaseSchemaProvider sqlServerProvider = Substitute.For<IDatabaseSchemaProvider>();
        sqlServerProvider.DatabaseType.Returns(DatabaseType.SqlServer);

        IDatabaseSchemaProvider oracleProvider = Substitute.For<IDatabaseSchemaProvider>();
        oracleProvider.DatabaseType.Returns(DatabaseType.Oracle);

        IReadOnlyList<DatabaseObjectSchema> filteredObjects = [new DatabaseObjectSchema { SchemaName = "TEST", ObjectName = "T1", ObjectType = "BASE TABLE" }];
        DatabaseSchemaDetails expected = new();
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        oracleProvider.LoadDetailsAsync("oracle-connection", filteredObjects, cancellationToken).Returns(Task.FromResult(expected));

        DatabaseSchemaProviderFactory sut = new([sqlServerProvider, oracleProvider]);

        DatabaseSchemaDetails result = await sut.LoadDetailsAsync(
            DatabaseType.Oracle,
            "oracle-connection",
            filteredObjects,
            cancellationToken
        );

        Assert.That(result, Is.SameAs(expected));
        await oracleProvider.Received(1).LoadDetailsAsync("oracle-connection", filteredObjects, cancellationToken);
    }
}
