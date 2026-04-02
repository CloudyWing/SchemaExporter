namespace CloudyWing.SchemaExporter.Core.Exporting.Diffs;

/// <summary>
/// 指定兩份結構描述快照之間偵測到的異動類型。
/// </summary>
public enum SchemaChangeType {
    /// <summary>
    /// 表示結構描述元素已新增。
    /// </summary>
    Added = 0,

    /// <summary>
    /// 表示結構描述元素已移除。
    /// </summary>
    Removed = 1,

    /// <summary>
    /// 表示結構描述元素已修改。
    /// </summary>
    Modified = 2
}

