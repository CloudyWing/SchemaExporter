using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.Exporting.CommentSql;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;
using CloudyWing.SchemaExporter.Core.IntegrationTests.Infrastructure;
using CloudyWing.SchemaExporter.Core.SchemaProviders;
using Microsoft.Data.SqlClient;
using Oracle.ManagedDataAccess.Client;

namespace CloudyWing.SchemaExporter.Core.IntegrationTests.CommentSql;

/// <summary>
/// 在 Testcontainers 啟動的拋棄式容器內，實際執行註解 SQL 產生器的輸出，
/// 讀回註解與輸入逐字比對，並確認重新匯出 Snapshot 後再產生一次不再有任何差異。
/// </summary>
[TestFixture]
[Category(IntegrationTestCategories.Integration)]
[Explicit("Requires Docker to start provider fixture containers.")]
[NonParallelizable]
public sealed partial class CommentSqlIntegrationTests {
    private const string Emoji = "\U0001F600";
    private const string SqlServerSchemaWithSpace = "Cs Schema";
    private const string SqlServerNamesTable = "Order Item.v1";

    private static readonly string[] NameMatrixColumns = ["select", "O'Brien \"x\"", "a.b", "with space", "[x]"];

    private static readonly JsonSerializerOptions CommentListJsonOptions = new() {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };


    [Test]
    [Category(IntegrationTestCategories.SqlServer)]
    public async Task SqlServer_GeneratedScript_WhenExecutedWithSqlcmd_RoundTripsEveryCommentVerbatimAsync() {
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await PrepareSqlServerSchemaAsync(database.ConnectionString);
        List<CommentSqlEntry> entries = BuildSqlServerEntries();
        using TempDirectory directory = new();

        CommentSqlResult first = await GenerateAsync(directory, await ExportSnapshotAsync(DatabaseType.SqlServer, database.ConnectionString, SqlServerObjectNames()), entries);
        string firstScript = first.SqlText;

        using (Assert.EnterMultipleScope()) {
            Assert.That(first.DatabaseType, Is.EqualTo(DatabaseType.SqlServer));
            Assert.That(first.SkippedEmptyCount, Is.EqualTo(1), "the empty comment entry must be skipped");
            Assert.That(first.UnchangedCount, Is.EqualTo(1), "SamePre already equals the database value");
            Assert.That(firstScript, Does.Contain("-- existing (whitespace-only): \"   \""));
            Assert.That(firstScript, Does.Contain("-- existing: \"old text\""));
        }

        byte[] fileBytes = await File.ReadAllBytesAsync(directory.Combine("comments.sql"));
        ScriptExecutionResult execution = await database.ExecuteSqlFileAsync(fileBytes);
        AssertScriptSucceeded(execution);
        await AssertSqlServerCommentsAsync(database.ConnectionString, entries, SqlServerPreExistingExpectations());

        ScriptExecutionResult rerun = await database.ExecuteSqlFileAsync(fileBytes);
        AssertScriptSucceeded(rerun);
        await AssertSqlServerCommentsAsync(database.ConnectionString, entries, SqlServerPreExistingExpectations());

        CommentSqlResult second = await GenerateAsync(directory, await ExportSnapshotAsync(DatabaseType.SqlServer, database.ConnectionString, SqlServerObjectNames()), entries, "second.sql");
        using (Assert.EnterMultipleScope()) {
            Assert.That(second.AddCount, Is.Zero, second.SqlText);
            Assert.That(second.UpdateCount, Is.Zero, second.SqlText);
            Assert.That(second.UnchangedCount, Is.EqualTo(entries.Count - 1));
        }
    }

