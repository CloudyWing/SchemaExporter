namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

/// <summary>
/// 表示與提供者無關的預存程序。
/// </summary>
public sealed class DatabaseRoutineSchema {
    /// <summary>
    /// 取得或設定結構描述名稱。
    /// </summary>
    public required string SchemaName { get; set; }

    /// <summary>
    /// 取得或設定選擇性的所屬套件或物件名稱。
    /// </summary>
    public string? ContainerName { get; set; }

    /// <summary>
    /// 取得或設定程序名稱。
    /// </summary>
    public required string RoutineName { get; set; }

    /// <summary>
    /// 取得或設定程序類型。
    /// </summary>
    public required string RoutineType { get; set; }

    /// <summary>
    /// 取得或設定多載識別碼。
    /// </summary>
    public string? OverloadIdentifier { get; set; }

    /// <summary>
    /// 取得或設定格式化的參數簽章。
    /// </summary>
    public string? ParameterSignature { get; set; }

    /// <summary>
    /// 取得或設定傳回類型。
    /// </summary>
    public string? ReturnType { get; set; }

    /// <summary>
    /// 取得或設定程序描述。
    /// </summary>
    public string? RoutineDescription { get; set; }

    /// <summary>
    /// 取得或設定程序定義。
    /// </summary>
    public string? RoutineDefinition { get; set; }

    /// <summary>
    /// 取得程序索引鍵。
    /// </summary>
    public DatabaseRoutineKey RoutineKey => new(
        SchemaName,
        ContainerName ?? "",
        RoutineName,
        RoutineType,
        OverloadIdentifier ?? ""
    );

    /// <summary>
    /// 取得結構描述限定的程序名稱。
    /// </summary>
    public string QualifiedName => string.IsNullOrWhiteSpace(ContainerName)
        ? $"{SchemaName}.{RoutineName}"
        : $"{SchemaName}.{ContainerName}.{RoutineName}";
}

