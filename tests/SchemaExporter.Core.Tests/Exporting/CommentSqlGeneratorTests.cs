using System.Text;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Core.Exporting.CommentSql;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;

namespace CloudyWing.SchemaExporter.Core.Tests.Exporting;

[TestFixture]
public sealed class CommentSqlGeneratorTests {
    private const string Emoji = "\U0001F600";


    [Test]
    public void Generate_SqlServerCommentWithSingleQuote_DoublesTheQuote() {
        CommentSqlResult result = Generate(DatabaseType.SqlServer, "It's a user's name");

        Assert.That(result.SqlText, Does.Contain("DECLARE @value nvarchar(4000) = N'It''s a user''s name';"));
    }

    [Test]
    public void Generate_SqlServerCommentWithChineseAndEmoji_UsesNPrefixedLiteralAndKeepsText() {
        string comment = "使用者名稱 " + Emoji + " 完整顯示";

        CommentSqlResult result = Generate(DatabaseType.SqlServer, comment);

        Assert.That(result.SqlText, Does.Contain($"= N'{comment}';"));
    }

    [TestCase("line1\r\nline2", "N'line1'\n    + NCHAR(13) + NCHAR(10)\n    + N'line2'", TestName = "Generate_SqlServerMultilineComment_CrLf")]
    [TestCase("line1\nline2", "N'line1'\n    + NCHAR(10)\n    + N'line2'", TestName = "Generate_SqlServerMultilineComment_Lf")]
    [TestCase("line1\n\nline3", "N'line1'\n    + NCHAR(10) + NCHAR(10)\n    + N'line3'", TestName = "Generate_SqlServerMultilineComment_BlankLine")]
    [TestCase("\nstart", "= NCHAR(10)\n    + N'start'", TestName = "Generate_SqlServerMultilineComment_LeadingLineBreak")]
    public void Generate_SqlServerMultilineComment_EncodesLineBreaksWithoutRawNewlinesInValue(string comment, string expectedFragment) {
        CommentSqlResult result = Generate(DatabaseType.SqlServer, comment);

        Assert.That(result.SqlText, Does.Contain(expectedFragment));
        Assert.That(result.SqlText, Does.Not.Contain("\r"));
    }

