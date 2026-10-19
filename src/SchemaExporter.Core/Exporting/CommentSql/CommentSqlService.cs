using System.Text.Json;
using CloudyWing.SchemaExporter.Core.Exporting.Snapshots;

namespace CloudyWing.SchemaExporter.Core.Exporting.CommentSql;

/// <summary>
/// 提供註解 SQL 產生的檔案層級流程：讀取 Snapshot 與註解清單、產生腳本並寫入檔案。
/// </summary>
public sealed class CommentSqlService {
    private static readonly JsonSerializerOptions CommentListJsonOptions = new() {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false
    };

    private readonly SchemaSnapshotDiffService snapshotService;
    private readonly CommentSqlGenerator generator;

    /// <summary>
    /// 初始化 <see cref="CommentSqlService"/> 類別的新執行個體。
    /// </summary>
    public CommentSqlService() : this(new SchemaSnapshotDiffService(), new CommentSqlGenerator()) {
    }

    /// <summary>
    /// 初始化 <see cref="CommentSqlService"/> 類別的新執行個體。
    /// </summary>
    /// <param name="snapshotService">用於載入 Snapshot 的服務。</param>
    /// <param name="generator">註解 SQL 產生器。</param>
    public CommentSqlService(SchemaSnapshotDiffService snapshotService, CommentSqlGenerator generator) {
        ArgumentNullException.ThrowIfNull(snapshotService);
        ArgumentNullException.ThrowIfNull(generator);

        this.snapshotService = snapshotService;
        this.generator = generator;
    }

    /// <summary>
    /// 讀取 Snapshot 與註解清單，產生註解 SQL 並寫入輸出檔。
    /// 全部驗證通過才會寫檔；任何問題都不會建立或修改輸出檔。
    /// </summary>
    /// <param name="snapshotPath">Snapshot 檔案路徑。</param>
    /// <param name="commentsPath">註解清單 JSON 檔案路徑。</param>
    /// <param name="outputPath">輸出 <c>.sql</c> 檔案路徑；既有檔案會被覆寫。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>產生結果。</returns>
    /// <exception cref="ExportValidationException">輸入檔無法讀取、格式錯誤，或清單有無法產生 SQL 的項目。</exception>
    public async Task<CommentSqlResult> GenerateToFileAsync(
        string snapshotPath,
        string commentsPath,
        string outputPath,
        CancellationToken cancellationToken = default
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(commentsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        string fullOutputPath = GetFullPathOrThrow(outputPath);
        StringComparison pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(fullOutputPath, GetFullPathOrThrow(snapshotPath), pathComparison)
            || string.Equals(fullOutputPath, GetFullPathOrThrow(commentsPath), pathComparison)
        ) {
            throw new ExportValidationException($"輸出檔不可與輸入檔相同，否則會覆寫輸入：{fullOutputPath}");
        }

        SchemaSnapshotDocument snapshot = await snapshotService.LoadSnapshotAsync(snapshotPath, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<CommentSqlEntry> entries = await LoadCommentListAsync(commentsPath, cancellationToken).ConfigureAwait(false);
        CommentSqlResult result = generator.Generate(snapshot, entries);

        // 先寫入同目錄的暫存檔再取代，寫到一半失敗時不會留下截斷的輸出檔，也不影響既有檔案。
        string temporaryPath = $"{fullOutputPath}.{Guid.NewGuid():N}.tmp";
        try {
            string? directoryPath = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrWhiteSpace(directoryPath)) {
                Directory.CreateDirectory(directoryPath);
            }

            await File.WriteAllBytesAsync(temporaryPath, result.GetFileBytes(), cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, fullOutputPath, overwrite: true);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException) {
            throw new ExportValidationException($"無法寫入註解 SQL 檔案：{fullOutputPath}", ex);
        } finally {
            if (File.Exists(temporaryPath)) {
                File.Delete(temporaryPath);
            }
        }

        return result;
    }

    private static string GetFullPathOrThrow(string path) {
        try {
            return Path.GetFullPath(path.Trim());
        } catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) {
            throw new ExportValidationException($"路徑無效：{path}", ex);
        }
    }

    /// <summary>
    /// 從磁碟載入註解清單。檔案為 JSON 陣列，每個元素含 <c>schema</c>、<c>object</c>、<c>column</c>、<c>comment</c>。
    /// </summary>
    /// <param name="commentsPath">註解清單檔案路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>註解清單。</returns>
    /// <exception cref="ExportValidationException">檔案無法讀取或格式錯誤。</exception>
    public static async Task<IReadOnlyList<CommentSqlEntry>> LoadCommentListAsync(
        string commentsPath,
        CancellationToken cancellationToken = default
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(commentsPath);

        string fullPath = Path.GetFullPath(commentsPath.Trim());
        try {
            await using FileStream stream = File.OpenRead(fullPath);
            List<CommentSqlEntry>? entries = await JsonSerializer.DeserializeAsync<List<CommentSqlEntry>>(
                stream,
                CommentListJsonOptions,
                cancellationToken
            ).ConfigureAwait(false);

            if (entries is null || entries.Any(x => x is null)) {
                throw new ExportValidationException($"註解清單檔案內容無效，必須是由物件組成的 JSON 陣列：{fullPath}");
            }

            return entries;
        } catch (ExportValidationException) {
            throw;
        } catch (JsonException ex) {
            throw new ExportValidationException($"註解清單檔案格式無效：{fullPath}（{ex.Message}）", ex);
        } catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException) {
            throw new ExportValidationException($"無法讀取註解清單檔案：{fullPath}", ex);
        }
    }
}
