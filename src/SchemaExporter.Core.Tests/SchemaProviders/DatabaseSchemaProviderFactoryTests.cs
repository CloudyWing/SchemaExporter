using CloudyWing.SchemaExporter.SchemaProviders;
using NSubstitute;

namespace CloudyWing.SchemaExporter.Core.Tests.SchemaProviders;

[TestFixture]
public sealed class DatabaseSchemaProviderFactoryTests {
    [Test]
    public async Task LoadSchemaAsync_WhenProviderExists_UsesMatchingProvider() {
        // Arrange
        IDatabaseSchemaProvider sqlServerProvider = Substitute.For<IDatabaseSchemaProvider>();
        sqlServerProvider.DatabaseType.Returns(DatabaseType.SqlServer);

        IDatabaseSchemaProvider oracleProvider = Substitute.For<IDatabaseSchemaProvider>();
        oracleProvider.DatabaseType.Returns(DatabaseType.Oracle);

        DatabaseSchemaExport expected = new();
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        oracleProvider.LoadSchemaAsync("oracle-connection", cancellationToken).Returns(Task.FromResult(expected));

        DatabaseSchemaProviderFactory sut = new([sqlServerProvider, oracleProvider]);

        // Act
        DatabaseSchemaExport result = await sut.LoadSchemaAsync(
            DatabaseType.Oracle,
            "oracle-connection",
            cancellationToken
        );

        // Assert
        Assert.That(result, Is.SameAs(expected));
        await oracleProvider.Received(1).LoadSchemaAsync("oracle-connection", cancellationToken);
        await sqlServerProvider.DidNotReceive().LoadSchemaAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void LoadSchemaAsync_WhenProviderDoesNotExist_ThrowsNotSupportedException() {
        // Arrange
        IDatabaseSchemaProvider sqlServerProvider = Substitute.For<IDatabaseSchemaProvider>();
        sqlServerProvider.DatabaseType.Returns(DatabaseType.SqlServer);

        DatabaseSchemaProviderFactory sut = new([sqlServerProvider]);

        // Act
        NotSupportedException? exception = Assert.ThrowsAsync<NotSupportedException>(
            async () => await sut.LoadSchemaAsync(DatabaseType.Oracle, "missing-provider")
        );

        // Assert
        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Does.Contain("Oracle"));
    }
}
