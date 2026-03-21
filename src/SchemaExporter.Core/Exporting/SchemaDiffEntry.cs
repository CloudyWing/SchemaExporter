namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 表示 schema 差異文件中的一筆異動項目。
/// </summary>
public sealed class SchemaDiffEntry {
    /// <summary>
    /// 取得或設定異動類型。
    /// </summary>
    public SchemaChangeType ChangeType { get; init; }

    /// <summary>
    /// 取得或設定異動項目的識別字串。
    /// </summary>
    public string Identifier { get; init; } = "";

    /// <summary>
    /// 取得或設定屬性差異集合。
    /// </summary>
    public Dictionary<string, SchemaValueChange> PropertyChanges { get; init; } = [];
}