    [Test]
    [Category(IntegrationTestCategories.SqlServer)]
    public async Task SqlServer_GeneratedScript_WhenExecutedAsGoSeparatedBatches_ProducesSameCommentsAsSqlcmdAsync() {
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await PrepareSqlServerSchemaAsync(database.ConnectionString);
        List<CommentSqlEntry> entries = BuildSqlServerEntries();
        using TempDirectory directory = new();
        await GenerateAsync(directory, await ExportSnapshotAsync(DatabaseType.SqlServer, database.ConnectionString, SqlServerObjectNames()), entries);

        // Mirrors how SSMS and sqlcmd cut batches: only a line that is exactly GO ends a batch.
        string script = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(directory.Combine("comments.sql"))).TrimStart((char)0xFEFF);
        List<string> batches = GoLineRegex().Split(script).Where(x => x.Trim().Length > 0).ToList();
        await using SqlConnection connection = new(database.ConnectionString);
        await connection.OpenAsync();
        foreach (string batch in batches) {
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = batch;
            await command.ExecuteNonQueryAsync();
        }

        await AssertSqlServerCommentsAsync(database.ConnectionString, entries, SqlServerPreExistingExpectations());
        Assert.That(batches, Has.Count.EqualTo(entries.Count - 2), "one batch per statement; GO inside comment text must not split a batch");
    }

    [Test]
    [Category(IntegrationTestCategories.SqlServer)]
    public async Task SqlServer_ExtendedPropertyValueLimit_MatchesGeneratorLimitAsync() {
        await using SqlServerTestDatabase database = await SqlServerTestDatabase.CreateAsync();
        await PrepareSqlServerSchemaAsync(database.ConnectionString);

        SqlException? overLimit = Assert.ThrowsAsync<SqlException>(
            () => AddSqlServerExtendedPropertyAsync(database.ConnectionString, "dbo", "CS_Existing", "PreNone", new string('a', 3751))
        );
        await AddSqlServerExtendedPropertyAsync(database.ConnectionString, "dbo", "CS_Existing", "PreNone", new string('a', 3750));

        using (Assert.EnterMultipleScope()) {
            Assert.That(overLimit!.Number, Is.EqualTo(15097));
            Assert.That(
                (await ReadSqlServerCommentAsync(database.ConnectionString, "dbo", "CS_Existing", "PreNone"))!.Length,
                Is.EqualTo(3750)
            );
            Assert.That(SqlServerCommentSqlWriter.MaxCommentCodeUnits, Is.EqualTo(3750));
        }
    }


    [Test]
    [Category(IntegrationTestCategories.Oracle)]
    public async Task Oracle_GeneratedScript_WhenExecutedWithSqlPlus_RoundTripsEveryCommentVerbatimAsync() {
        await using OracleTestDatabase database = await OracleTestDatabase.CreateAsync();
        string schema = await PrepareOracleSchemaAsync(database.ConnectionString);
        List<CommentSqlEntry> entries = BuildOracleEntries(schema);
        using TempDirectory directory = new();

        CommentSqlResult first = await GenerateAsync(directory, await ExportSnapshotAsync(DatabaseType.Oracle, database.ConnectionString, OracleObjectNames()), entries);

        using (Assert.EnterMultipleScope()) {
            Assert.That(first.DatabaseType, Is.EqualTo(DatabaseType.Oracle));
            Assert.That(first.SkippedEmptyCount, Is.EqualTo(1), "the empty comment entry must be skipped");
            Assert.That(first.UnchangedCount, Is.EqualTo(1), "SamePre already equals the database value");
            Assert.That(first.SqlText, Does.Contain("-- existing: \"old text\""));
        }

        byte[] fileBytes = await File.ReadAllBytesAsync(directory.Combine("comments.sql"));
        ScriptExecutionResult execution = await database.ExecuteSqlPlusFileAsync(fileBytes);
        AssertScriptSucceeded(execution);
        await AssertOracleCommentsAsync(database.ConnectionString, schema, entries, OraclePreExistingExpectations());

        ScriptExecutionResult rerun = await database.ExecuteSqlPlusFileAsync(fileBytes);
        AssertScriptSucceeded(rerun);
        await AssertOracleCommentsAsync(database.ConnectionString, schema, entries, OraclePreExistingExpectations());

        CommentSqlResult second = await GenerateAsync(directory, await ExportSnapshotAsync(DatabaseType.Oracle, database.ConnectionString, OracleObjectNames()), entries, "second.sql");
        using (Assert.EnterMultipleScope()) {
            Assert.That(second.AddCount, Is.Zero, second.SqlText);
            Assert.That(second.UpdateCount, Is.Zero, second.SqlText);
            Assert.That(second.UnchangedCount, Is.EqualTo(entries.Count - 1));
        }
    }

    [Test]
    [Category(IntegrationTestCategories.Oracle)]
    public async Task Oracle_EmptyAndNullComments_BehaveAsTheGeneratorAssumesAsync() {
        await using OracleTestDatabase database = await OracleTestDatabase.CreateAsync();
        string schema = await PrepareOracleSchemaAsync(database.ConnectionString);

        await ExecuteOracleAsync(database.ConnectionString, $"COMMENT ON COLUMN \"{schema}\".\"CS_Existing\".\"PreDifferent\" IS ''");
        string? afterEmptyLiteral = await ReadOracleCommentAsync(database.ConnectionString, schema, "CS_Existing", "PreDifferent");
        SchemaSnapshotDocument snapshot = await ExportSnapshotAsync(DatabaseType.Oracle, database.ConnectionString, ["CS_Existing"]);
        SchemaSnapshotColumnDocument cleared = snapshot.Objects.Single().Columns.Single(x => x.ColumnName == "PreDifferent");
        SchemaSnapshotColumnDocument whitespaceOnly = snapshot.Objects.Single().Columns.Single(x => x.ColumnName == "PreWhitespace");

        using (Assert.EnterMultipleScope()) {
            Assert.That(afterEmptyLiteral, Is.Null, "COMMENT ON ... IS '' stores NULL, so an empty comment is the same as no comment");
            Assert.That(cleared.ColumnDescription, Is.Empty, "the snapshot reports a NULL Oracle comment as an empty string");
            Assert.That(whitespaceOnly.ColumnDescription, Is.EqualTo("   "), "a whitespace-only Oracle comment is kept as is");
        }
    }

    [Test]
    [Category(IntegrationTestCategories.Oracle)]
    public async Task Oracle_CommentByteLimit_MatchesGeneratorLimitAsync() {
        await using OracleTestDatabase database = await OracleTestDatabase.CreateAsync();
        string schema = await PrepareOracleSchemaAsync(database.ConnectionString);
        string asciiAtLimit = new('a', 4000);
        string cjkAtLimit = new string('中', 1333) + "a";

        await ExecuteOracleAsync(database.ConnectionString, $"COMMENT ON COLUMN \"{schema}\".\"CS_Existing\".\"PreNone\" IS '{asciiAtLimit}'");
        await ExecuteOracleAsync(database.ConnectionString, $"COMMENT ON COLUMN \"{schema}\".\"CS_Existing\".\"PreNone\" IS '{cjkAtLimit}'");
        OracleException? asciiOver = Assert.ThrowsAsync<OracleException>(
            () => ExecuteOracleAsync(database.ConnectionString, $"COMMENT ON COLUMN \"{schema}\".\"CS_Existing\".\"PreNone\" IS '{asciiAtLimit}b'")
        );
        OracleException? cjkOver = Assert.ThrowsAsync<OracleException>(
            () => ExecuteOracleAsync(database.ConnectionString, $"COMMENT ON COLUMN \"{schema}\".\"CS_Existing\".\"PreNone\" IS '{new string('中', 1334)}'")
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(asciiOver, Is.Not.Null);
            Assert.That(cjkOver, Is.Not.Null);
            Assert.That(await ReadOracleCommentAsync(database.ConnectionString, schema, "CS_Existing", "PreNone"), Is.EqualTo(cjkAtLimit));
            Assert.That(OracleCommentSqlWriter.MaxCommentBytes, Is.EqualTo(4000));
        }
    }


    private static List<(string Key, string Comment)> CommonCases() {
        return [
            ("C01_Simple", "Plain ASCII description"),
            ("C02_Quote", "It's a user's \"name\" with ''two'' quotes and '"),
            ("C03_CjkEmoji", "使用者名稱 " + Emoji + " 完整顯示 \U00020BB7"),
            ("C04_CrLf", "line1\r\nline2\r\n\r\nline4"),
            ("C05_Lf", "line1\nline2\n\nline4\n"),
            ("C06_Cr", "a\rb"),
            ("C07_BlankLines", "first\n\n\nlast"),
            ("C08_WhitespaceOnly", "   "),
            ("C09_Padded", "  lead and trail  "),
            ("C10_Tab", "col1\tcol2"),
            ("C11_GoLines", "before\nGO\nafter\ngo\n  GO  \nGO 5"),
            ("C12_SqlcmdVariable", "cost $(Price) $(x) $5 $ $("),
            ("C13_Ampersand", "R&D & Q&A &&var &x. &1"),
            ("C14_SqlPlusLines", "x\n/\n.\n#set x\n@file\n-- dash\nREM text\nEXIT\nlast;"),
            ("C15_Semicolon", "ends with semicolon;"),
            ("C16_SqlSyntax", "/* c */ -- d ' \" \\ % _ ; ]] [[ -- end"),
            ("C17_Backslash", "C:\\path\\n is not a newline"),
            ("C18_LongMultiline", string.Join("\n", Enumerable.Range(1, 100).Select(x => $"line {x} of the long description"))),
            ("C19_LongChineseMultiline", string.Join("\r\n", Enumerable.Range(1, 60).Select(x => $"第 {x} 行說明文字，包含中文與標點。")))
        ];
    }

    private static List<(string Key, string Comment)> SqlServerLimitCases() {
        return [
            ("L01_MaxAscii", new string('a', 3750)),
            ("L02_MaxCjk", new string('中', 3750)),
            ("L03_MaxEmoji", string.Concat(Enumerable.Repeat(Emoji, 1875)))
        ];
    }

    private static List<(string Key, string Comment)> OracleLimitCases() {
        return [
            ("L01_MaxAscii", new string('a', 4000)),
            ("L02_MaxCjk", new string('中', 1333) + "a"),
            ("L03_MaxEmoji", string.Concat(Enumerable.Repeat(Emoji, 1000)))
        ];
    }

    private static List<CommentSqlEntry> BuildSqlServerEntries() {
        List<CommentSqlEntry> entries = [];
        foreach ((string key, string comment) in CommonCases().Concat(SqlServerLimitCases())) {
            entries.Add(Entry("dbo", "CS_Cases", key, comment));
        }

        entries.Add(Entry("dbo", "CS_Cases", null, "Table level comment\nwith a second line, it's fine"));
        entries.Add(Entry("dbo", "CS_View", null, "View level comment 檢視表"));
        entries.Add(Entry("dbo", "CS_View", "Id", "View column comment"));
        AddSqlServerNameMatrixEntries(entries);

        entries.Add(Entry("dbo", "CS_Existing", "PreEmpty", "was an empty property"));
        entries.Add(Entry("dbo", "CS_Existing", "PreWhitespace", "was whitespace only"));
        entries.Add(Entry("dbo", "CS_Existing", "PreDifferent", "new text"));
        entries.Add(Entry("dbo", "CS_Existing", "PreSame", "same text"));
        entries.Add(Entry("dbo", "CS_Existing", "PreKeep", ""));
        entries.Add(Entry("dbo", "CS_Existing", "PreNone", "fresh"));
        return entries;
    }

    private static List<CommentSqlEntry> BuildOracleEntries(string schema) {
        List<CommentSqlEntry> entries = [];
        foreach ((string key, string comment) in CommonCases().Concat(OracleLimitCases())) {
            entries.Add(Entry(schema, "CS_Cases", key, comment));
        }

        entries.Add(Entry(schema, "CS_Cases", null, "Table level comment\nwith a second line, it's fine"));
        entries.Add(Entry(schema, "CS_View", null, "View level comment 檢視表"));
        entries.Add(Entry(schema, "CS_View", "Id", "View column comment"));
        foreach (string table in new[] { "Order Item.v1", "SELECT", "O'Brien", "A&B" }) {
            entries.Add(Entry(schema, table, null, $"Table {table} comment"));
            foreach (string column in new[] { "SELECT", "O'Brien", "a.b", "with space", "A&B", "MixedCase" }) {
                entries.Add(Entry(schema, table, column, $"Column {column} of {table}"));
            }
        }

        entries.Add(Entry(schema, "CS_Existing", "PreWhitespace", "was whitespace only"));
        entries.Add(Entry(schema, "CS_Existing", "PreDifferent", "new text"));
        entries.Add(Entry(schema, "CS_Existing", "PreSame", "same text"));
        entries.Add(Entry(schema, "CS_Existing", "PreKeep", ""));
        entries.Add(Entry(schema, "CS_Existing", "PreNone", "fresh"));
        return entries;
    }

    private static void AddSqlServerNameMatrixEntries(List<CommentSqlEntry> entries) {
        foreach (string table in new[] { SqlServerNamesTable, "Select", "Quo'te", "[Bracket]" }) {
            entries.Add(Entry(SqlServerSchemaWithSpace, table, null, $"Table {table} comment"));
            foreach (string column in NameMatrixColumns) {
                entries.Add(Entry(SqlServerSchemaWithSpace, table, column, $"Column {column} of {table}"));
            }
        }
    }

    private static Dictionary<(string, string, string?), string?> SqlServerPreExistingExpectations() {
        return new() {
            [("dbo", "CS_Existing", "PreKeep")] = "keep me",
            [("dbo", "CS_Existing", "PreUntouched")] = "untouched"
        };
    }

    private static Dictionary<(string, string, string?), string?> OraclePreExistingExpectations() {
        return new() {
            [("", "CS_Existing", "PreKeep")] = "keep me",
            [("", "CS_Existing", "PreUntouched")] = "untouched"
        };
    }

    private static CommentSqlEntry Entry(string schema, string objectName, string? column, string comment) {
        return new CommentSqlEntry { Schema = schema, ObjectName = objectName, Column = column, Comment = comment };
    }


    private static async Task<SchemaSnapshotDocument> ExportSnapshotAsync(
        DatabaseType databaseType,
        string connectionString,
        HashSet<string> objectNames
    ) {
        IDatabaseSchemaProvider provider = databaseType == DatabaseType.SqlServer
            ? new SqlServerDatabaseSchemaProvider()
            : new OracleDatabaseSchemaProvider();

        IReadOnlyList<DatabaseObjectSchema> objects = (await provider.LoadObjectsAsync(connectionString))
            .Where(x => objectNames.Contains(x.ObjectName))
            .ToList();
        DatabaseSchemaDetails details = await provider.LoadDetailsAsync(connectionString, objects);
        FilteredSchemaExport filtered = new() {
            Objects = objects,
            Columns = details.Columns,
            Indexes = details.Indexes,
            Routines = []
        };

        return new SchemaSnapshotBuilder().Build(
            "fixture.xlsx",
            new SchemaConnection { Name = "Fixture", DatabaseType = databaseType, ConnectionString = "not used" },
            new ExportProfile { Name = "Default" },
            filtered,
            []
        );
    }

    private static async Task<CommentSqlResult> GenerateAsync(
        TempDirectory directory,
        SchemaSnapshotDocument snapshot,
        List<CommentSqlEntry> entries,
        string outputFileName = "comments.sql"
    ) {
        string snapshotPath = directory.Combine($"{outputFileName}.snapshot.json");
        string commentsPath = directory.Combine($"{outputFileName}.comments.json");
        await File.WriteAllTextAsync(snapshotPath, JsonSerializer.Serialize(snapshot, SchemaArtifactJsonSerializerOptions.Default));
        await File.WriteAllTextAsync(commentsPath, JsonSerializer.Serialize(entries, CommentListJsonOptions), new UTF8Encoding(false));

        CommentSqlResult result = await new CommentSqlService().GenerateToFileAsync(snapshotPath, commentsPath, directory.Combine(outputFileName));
        string savedPath = System.IO.Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"{TestContext.CurrentContext.Test.Name}.{outputFileName}"
        );
        File.Copy(directory.Combine(outputFileName), savedPath, overwrite: true);
        TestContext.Out.WriteLine($"Generated script saved to {savedPath}");
        return result;
    }

    private static void AssertScriptSucceeded(ScriptExecutionResult execution) {
        TestContext.Out.WriteLine(execution.Describe());
        string combined = execution.Stdout + execution.Stderr;
        using (Assert.EnterMultipleScope()) {
            Assert.That(execution.ExitCode, Is.Zero, execution.Describe());
            Assert.That(combined, Does.Not.Contain("ORA-"), execution.Describe());
            Assert.That(combined, Does.Not.Contain("SP2-"), execution.Describe());
            Assert.That(combined, Does.Not.Contain("Msg "), execution.Describe());
            Assert.That(combined, Does.Not.Contain("Sqlcmd:"), execution.Describe());
        }
    }


    private static HashSet<string> SqlServerObjectNames() {
        return ["CS_Cases", "CS_View", "CS_Existing", SqlServerNamesTable, "Select", "Quo'te", "[Bracket]"];
    }

    private static string Bracket(string name) => "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";

    private static async Task PrepareSqlServerSchemaAsync(string connectionString) {
        List<string> statements = [$"CREATE SCHEMA {Bracket(SqlServerSchemaWithSpace)}"];

        string caseColumns = string.Join(", ", CommonCases().Concat(SqlServerLimitCases()).Select(x => $"{Bracket(x.Key)} nvarchar(max) NULL"));
        statements.Add($"CREATE TABLE [dbo].[CS_Cases] ([Id] int NOT NULL PRIMARY KEY, {caseColumns})");
        statements.Add("CREATE VIEW [dbo].[CS_View] AS SELECT [Id] FROM [dbo].[CS_Cases]");
        statements.Add("CREATE TABLE [dbo].[CS_Existing] ([Id] int NOT NULL PRIMARY KEY, [PreEmpty] int NULL, [PreWhitespace] int NULL, [PreDifferent] int NULL, [PreSame] int NULL, [PreKeep] int NULL, [PreUntouched] int NULL, [PreNone] int NULL)");

        string nameColumns = string.Join(", ", NameMatrixColumns.Select(x => $"{Bracket(x)} int NULL"));
        foreach (string table in new[] { SqlServerNamesTable, "Select", "Quo'te", "[Bracket]" }) {
            statements.Add($"CREATE TABLE {Bracket(SqlServerSchemaWithSpace)}.{Bracket(table)} ([Id] int NOT NULL, {nameColumns})");
        }

        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        foreach (string statement in statements) {
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync();
        }

        await AddSqlServerExtendedPropertyAsync(connectionString, "dbo", "CS_Existing", "PreEmpty", "");
        await AddSqlServerExtendedPropertyAsync(connectionString, "dbo", "CS_Existing", "PreWhitespace", "   ");
        await AddSqlServerExtendedPropertyAsync(connectionString, "dbo", "CS_Existing", "PreDifferent", "old text");
        await AddSqlServerExtendedPropertyAsync(connectionString, "dbo", "CS_Existing", "PreSame", "same text");
        await AddSqlServerExtendedPropertyAsync(connectionString, "dbo", "CS_Existing", "PreKeep", "keep me");
        await AddSqlServerExtendedPropertyAsync(connectionString, "dbo", "CS_Existing", "PreUntouched", "untouched");
    }

    private static async Task AddSqlServerExtendedPropertyAsync(
        string connectionString,
        string schema,
        string table,
        string column,
        string value
    ) {
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = """
            EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = @value,
                @level0type = N'SCHEMA', @level0name = @schema,
                @level1type = N'TABLE', @level1name = @table,
                @level2type = N'COLUMN', @level2name = @column;
            """;
        command.Parameters.Add(new SqlParameter("@value", System.Data.SqlDbType.NVarChar, 4000) { Value = value });
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@table", table);
        command.Parameters.AddWithValue("@column", column);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ReadSqlServerCommentAsync(string connectionString, string schema, string objectName, string? column) {
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT CAST(value AS nvarchar(max))
            FROM sys.fn_listextendedproperty(N'MS_Description', N'SCHEMA', @schema, @type, @object, @level2type, @level2name);
            """;
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@type", ResolveSqlServerObjectType(objectName));
        command.Parameters.AddWithValue("@object", objectName);
        command.Parameters.AddWithValue("@level2type", column is null ? DBNull.Value : "COLUMN");
        command.Parameters.AddWithValue("@level2name", column is null ? DBNull.Value : column);

        object? value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : (string)value;
    }

    private static string ResolveSqlServerObjectType(string objectName) => objectName == "CS_View" ? "VIEW" : "TABLE";

    private static async Task AssertSqlServerCommentsAsync(
        string connectionString,
        IReadOnlyList<CommentSqlEntry> entries,
        Dictionary<(string, string, string?), string?> untouchedExpectations
    ) {
        List<string> mismatches = [];
        foreach (CommentSqlEntry entry in entries.Where(x => x.Comment!.Length > 0)) {
            string? actual = await ReadSqlServerCommentAsync(connectionString, entry.Schema!, entry.ObjectName!, entry.Column);
            if (!string.Equals(actual, entry.Comment, StringComparison.Ordinal)) {
                mismatches.Add($"{entry.Schema}.{entry.ObjectName}.{entry.Column}: expected length {entry.Comment!.Length}, actual {(actual is null ? "<missing>" : actual.Length.ToString(System.Globalization.CultureInfo.InvariantCulture))}");
            }
        }

        foreach (((string schema, string objectName, string? column), string? expected) in untouchedExpectations) {
            string? actual = await ReadSqlServerCommentAsync(connectionString, schema, objectName, column);
            if (!string.Equals(actual, expected, StringComparison.Ordinal)) {
                mismatches.Add($"{schema}.{objectName}.{column}: expected untouched '{expected}', actual '{actual}'");
            }
        }

        Assert.That(mismatches, Is.Empty, string.Join(Environment.NewLine, mismatches));
    }


    private static HashSet<string> OracleObjectNames() {
        return ["CS_Cases", "CS_View", "CS_Existing", "Order Item.v1", "SELECT", "O'Brien", "A&B"];
    }

    private static string Quote(string name) => "\"" + name + "\"";

    private static async Task<string> PrepareOracleSchemaAsync(string connectionString) {
        string schema;
        await using (OracleConnection connection = new(connectionString)) {
            await connection.OpenAsync();
            await using OracleCommand command = connection.CreateCommand();
            command.CommandText = "SELECT USER FROM DUAL";
            schema = (string)(await command.ExecuteScalarAsync())!;
        }

        string caseColumns = string.Join(", ", CommonCases().Concat(OracleLimitCases()).Select(x => $"{Quote(x.Key)} VARCHAR2(10) NULL"));
        List<string> statements = [
            $"CREATE TABLE \"CS_Cases\" (\"Id\" NUMBER NOT NULL PRIMARY KEY, {caseColumns})",
            "CREATE VIEW \"CS_View\" AS SELECT \"Id\" FROM \"CS_Cases\"",
            "CREATE TABLE \"CS_Existing\" (\"Id\" NUMBER NOT NULL PRIMARY KEY, \"PreWhitespace\" NUMBER NULL, \"PreDifferent\" NUMBER NULL, \"PreSame\" NUMBER NULL, \"PreKeep\" NUMBER NULL, \"PreUntouched\" NUMBER NULL, \"PreNone\" NUMBER NULL)"
        ];

        string nameColumns = string.Join(", ", new[] { "SELECT", "O'Brien", "a.b", "with space", "A&B", "MixedCase" }.Select(x => $"{Quote(x)} NUMBER NULL"));
        foreach (string table in new[] { "Order Item.v1", "SELECT", "O'Brien", "A&B" }) {
            statements.Add($"CREATE TABLE {Quote(table)} (\"Id\" NUMBER NOT NULL, {nameColumns})");
        }

        foreach (string statement in statements) {
            await ExecuteOracleAsync(connectionString, statement);
        }

        await ExecuteOracleAsync(connectionString, $"COMMENT ON COLUMN \"{schema}\".\"CS_Existing\".\"PreWhitespace\" IS '   '");
        await ExecuteOracleAsync(connectionString, $"COMMENT ON COLUMN \"{schema}\".\"CS_Existing\".\"PreDifferent\" IS 'old text'");
        await ExecuteOracleAsync(connectionString, $"COMMENT ON COLUMN \"{schema}\".\"CS_Existing\".\"PreSame\" IS 'same text'");
        await ExecuteOracleAsync(connectionString, $"COMMENT ON COLUMN \"{schema}\".\"CS_Existing\".\"PreKeep\" IS 'keep me'");
        await ExecuteOracleAsync(connectionString, $"COMMENT ON COLUMN \"{schema}\".\"CS_Existing\".\"PreUntouched\" IS 'untouched'");
        return schema;
    }

    private static async Task ExecuteOracleAsync(string connectionString, string commandText) {
        await using OracleConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using OracleCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ReadOracleCommentAsync(string connectionString, string schema, string objectName, string? column) {
        await using OracleConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using OracleCommand command = connection.CreateCommand();
        command.BindByName = true;
        if (column is null) {
            command.CommandText = "SELECT comments FROM all_tab_comments WHERE owner = :owner AND table_name = :tableName";
        } else {
            command.CommandText = "SELECT comments FROM all_col_comments WHERE owner = :owner AND table_name = :tableName AND column_name = :columnName";
            command.Parameters.Add(new OracleParameter("columnName", column));
        }

        command.Parameters.Add(new OracleParameter("owner", schema));
        command.Parameters.Add(new OracleParameter("tableName", objectName));

        object? value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : (string)value;
    }

    private static async Task AssertOracleCommentsAsync(
        string connectionString,
        string schema,
        IReadOnlyList<CommentSqlEntry> entries,
        Dictionary<(string, string, string?), string?> untouchedExpectations
    ) {
        List<string> mismatches = [];
        foreach (CommentSqlEntry entry in entries.Where(x => x.Comment!.Length > 0)) {
            string? actual = await ReadOracleCommentAsync(connectionString, entry.Schema!, entry.ObjectName!, entry.Column);
            if (!string.Equals(actual, entry.Comment, StringComparison.Ordinal)) {
                mismatches.Add($"{entry.Schema}.{entry.ObjectName}.{entry.Column}: expected length {entry.Comment!.Length}, actual {(actual is null ? "<missing>" : actual.Length.ToString(System.Globalization.CultureInfo.InvariantCulture))}");
            }
        }

        foreach (((_, string objectName, string? column), string? expected) in untouchedExpectations) {
            string? actual = await ReadOracleCommentAsync(connectionString, schema, objectName, column);
            if (!string.Equals(actual, expected, StringComparison.Ordinal)) {
                mismatches.Add($"{schema}.{objectName}.{column}: expected untouched '{expected}', actual '{actual}'");
            }
        }

        Assert.That(mismatches, Is.Empty, string.Join(Environment.NewLine, mismatches));
    }

    [GeneratedRegex(@"^[ \t]*GO[ \t]*\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoLineRegex();

    private sealed class TempDirectory : IDisposable {
        public TempDirectory() {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"SchemaExporter.CommentSql.{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string Combine(string fileName) => System.IO.Path.Combine(Path, fileName);

        public void Dispose() {
            if (Directory.Exists(Path)) {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
