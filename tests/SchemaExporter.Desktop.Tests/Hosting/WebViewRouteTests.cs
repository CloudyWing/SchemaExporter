using CloudyWing.SchemaExporter.Desktop.Hosting;

namespace CloudyWing.SchemaExporter.Desktop.Tests.Hosting;

[TestFixture]
public sealed class WebViewRouteTests {
    [TestCase(null, null)]
    [TestCase("", null)]
    [TestCase("settings", "settings")]
    [TestCase("settings?tab=2#x", "settings?tab=2#x")]
    [TestCase("https://evil/x", null)]
    public void Normalize_Route_ReturnsRestorableRouteOrNull(string? route, string? expected) {
        string? result = WebViewRoute.Normalize(route);

        Assert.That(result, Is.EqualTo(expected));
    }
}
