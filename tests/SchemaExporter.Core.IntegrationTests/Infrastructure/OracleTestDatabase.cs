using System.Text.RegularExpressions;
using DotNet.Testcontainers.Containers;
using Oracle.ManagedDataAccess.Client;
using Testcontainers.Oracle;

namespace CloudyWing.SchemaExporter.Core.IntegrationTests.Infrastructure;

internal sealed class OracleTestDatabase : IAsyncDisposable {
    private const string ImageName = "gvenzl/oracle-xe:21.3.0-slim-faststart";
    private readonly OracleContainer container;

    private OracleTestDatabase(OracleContainer container) {
        this.container = container;
        ConnectionString = container.GetConnectionString();
        AdminConnectionString = new OracleConnectionStringBuilder(ConnectionString) { UserID = "system" }.ConnectionString;
    }

    public string ConnectionString { get; }

    public string AdminConnectionString { get; }

    public static async Task<OracleTestDatabase> CreateAsync() {
        OracleContainer container = new OracleBuilder(ImageName).Build();

        try {
            await container.StartAsync();

            string script = await File.ReadAllTextAsync(ProviderFixtureFiles.GetSchemaScriptPath("oracle"));
            await container.ExecScriptAsync(script).ThrowOnFailure();

            return new OracleTestDatabase(container);
        } catch {
            await container.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// 把 SQL 檔案原樣複製進容器，並以容器內的 <c>sqlplus</c> 執行，讓 <c>SET DEFINE OFF</c> 等 SQL*Plus 指令實際生效。
    /// 以 <c>WHENEVER SQLERROR EXIT</c> 包裝，遇到第一個資料庫錯誤即以非零結束代碼停止。
    /// </summary>
    public async Task<ScriptExecutionResult> ExecuteSqlPlusFileAsync(byte[] fileContent) {
        const string ScriptPath = "/tmp/comment-sql.sql";
        const string WrapperPath = "/tmp/comment-sql-run.sql";
        await container.CopyAsync(fileContent, ScriptPath);
        await container.CopyAsync(
            System.Text.Encoding.ASCII.GetBytes($"WHENEVER SQLERROR EXIT SQL.SQLCODE\nWHENEVER OSERROR EXIT FAILURE\n@{ScriptPath}\nEXIT\n"),
            WrapperPath
        );

        string dataSource = new OracleConnectionStringBuilder(ConnectionString).DataSource;
        string serviceName = Regex.Match(dataSource, @"SERVICE_NAME=(?<name>[^)]+)").Groups["name"].Value;
        ExecResult result = await container.ExecAsync([
            "sh",
            "-c",
            $"NLS_LANG=AMERICAN_AMERICA.AL32UTF8 sqlplus -s -L {OracleBuilder.DefaultUsername}/{OracleBuilder.DefaultPassword}@//localhost/{serviceName} @{WrapperPath}"
        ]);
        return new ScriptExecutionResult(result.ExitCode ?? -1, result.Stdout, result.Stderr);
    }

    public async ValueTask DisposeAsync() {
        await container.DisposeAsync();
    }
}

