namespace CloudyWing.SchemaExporter.Core.SchemaProviders;

/// <summary>
/// 使用結構描述、容器、名稱、類型和多載識別碼識別資料庫程序。
/// </summary>
/// <param name="SchemaName">資料庫結構描述名稱。</param>
/// <param name="ContainerName">選擇性的所屬套件或物件名稱。</param>
/// <param name="RoutineName">程序名稱。</param>
/// <param name="RoutineType">提供者特定的程序類型。</param>
/// <param name="OverloadIdentifier">多載識別碼（若有）。</param>
public readonly record struct DatabaseRoutineKey(
    string SchemaName,
    string ContainerName,
    string RoutineName,
    string RoutineType,
    string OverloadIdentifier
);

