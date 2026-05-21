namespace CloudyWing.SchemaExporter.Core.IntegrationTests.Infrastructure;

internal static class ProviderFixtureFiles {
    public static string GetSchemaScriptPath(string providerName) {
        return Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "ProviderFixtures",
            providerName,
            "schema.sql"
        );
    }
}

