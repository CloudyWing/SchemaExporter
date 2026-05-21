namespace CloudyWing.SchemaExporter.Core.IntegrationTests;

[TestFixture]
public sealed class IntegrationTestProjectSmokeTests {
    [Test]
    public void ProviderIntegrationTests_WhenDiscovered_RequireExplicitSelection() {
        ExplicitAttribute? explicitAttribute = Attribute.GetCustomAttribute(
            typeof(SchemaProviders.ProviderIntegrationTests),
            typeof(ExplicitAttribute)
        ) as ExplicitAttribute;
        CategoryAttribute? categoryAttribute = Attribute.GetCustomAttribute(
            typeof(SchemaProviders.ProviderIntegrationTests),
            typeof(CategoryAttribute)
        ) as CategoryAttribute;

        using (Assert.EnterMultipleScope()) {
            Assert.That(explicitAttribute, Is.Not.Null);
            Assert.That(categoryAttribute?.Name, Is.EqualTo(IntegrationTestCategories.Integration));
        }
    }
}

