namespace CloudyWing.SchemaExporter.Desktop.State;

/// <summary>
/// 定義 Desktop 介面的主題模式。
/// </summary>
public enum ThemeMode {
    /// <summary>
    /// 跟隨 Windows 的淺色／深色設定。
    /// </summary>
    System,

    /// <summary>
    /// 固定使用淺色主題。
    /// </summary>
    Light,

    /// <summary>
    /// 固定使用深色主題。
    /// </summary>
    Dark
}
