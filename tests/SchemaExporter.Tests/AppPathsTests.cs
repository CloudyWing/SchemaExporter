using System.IO;

namespace CloudyWing.SchemaExporter.Tests;

[TestFixture]
[NonParallelizable]
public sealed class AppPathsTests {
    [Test]
    public async Task EnsureUserConfigExists_WhenUserConfigIsMissing_CopiesInstallConfig() {
        using AppSettingsTestScope scope = new();
        string installConfig = """
            {
              "Schema": {
                "ExportPath": "C:\\Install\\Exports"
              }
            }
            """;
        await File.WriteAllTextAsync(scope.InstallConfigPath, installConfig);

        AppPaths.EnsureUserConfigExists();

        string userConfig = await File.ReadAllTextAsync(scope.UserConfigPath);
        using (Assert.EnterMultipleScope()) {
            Assert.That(File.Exists(scope.UserConfigPath), Is.True);
            Assert.That(userConfig, Is.EqualTo(installConfig));
        }
    }

    [Test]
    public async Task EnsureUserConfigExists_WhenUserConfigAlreadyExists_DoesNotOverwriteIt() {
        using AppSettingsTestScope scope = new();
        string installConfig = """
            {
              "Schema": {
                "ExportPath": "C:\\Install\\Exports"
              }
            }
            """;
        string existingUserConfig = """
            {
              "Schema": {
                "ExportPath": "C:\\User\\Exports"
              }
            }
            """;
        await File.WriteAllTextAsync(scope.InstallConfigPath, installConfig);
        Directory.CreateDirectory(scope.UserConfigDirectory);
        await File.WriteAllTextAsync(scope.UserConfigPath, existingUserConfig);

        AppPaths.EnsureUserConfigExists();

        string userConfig = await File.ReadAllTextAsync(scope.UserConfigPath);
        Assert.That(userConfig, Is.EqualTo(existingUserConfig));
    }

    [Test]
    public void EnsureUserConfigExistsIfInstallConfigExists_WhenInstallConfigIsMissing_DoesNothing() {
        using AppSettingsTestScope scope = new();
        if (File.Exists(scope.InstallConfigPath)) {
            File.Delete(scope.InstallConfigPath);
        }

        AppPaths.EnsureUserConfigExistsIfInstallConfigExists();

        Assert.That(File.Exists(scope.UserConfigPath), Is.False);
    }
}
