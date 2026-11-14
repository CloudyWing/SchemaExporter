using System.Security.Cryptography;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Infrastructure.Security;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CloudyWing.SchemaExporter.Infrastructure.Tests.Security;

[TestFixture]
[NonParallelizable]
public sealed class ConnectionStringResolverTests {
    private const string VariableName = "SCHEMAEXPORTER_TEST_CONNECTION";

    [TearDown]
    public void TearDown() {
        Environment.SetEnvironmentVariable(VariableName, null);
    }

    [Test]
    public void Resolve_WhenValueIsPlainText_ReturnsValueWithoutCallingProtector() {
        IConnectionStringProtector protector = Substitute.For<IConnectionStringProtector>();
        ConnectionStringResolver sut = new(protector);

        string result = sut.Resolve(CreateConnection("Server=.;Database=App;"));

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.EqualTo("Server=.;Database=App;"));
            protector.DidNotReceiveWithAnyArgs().Unprotect(default!);
        }
    }

    [Test]
    public void Resolve_WhenValueIsEnvironmentReference_ReturnsEnvironmentValue() {
        Environment.SetEnvironmentVariable(VariableName, "Server=ci;Password=FromEnv;");
        ConnectionStringResolver sut = new(Substitute.For<IConnectionStringProtector>());

        string result = sut.Resolve(CreateConnection($"  ${{env:{VariableName}}}  "));

        Assert.That(result, Is.EqualTo("Server=ci;Password=FromEnv;"));
    }

    [Test]
    public void Resolve_WhenEnvironmentVariableIsMissing_ThrowsValidationExceptionNamingVariable() {
        ConnectionStringResolver sut = new(Substitute.For<IConnectionStringProtector>());

        ExportValidationException? exception = Assert.Throws<ExportValidationException>(
            () => sut.Resolve(CreateConnection($"${{env:{VariableName}}}"))
        );

        Assert.That(exception?.Message, Does.Contain(VariableName).And.Contain("Primary"));
    }

    [Test]
    public void Resolve_WhenValueIsProtected_ReturnsDecryptedValue() {
        IConnectionStringProtector protector = Substitute.For<IConnectionStringProtector>();
        protector.Unprotect("dpapi:AAAA").Returns("Server=prod;Password=Secret;");
        ConnectionStringResolver sut = new(protector);

        string result = sut.Resolve(CreateConnection("dpapi:AAAA"));

        Assert.That(result, Is.EqualTo("Server=prod;Password=Secret;"));
    }

    [Test]
    public void Resolve_WhenDecryptionFails_ThrowsValidationExceptionWithoutProtectedValue() {
        IConnectionStringProtector protector = Substitute.For<IConnectionStringProtector>();
        protector.Unprotect(Arg.Any<string>()).Throws(new CryptographicException("key not valid"));
        ConnectionStringResolver sut = new(protector);

        ExportValidationException? exception = Assert.Throws<ExportValidationException>(
            () => sut.Resolve(CreateConnection("dpapi:SECRETPAYLOAD"))
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(exception?.Message, Does.Contain("無法解密"));
            Assert.That(exception?.Message, Does.Not.Contain("SECRETPAYLOAD"));
        }
    }

    [Test]
    public void Resolve_WhenPlatformDoesNotSupportDecryption_ThrowsValidationException() {
        IConnectionStringProtector protector = Substitute.For<IConnectionStringProtector>();
        protector.Unprotect(Arg.Any<string>()).Throws(new PlatformNotSupportedException());
        ConnectionStringResolver sut = new(protector);

        Assert.Throws<ExportValidationException>(() => sut.Resolve(CreateConnection("dpapi:AAAA")));
    }

    [TestCase("${env:NAME}", true, "NAME")]
    [TestCase(" ${env:_A1} ", true, "_A1")]
    [TestCase("${env:1BAD}", false, "")]
    [TestCase("Server=${env:NAME};", false, "")]
    [TestCase("${ENV:NAME}", false, "")]
    public void TryGetEnvironmentVariableName_WhenCalled_AcceptsOnlyWholeValueReference(string value, bool expected, string expectedName) {
        bool result = ConnectionStringFormat.TryGetEnvironmentVariableName(value, out string name);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(name, Is.EqualTo(expectedName));
        }
    }

    [Test]
    public void DpapiProtector_WhenNotOnWindows_ThrowsPlatformNotSupported() {
        if (OperatingSystem.IsWindows()) {
            Assert.Ignore("只在非 Windows 平台驗證。");
        }

        DpapiConnectionStringProtector sut = new();

        Assert.Throws<PlatformNotSupportedException>(() => sut.Protect("Server=.;"));
    }

    [Test]
    [Platform(Include = "Win")]
    public void DpapiProtector_OnWindows_RoundTripsAndUsesPrefix() {
        DpapiConnectionStringProtector sut = new();

        string protectedValue = sut.Protect("Server=.;Password=Secret;");

        using (Assert.EnterMultipleScope()) {
            Assert.That(protectedValue, Does.StartWith(ConnectionStringFormat.ProtectedPrefix));
            Assert.That(protectedValue, Does.Not.Contain("Secret"));
            Assert.That(sut.Unprotect(protectedValue), Is.EqualTo("Server=.;Password=Secret;"));
        }
    }

    private static SchemaConnection CreateConnection(string connectionString) {
        return new SchemaConnection {
            Name = "Primary",
            ConnectionString = connectionString
        };
    }
}