    [Test]
    public void Generate_SqlServerCommentWithStandaloneGoLine_DoesNotProduceExtraBatchSeparator() {
        CommentSqlResult result = Generate(DatabaseType.SqlServer, "before\nGO\nafter");

        string[] lines = result.SqlText.Split('\n');
        int goLineCount = lines.Count(x => x.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase));
        Assert.That(goLineCount, Is.EqualTo(1));
        Assert.That(result.SqlText, Does.Contain("N'before'\n    + NCHAR(10)\n    + N'GO'\n    + NCHAR(10)\n    + N'after'"));
    }

    [Test]
    public void Generate_SqlServerCommentWithSqlcmdVariableSyntax_EncodesDollarSign() {
        CommentSqlResult result = Generate(DatabaseType.SqlServer, "cost $(Price) and $5");

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.SqlText, Does.Contain("N'cost '\n    + NCHAR(36)\n    + N'(Price) and $5'"));
            Assert.That(result.SqlText.Replace("\\u0024(", ""), Does.Not.Contain("$("));
        }
    }

    [TestCase("   ", TestName = "Generate_SqlServerCommentOnlyWhitespace")]
    [TestCase("  padded  ", TestName = "Generate_SqlServerCommentWithLeadingAndTrailingWhitespace")]
    [TestCase("tab\tinside", TestName = "Generate_SqlServerCommentWithTab")]
    public void Generate_SqlServerCommentWithWhitespace_KeepsWhitespaceVerbatim(string comment) {
        CommentSqlResult result = Generate(DatabaseType.SqlServer, comment);

        Assert.That(result.SqlText, Does.Contain($"= N'{comment}';"));
    }

    [Test]
    public void Generate_SqlServerCommentAtLimit_Succeeds() {
        CommentSqlResult result = Generate(DatabaseType.SqlServer, new string('a', 3750));

        Assert.That(result.AddCount, Is.EqualTo(1));
    }

    [Test]
    public void Generate_SqlServerCommentOverLimit_ThrowsInsteadOfTruncating() {
        ExportValidationException? exception = Assert.Throws<ExportValidationException>(
            () => Generate(DatabaseType.SqlServer, new string('a', 3751))
        );

        Assert.That(exception!.Message, Does.Contain("7,500"));
    }

    [Test]
    public void Generate_SqlServerCommentOfSurrogatePairsOverLimit_CountsUtf16Units() {
        string atLimit = string.Concat(Enumerable.Repeat(Emoji, 1875));
        string overLimit = atLimit + "a";

        Assert.DoesNotThrow(() => Generate(DatabaseType.SqlServer, atLimit));
        Assert.Throws<ExportValidationException>(() => Generate(DatabaseType.SqlServer, overLimit));
    }

    [Test]
    public void Generate_SqlServerLongComment_SplitsLiteralsWithoutBreakingSurrogatePairs() {
        string comment = string.Concat(Enumerable.Repeat("中" + Emoji, 700));

        CommentSqlResult result = Generate(DatabaseType.SqlServer, comment);

        Assert.That(result.SqlText.Split('\n').All(x => x.Length < 1100), Is.True);
        Assert.That(ExtractSqlServerValue(result.SqlText), Is.EqualTo(comment));
    }

    [Test]
    public void Generate_SqlServerNamesWithSpecialCharacters_QuotesNamesAsStringLiterals() {
        SchemaSnapshotDocument snapshot = CreateSnapshot(
            DatabaseType.SqlServer,
            CreateObject("my schema", "Order Item.v1", "BASE TABLE", "", ("select", ""), ("O'Brien \"x\"", ""), ("a.b", ""))
        );

        CommentSqlResult result = Generator().Generate(snapshot, [
            Entry("my schema", "Order Item.v1", null, "table"),
            Entry("my schema", "Order Item.v1", "select", "reserved"),
            Entry("my schema", "Order Item.v1", "O'Brien \"x\"", "quote"),
            Entry("my schema", "Order Item.v1", "a.b", "dot")
        ]);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.SqlText, Does.Contain("@level0name = N'my schema', @level1type = N'TABLE', @level1name = N'Order Item.v1'"));
            Assert.That(result.SqlText, Does.Contain("@level2type = N'COLUMN', @level2name = N'select'"));
            Assert.That(result.SqlText, Does.Contain("@level2name = N'O''Brien \"x\"'"));
            Assert.That(result.SqlText, Does.Contain("@level2name = N'a.b'"));
            Assert.That(result.SqlText, Does.Contain("fn_listextendedproperty(N'MS_Description', N'SCHEMA', N'my schema', N'TABLE', N'Order Item.v1', N'COLUMN', N'O''Brien \"x\"')"));
        }
    }

    [Test]
    public void Generate_SqlServerView_UsesViewLevelType() {
        SchemaSnapshotDocument snapshot = CreateSnapshot(
            DatabaseType.SqlServer,
            CreateObject("dbo", "ActiveUsers", "VIEW", "", ("Id", ""))
        );

        CommentSqlResult result = Generator().Generate(snapshot, [Entry("dbo", "ActiveUsers", "Id", "id")]);

        Assert.That(result.SqlText, Does.Contain("@level1type = N'VIEW', @level1name = N'ActiveUsers'"));
    }

    [Test]
    public void Generate_SqlServerTableLevelComment_OmitsLevel2Arguments() {
        CommentSqlResult result = Generator().Generate(
            CreateSnapshot(DatabaseType.SqlServer, CreateObject("dbo", "Users", "BASE TABLE", "", ("Id", ""))),
            [Entry("dbo", "Users", null, "table comment")]
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.SqlText, Does.Not.Contain("@level2"));
            Assert.That(result.SqlText, Does.Contain("N'TABLE', N'Users', NULL, NULL)"));
        }
    }

    [Test]
    public void Generate_SqlServerResult_UsesUtf8BomAndAddOrUpdateStatements() {
        CommentSqlResult result = Generate(DatabaseType.SqlServer, "中文");
        byte[] bytes = result.GetFileBytes();

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.EmitUtf8Bom, Is.True);
            Assert.That(bytes.Take(3), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
            Assert.That(result.SqlText, Does.Contain("sp_updateextendedproperty"));
            Assert.That(result.SqlText, Does.Contain("sp_addextendedproperty"));
            Assert.That(result.SqlText, Does.Contain("MS_Description"));
        }
    }


    [Test]
    public void Generate_OracleHeader_DisablesSubstitutionAndAllowsBlankLinesBeforeFirstStatement() {
        CommentSqlResult result = Generate(DatabaseType.Oracle, "R&D department & more");

        string[] lines = result.SqlText.Split('\n');
        int defineIndex = Array.IndexOf(lines, "SET DEFINE OFF");
        int blankLinesIndex = Array.IndexOf(lines, "SET SQLBLANKLINES ON");
        int statementIndex = Array.FindIndex(lines, x => x.StartsWith("COMMENT ON", StringComparison.Ordinal));
        using (Assert.EnterMultipleScope()) {
            Assert.That(defineIndex, Is.EqualTo(0));
            Assert.That(blankLinesIndex, Is.GreaterThan(defineIndex).And.LessThan(statementIndex));
            Assert.That(result.SqlText, Does.Contain("IS 'R&D department & more';"));
        }
    }

    [Test]
    public void Generate_OracleCommentWithSingleQuote_DoublesTheQuote() {
        CommentSqlResult result = Generate(DatabaseType.Oracle, "It's a user's name");

        Assert.That(result.SqlText, Does.Contain("IS 'It''s a user''s name';"));
    }

    [Test]
    public void Generate_OracleCommentWithChineseAndEmoji_KeepsTextAndHasNoBom() {
        string comment = "使用者名稱 " + Emoji;

        CommentSqlResult result = Generate(DatabaseType.Oracle, comment);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.SqlText, Does.Contain($"IS '{comment}';"));
            Assert.That(result.EmitUtf8Bom, Is.False);
            Assert.That(result.GetFileBytes().Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
        }
    }

    [TestCase("line1\r\nline2", "'line1'\n        || CHR(13) || CHR(10)\n        || 'line2'", TestName = "Generate_OracleMultilineComment_CrLf")]
    [TestCase("line1\nline2", "'line1'\n        || CHR(10)\n        || 'line2'", TestName = "Generate_OracleMultilineComment_Lf")]
    public void Generate_OracleMultilineComment_UsesPlsqlBlockWithChr(string comment, string expectedFragment) {
        CommentSqlResult result = Generate(DatabaseType.Oracle, comment);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.SqlText, Does.Contain(expectedFragment));
            Assert.That(result.SqlText, Does.Contain("EXECUTE IMMEDIATE 'COMMENT ON TABLE \"dbo\".\"Users\" IS '''"));
            Assert.That(result.SqlText, Does.Not.Contain("\r"));
        }
    }

    [Test]
    public void Generate_OracleCommentWithSqlPlusSpecialLines_NeverEmitsThemAsRawLines() {
        string comment = "a\n\n/\n.\n#set x\n@file\n-- dash\nREM text\nlast;";

        CommentSqlResult result = Generate(DatabaseType.Oracle, comment);

        string[] lines = result.SqlText.Split('\n');
        using (Assert.EnterMultipleScope()) {
            Assert.That(lines.Count(x => x == "/"), Is.EqualTo(1), "only the PL/SQL block terminator may be a bare slash");
            Assert.That(lines, Does.Not.Contain("."));
            Assert.That(lines.Any(x => x.StartsWith('#') || x.StartsWith('@')), Is.False);
        }
    }

    [Test]
    public void Generate_OracleCommentWithTrailingSemicolonAndComments_StaysOnOneStatementLine() {
        CommentSqlResult result = Generate(DatabaseType.Oracle, "use; /* x */ -- y;");

        Assert.That(result.SqlText, Does.Contain("IS 'use; /* x */ -- y;';"));
    }

    [Test]
    public void Generate_OracleNamesWithSpecialCharacters_AlwaysQuotesIdentifiers() {
        SchemaSnapshotDocument snapshot = CreateSnapshot(
            DatabaseType.Oracle,
            CreateObject("My Schema", "Order.Item", "BASE TABLE", "", ("SELECT", ""), ("O'Brien", ""), ("a&b", ""), ("lower", ""))
        );

        CommentSqlResult result = Generator().Generate(snapshot, [
            Entry("My Schema", "Order.Item", null, "table"),
            Entry("My Schema", "Order.Item", "SELECT", "reserved"),
            Entry("My Schema", "Order.Item", "O'Brien", "quote"),
            Entry("My Schema", "Order.Item", "a&b", "ampersand"),
            Entry("My Schema", "Order.Item", "lower", "case")
        ]);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.SqlText, Does.Contain("COMMENT ON TABLE \"My Schema\".\"Order.Item\" IS 'table';"));
            Assert.That(result.SqlText, Does.Contain("COMMENT ON COLUMN \"My Schema\".\"Order.Item\".\"SELECT\" IS 'reserved';"));
            Assert.That(result.SqlText, Does.Contain("COMMENT ON COLUMN \"My Schema\".\"Order.Item\".\"O'Brien\" IS 'quote';"));
            Assert.That(result.SqlText, Does.Contain("\"a&b\" IS 'ampersand';"));
            Assert.That(result.SqlText, Does.Contain("\"lower\" IS 'case';"));
        }
    }

    [Test]
    public void Generate_OracleNameWithDoubleQuote_Throws() {
        SchemaSnapshotDocument snapshot = CreateSnapshot(
            DatabaseType.Oracle,
            CreateObject("APP", "T\"X", "BASE TABLE", "")
        );

        ExportValidationException? exception = Assert.Throws<ExportValidationException>(
            () => Generator().Generate(snapshot, [Entry("APP", "T\"X", null, "x")])
        );

        Assert.That(exception!.Message, Does.Contain("雙引號"));
    }

    [Test]
    public void Generate_OracleBlockForMultilineWithNames_EscapesQuotesInsideDynamicSql() {
        SchemaSnapshotDocument snapshot = CreateSnapshot(
            DatabaseType.Oracle,
            CreateObject("APP", "O'Brien", "BASE TABLE", "", ("C'1", ""))
        );

        CommentSqlResult result = Generator().Generate(snapshot, [Entry("APP", "O'Brien", "C'1", "a'b\nc")]);

        Assert.That(
            result.SqlText,
            Does.Contain("EXECUTE IMMEDIATE 'COMMENT ON COLUMN \"APP\".\"O''Brien\".\"C''1\" IS '''")
        );
        Assert.That(result.SqlText, Does.Contain("'a''b'"));
    }

    [Test]
    public void Generate_OracleCommentAtByteLimit_Succeeds() {
        string asciiAtLimit = new('a', 4000);
        string chineseAtLimit = new string('中', 1333) + "a";

        Assert.DoesNotThrow(() => Generate(DatabaseType.Oracle, asciiAtLimit));
        Assert.DoesNotThrow(() => Generate(DatabaseType.Oracle, chineseAtLimit));
    }

    [Test]
    public void Generate_OracleCommentOverByteLimit_ThrowsInsteadOfTruncating() {
        string asciiOver = new('a', 4001);
        string chineseOver = new('中', 1334);
        string emojiOver = string.Concat(Enumerable.Repeat(Emoji, 1001));

        using (Assert.EnterMultipleScope()) {
            Assert.Throws<ExportValidationException>(() => Generate(DatabaseType.Oracle, asciiOver));
            Assert.Throws<ExportValidationException>(() => Generate(DatabaseType.Oracle, chineseOver));
            Assert.Throws<ExportValidationException>(() => Generate(DatabaseType.Oracle, emojiOver));
        }
    }

    [Test]
    public void Generate_OracleLongComment_UsesBlockWithShortLines() {
        string comment = new('a', 3900);

        CommentSqlResult result = Generate(DatabaseType.Oracle, comment);

        string[] lines = result.SqlText.Split('\n');
        using (Assert.EnterMultipleScope()) {
            Assert.That(lines.Max(x => Encoding.UTF8.GetByteCount(x)), Is.LessThan(800));
            Assert.That(ExtractOracleValue(result.SqlText), Is.EqualTo(comment));
        }
    }

    [Test]
    public void Generate_OracleLongChineseComment_KeepsStatementLineWithinByteBudget() {
        string comment = new('中', 1300);

        CommentSqlResult result = Generate(DatabaseType.Oracle, comment);

        string[] lines = result.SqlText.Split('\n');
        using (Assert.EnterMultipleScope()) {
            Assert.That(lines.Max(x => Encoding.UTF8.GetByteCount(x)), Is.LessThan(2200));
            Assert.That(ExtractOracleValue(result.SqlText), Is.EqualTo(comment));
        }
    }

    [TestCase("   ", TestName = "Generate_OracleCommentOnlyWhitespace")]
    [TestCase("  padded  ", TestName = "Generate_OracleCommentWithLeadingAndTrailingWhitespace")]
    [TestCase("tab\tinside", TestName = "Generate_OracleCommentWithTab")]
    public void Generate_OracleCommentWithWhitespace_KeepsWhitespaceVerbatim(string comment) {
        CommentSqlResult result = Generate(DatabaseType.Oracle, comment);

        Assert.That(result.SqlText, Does.Contain($"IS '{comment}';"));
    }


    [TestCase(DatabaseType.SqlServer)]
    [TestCase(DatabaseType.Oracle)]
    public void Generate_EmptyComment_IsSkippedAndNeverClearsExistingComment(DatabaseType databaseType) {
        SchemaSnapshotDocument snapshot = CreateSnapshot(
            databaseType,
            CreateObject("dbo", "Users", "BASE TABLE", "keep me", ("Id", "keep me too"))
        );

        CommentSqlResult result = Generator().Generate(snapshot, [
            Entry("dbo", "Users", null, ""),
            Entry("dbo", "Users", "Id", "")
        ]);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.SkippedEmptyCount, Is.EqualTo(2));
            Assert.That(result.AddCount, Is.Zero);
            Assert.That(result.UpdateCount, Is.Zero);
            Assert.That(result.SqlText, Does.Not.Contain("keep me"));
            Assert.That(result.SqlText.Split('\n').Any(x => x.StartsWith("COMMENT ON", StringComparison.Ordinal)), Is.False);
            Assert.That(result.SqlText, Does.Not.Contain("DECLARE"));
        }
    }

    [TestCase(DatabaseType.SqlServer)]
    [TestCase(DatabaseType.Oracle)]
    public void Generate_ExistingCommentClassification_SplitsAddUpdateAndUnchanged(DatabaseType databaseType) {
        SchemaSnapshotDocument snapshot = CreateSnapshot(
            databaseType,
            CreateObject(
                "dbo", "Users", "BASE TABLE", "same",
                ("NoComment", ""), ("Changed", "old value"), ("Same", "same"), ("Blank", "   ")
            )
        );

        CommentSqlResult result = Generator().Generate(snapshot, [
            Entry("dbo", "Users", null, "same"),
            Entry("dbo", "Users", "NoComment", "new"),
            Entry("dbo", "Users", "Changed", "newer"),
            Entry("dbo", "Users", "Same", "same"),
            Entry("dbo", "Users", "Blank", "now real")
        ]);

        string updateSection = result.SqlText[result.SqlText.IndexOf("===== Update", StringComparison.Ordinal)..];
        string addSection = result.SqlText[..result.SqlText.IndexOf("===== Update", StringComparison.Ordinal)];
        using (Assert.EnterMultipleScope()) {
            Assert.That(result.AddCount, Is.EqualTo(1));
            Assert.That(result.UpdateCount, Is.EqualTo(2));
            Assert.That(result.UnchangedCount, Is.EqualTo(2));
            Assert.That(addSection, Does.Contain("dbo.Users.NoComment"));
            Assert.That(addSection, Does.Not.Contain("Changed"));
            Assert.That(updateSection, Does.Contain("-- existing: \"old value\""));
            Assert.That(updateSection, Does.Contain("-- existing (whitespace-only): \"   \""));
            Assert.That(updateSection, Does.Not.Contain("NoComment"));
        }
    }

    [TestCase(DatabaseType.SqlServer)]
    [TestCase(DatabaseType.Oracle)]
    public void Generate_ExistingCommentWithSpecialCharacters_IsShownOnOneEscapedCommentLine(DatabaseType databaseType) {
        SchemaSnapshotDocument snapshot = CreateSnapshot(
            databaseType,
            CreateObject("dbo", "Users", "BASE TABLE", "line1\r\nGO\n/\n\"q\" \\ $(x)\t" + Emoji)
        );

        CommentSqlResult result = Generator().Generate(snapshot, [Entry("dbo", "Users", null, "replacement")]);

        string existingLine = result.SqlText.Split('\n').Single(x => x.StartsWith("-- existing: ", StringComparison.Ordinal));
        Assert.That(
            existingLine,
            Is.EqualTo("-- existing: \"line1\\r\\nGO\\n/\\n\\\"q\\\" \\\\ \\u0024(x)\\t" + Emoji + "\"")
        );
    }

    [Test]
    public void Generate_VeryLongExistingComment_WrapsIntoSeveralCommentLines() {
        SchemaSnapshotDocument snapshot = CreateSnapshot(
            DatabaseType.Oracle,
            CreateObject("dbo", "Users", "BASE TABLE", new string('x', 3000))
        );

        CommentSqlResult result = Generator().Generate(snapshot, [Entry("dbo", "Users", null, "short")]);

        string[] commentLines = result.SqlText.Split('\n')
            .Where(x => x.StartsWith("-- existing", StringComparison.Ordinal) || x.StartsWith("-- ...continued", StringComparison.Ordinal))
            .ToArray();
        using (Assert.EnterMultipleScope()) {
            Assert.That(commentLines.Length, Is.GreaterThan(5));
            Assert.That(commentLines.Max(x => x.Length), Is.LessThan(450));
        }
    }

    [TestCase(DatabaseType.SqlServer)]
    [TestCase(DatabaseType.Oracle)]
    public void Generate_SameInputInAnyEntryOrder_ProducesByteIdenticalOutput(DatabaseType databaseType) {
        SchemaSnapshotDocument snapshot = CreateSnapshot(
            databaseType,
            CreateObject("b", "T2", "BASE TABLE", "", ("c2", ""), ("c1", "old")),
            CreateObject("a", "T1", "BASE TABLE", "", ("z", ""))
        );
        List<CommentSqlEntry> entries = [
            Entry("b", "T2", "c2", "two"),
            Entry("a", "T1", null, "table one"),
            Entry("b", "T2", "c1", "one"),
            Entry("a", "T1", "z", "zed"),
            Entry("b", "T2", null, "table two")
        ];
        List<CommentSqlEntry> reversed = [.. entries.AsEnumerable().Reverse()];

        byte[] first = Generator().Generate(snapshot, entries).GetFileBytes();
        byte[] second = Generator().Generate(snapshot, entries).GetFileBytes();
        byte[] third = Generator().Generate(snapshot, reversed).GetFileBytes();

        using (Assert.EnterMultipleScope()) {
            Assert.That(second, Is.EqualTo(first));
            Assert.That(third, Is.EqualTo(first));
        }
    }

    [Test]
    public void Generate_StatementOrder_IsOrdinalBySchemaObjectThenTableBeforeColumns() {
        SchemaSnapshotDocument snapshot = CreateSnapshot(
            DatabaseType.Oracle,
            CreateObject("S", "B", "BASE TABLE", "", ("Y", ""), ("X", "")),
            CreateObject("S", "A", "BASE TABLE", "", ("Z", ""))
        );

        CommentSqlResult result = Generator().Generate(snapshot, [
            Entry("S", "B", "Y", "1"),
            Entry("S", "A", "Z", "2"),
            Entry("S", "B", null, "3"),
            Entry("S", "B", "X", "4")
        ]);

        string[] statements = result.SqlText.Split('\n').Where(x => x.StartsWith("COMMENT ON", StringComparison.Ordinal)).ToArray();
        Assert.That(statements, Is.EqualTo(new[] {
            "COMMENT ON COLUMN \"S\".\"A\".\"Z\" IS '2';",
            "COMMENT ON TABLE \"S\".\"B\" IS '3';",
            "COMMENT ON COLUMN \"S\".\"B\".\"X\" IS '4';",
            "COMMENT ON COLUMN \"S\".\"B\".\"Y\" IS '1';"
        }));
    }

    [TestCase(DatabaseType.SqlServer)]
    [TestCase(DatabaseType.Oracle)]
    public void Generate_Output_UsesLfNewlinesOnly(DatabaseType databaseType) {
        CommentSqlResult result = Generate(databaseType, "a\r\nb");

        Assert.That(result.SqlText, Does.Not.Contain("\r"));
        Assert.That(result.SqlText, Does.EndWith("\n"));
    }

    [TestCase("nul\0char", TestName = "Generate_CommentWithNul_Throws")]
    [TestCase("ctrl\u0001char", TestName = "Generate_CommentWithControlCharacter_Throws")]
    [TestCase("sep\x2028_char", TestName = "Generate_CommentWithLineSeparator_Throws")]
    public void Generate_CommentWithUnsupportedCharacter_Throws(string comment) {
        foreach (DatabaseType databaseType in new[] { DatabaseType.SqlServer, DatabaseType.Oracle }) {
            Assert.Throws<ExportValidationException>(() => Generate(databaseType, comment), databaseType.ToString());
        }
    }

    [Test]
    public void Generate_CommentWithLoneSurrogate_Throws() {
        string highSurrogateOnly = "lone" + (char)0xD83D + "surrogate";
        string lowSurrogateOnly = "lone" + (char)0xDE00 + "surrogate";

        foreach (DatabaseType databaseType in new[] { DatabaseType.SqlServer, DatabaseType.Oracle }) {
            using (Assert.EnterMultipleScope()) {
                Assert.Throws<ExportValidationException>(() => Generate(databaseType, highSurrogateOnly), databaseType.ToString());
                Assert.Throws<ExportValidationException>(() => Generate(databaseType, lowSurrogateOnly), databaseType.ToString());
            }
        }
    }

    [Test]
    public void Generate_SqlServerNameContainingSqlcmdVariableSyntax_Throws() {
        SchemaSnapshotDocument snapshot = CreateSnapshot(DatabaseType.SqlServer, CreateObject("dbo", "T$(x)", "BASE TABLE", ""));

        Assert.Throws<ExportValidationException>(() => Generator().Generate(snapshot, [Entry("dbo", "T$(x)", null, "x")]));
    }

    [Test]
    public void Generate_WhenSeveralEntriesAreInvalid_ReportsEveryProblemAndProducesNoSql() {
        SchemaSnapshotDocument snapshot = CreateSnapshot(
            DatabaseType.SqlServer,
            CreateObject("dbo", "Users", "BASE TABLE", "", ("Id", ""))
        );

        ExportValidationException? exception = Assert.Throws<ExportValidationException>(() => Generator().Generate(snapshot, [
            Entry("dbo", "Missing", null, "x"),
            Entry("dbo", "Users", "Nope", "x"),
            Entry("dbo", "Users", "Id", "ok"),
            Entry("dbo", "Users", "Id", "duplicate"),
            Entry("", "Users", null, "x"),
            new CommentSqlEntry { Schema = "dbo", ObjectName = "Users", Comment = null }
        ]));

        using (Assert.EnterMultipleScope()) {
            Assert.That(exception!.Message, Does.Contain("5 個問題"));
            Assert.That(exception.Message, Does.Contain("找不到物件"));
            Assert.That(exception.Message, Does.Contain("找不到欄位"));
            Assert.That(exception.Message, Does.Contain("重複指定"));
            Assert.That(exception.Message, Does.Contain("schema 與 object"));
            Assert.That(exception.Message, Does.Contain("缺少 comment"));
        }
    }

    [Test]
    public void Generate_NamesDifferingOnlyByCase_ResolveToSnapshotSpelling() {
        SchemaSnapshotDocument snapshot = CreateSnapshot(
            DatabaseType.Oracle,
            CreateObject("APP", "USERS", "BASE TABLE", "", ("NAME", ""))
        );

        CommentSqlResult result = Generator().Generate(snapshot, [Entry("app", "users", "name", "x")]);

        Assert.That(result.SqlText, Does.Contain("COMMENT ON COLUMN \"APP\".\"USERS\".\"NAME\" IS 'x';"));
    }

    [Test]
    public void Generate_AmbiguousCaseOnlyDifference_ThrowsUnlessSpellingIsExact() {
        SchemaSnapshotDocument snapshot = CreateSnapshot(
            DatabaseType.Oracle,
            CreateObject("APP", "T", "BASE TABLE", "", ("c", ""), ("C", ""))
        );

        using (Assert.EnterMultipleScope()) {
            Assert.Throws<ExportValidationException>(() => Generator().Generate(snapshot, [Entry("APP", "T", "cC", "x")]));
            Assert.Throws<ExportValidationException>(() => Generator().Generate(snapshot, [Entry("APP", "T", "c ", "x")]));
            CommentSqlResult exact = Generator().Generate(snapshot, [Entry("APP", "T", "C", "x")]);
            Assert.That(exact.SqlText, Does.Contain("\"APP\".\"T\".\"C\" IS 'x';"));
        }
    }

    [Test]
    public void Generate_ObjectOfUnsupportedType_Throws() {
        SchemaSnapshotDocument snapshot = CreateSnapshot(DatabaseType.SqlServer, CreateObject("dbo", "Syn", "SYNONYM", ""));

        Assert.Throws<ExportValidationException>(() => Generator().Generate(snapshot, [Entry("dbo", "Syn", null, "x")]));
    }

    [Test]
    public void Generate_SnapshotWithUnknownDatabaseType_Throws() {
        SchemaSnapshotDocument snapshot = CreateSnapshot(DatabaseType.SqlServer, CreateObject("dbo", "Users", "BASE TABLE", ""), "Postgres");

        Assert.Throws<ExportValidationException>(() => Generator().Generate(snapshot, [Entry("dbo", "Users", null, "x")]));
    }

    [Test]
    public void Generate_SnapshotWithRedactionDiagnostic_ReturnsWarning() {
        SchemaSnapshotDocument snapshot = CreateSnapshot(
            DatabaseType.SqlServer,
            [CreateObject("dbo", "Users", "BASE TABLE", "")],
            diagnostics: [new SchemaSnapshotDiagnostic { Severity = "Info", Category = "Redaction", Message = "masked" }]
        );

        CommentSqlResult result = Generator().Generate(snapshot, [Entry("dbo", "Users", null, "x")]);

        Assert.That(result.Warnings, Has.Count.EqualTo(1));
    }

    [Test]
    public void Generate_ConnectionNameWithTroublesomeCharacters_StaysInsideSingleCommentLine() {
        SchemaSnapshotDocument snapshot = CreateSnapshot(
            DatabaseType.Oracle,
            [CreateObject("dbo", "Users", "BASE TABLE", "")],
            connectionName: "prod\n/\n$(x)"
        );

        CommentSqlResult result = Generator().Generate(snapshot, [Entry("dbo", "Users", null, "x")]);

        string connectionLine = result.SqlText.Split('\n').Single(x => x.StartsWith("-- Snapshot connection:", StringComparison.Ordinal));
        Assert.That(connectionLine, Is.EqualTo("-- Snapshot connection: prod\\n/\\n\\u0024(x)"));
    }

    [Test]
    public void Generate_EmptyEntryList_ProducesScriptWithBothSectionsEmpty() {
        CommentSqlResult result = Generator().Generate(
            CreateSnapshot(DatabaseType.SqlServer, CreateObject("dbo", "Users", "BASE TABLE", "")),
            []
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.AddCount, Is.Zero);
            Assert.That(result.UpdateCount, Is.Zero);
            Assert.That(result.SqlText.Split('\n').Count(x => x == "-- (none)"), Is.EqualTo(2));
            Assert.That(result.SqlText, Does.Not.Contain("DECLARE"));
        }
    }


    private static CommentSqlGenerator Generator() => new();

    private static CommentSqlResult Generate(DatabaseType databaseType, string comment) {
        SchemaSnapshotDocument snapshot = CreateSnapshot(
            databaseType,
            CreateObject("dbo", "Users", "BASE TABLE", "")
        );

        return Generator().Generate(snapshot, [Entry("dbo", "Users", null, comment)]);
    }

    private static CommentSqlEntry Entry(string schema, string objectName, string? column, string comment) {
        return new CommentSqlEntry { Schema = schema, ObjectName = objectName, Column = column, Comment = comment };
    }

    private static SchemaSnapshotObjectDocument CreateObject(
        string schema,
        string name,
        string type,
        string description,
        params (string Name, string Description)[] columns
    ) {
        return new SchemaSnapshotObjectDocument {
            SchemaName = schema,
            ObjectName = name,
            ObjectType = type,
            ObjectDescription = description,
            Columns = columns.Select((x, index) => new SchemaSnapshotColumnDocument {
                ColumnName = x.Name,
                ColumnType = "nvarchar(50)",
                IsNullable = "Yes",
                ColumnDefault = "",
                IsPrimaryKey = "No",
                IsIdentity = "No",
                ColumnDescription = x.Description,
                ColumnOrder = index + 1
            }).ToList(),
            Indexes = []
        };
    }

    private static SchemaSnapshotDocument CreateSnapshot(DatabaseType databaseType, params SchemaSnapshotObjectDocument[] objects) {
        return CreateSnapshot(databaseType, objects, databaseTypeText: null);
    }

    private static SchemaSnapshotDocument CreateSnapshot(
        DatabaseType databaseType,
        SchemaSnapshotObjectDocument databaseObject,
        string databaseTypeText
    ) {
        return CreateSnapshot(databaseType, [databaseObject], databaseTypeText: databaseTypeText);
    }

    private static SchemaSnapshotDocument CreateSnapshot(
        DatabaseType databaseType,
        IReadOnlyList<SchemaSnapshotObjectDocument> objects,
        string? databaseTypeText = null,
        IReadOnlyList<SchemaSnapshotDiagnostic>? diagnostics = null,
        string connectionName = "Test"
    ) {
        return new SchemaSnapshotDocument {
            SchemaVersion = 3,
            ExportedAt = DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"),
            ConnectionName = connectionName,
            DatabaseType = databaseTypeText ?? databaseType.ToString(),
            ProfileName = "Default",
            OutputFilePath = "x.xlsx",
            Counts = new SchemaSnapshotCounts(),
            Diagnostics = diagnostics ?? [],
            Objects = objects,
            Routines = []
        };
    }

    private static string ExtractSqlServerValue(string sql) {
        int start = sql.IndexOf("DECLARE @value nvarchar(4000) = ", StringComparison.Ordinal) + "DECLARE @value nvarchar(4000) = ".Length;
        int end = sql.IndexOf(";\nIF EXISTS", start, StringComparison.Ordinal);
        string expression = sql[start..end];

        StringBuilder value = new();
        foreach (string part in expression.Split(["\n    + ", " + "], StringSplitOptions.None)) {
            if (part.StartsWith("N'", StringComparison.Ordinal)) {
                value.Append(part[2..^1].Replace("''", "'", StringComparison.Ordinal));
            } else if (part.StartsWith("NCHAR(", StringComparison.Ordinal)) {
                value.Append((char)int.Parse(part["NCHAR(".Length..^1], System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return value.ToString();
    }

    private static string ExtractOracleValue(string sql) {
        string[] lines = sql.Split('\n');
        int startLine = Array.FindIndex(lines, x => x.StartsWith("COMMENT ON", StringComparison.Ordinal) || x.StartsWith("    v_comment :=", StringComparison.Ordinal));
        if (lines[startLine].StartsWith("COMMENT ON", StringComparison.Ordinal)) {
            string line = lines[startLine];
            int open = line.IndexOf(" IS '", StringComparison.Ordinal) + " IS '".Length;
            return line[open..^2].Replace("''", "'", StringComparison.Ordinal);
        }

        StringBuilder value = new();
        for (int index = startLine; !lines[index].StartsWith("    EXECUTE IMMEDIATE", StringComparison.Ordinal); index++) {
            string text = lines[index];
            text = text.Replace("    v_comment := ", "", StringComparison.Ordinal).Replace("        || ", "", StringComparison.Ordinal).TrimEnd(';');
            foreach (string part in text.Split(" || ")) {
                if (part.StartsWith('\'')) {
                    value.Append(part[1..^1].Replace("''", "'", StringComparison.Ordinal));
                } else if (part.StartsWith("CHR(", StringComparison.Ordinal)) {
                    value.Append((char)int.Parse(part["CHR(".Length..^1], System.Globalization.CultureInfo.InvariantCulture));
                }
            }
        }

        return value.ToString();
    }
}
