namespace CloudyWing.SchemaExporter.Core.Exporting.Snapshots;

/// <summary>
/// 表示資料庫程序的快照文件。
/// </summary>
public sealed class SchemaSnapshotRoutineDocument {
    /// <summary>
    /// 取得結構描述名稱。
    /// </summary>
    public required string SchemaName { get; init; }

    /// <summary>
    /// 取得容器名稱（例如 Oracle 的套件名稱）。
    /// </summary>
    public required string ContainerName { get; init; }

    /// <summary>
    /// 取得程序名稱。
    /// </summary>
    public required string RoutineName { get; init; }

    /// <summary>
    /// 取得程序類型（例如 procedure、function）。
    /// </summary>
    public required string RoutineType { get; init; }

    /// <summary>
    /// 取得多載識別碼。
    /// </summary>
    public required string OverloadIdentifier { get; init; }

    /// <summary>
    /// 取得參數簽章。
    /// </summary>
    public required string ParameterSignature { get; init; }

    /// <summary>
    /// 取得回傳類型。
    /// </summary>
    public required string ReturnType { get; init; }

    /// <summary>
    /// 取得程序描述。
    /// </summary>
    public required string RoutineDescription { get; init; }

    /// <summary>
    /// 取得程序定義主體。
    /// </summary>
    public required string RoutineDefinition { get; init; }
}

