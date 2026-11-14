namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 定義匯出完成後開啟輸出資料夾的 Host 行為。Core 只決定何時開啟，實際的 Shell 呼叫由 Host 提供。
/// </summary>
public interface IOutputFolderLauncher {
    /// <summary>
    /// 開啟指定的輸出資料夾。
    /// </summary>
    /// <param name="directoryPath">要開啟的資料夾完整路徑。</param>
    /// <exception cref="InvalidOperationException">目前環境無法開啟資料夾時。</exception>
    /// <exception cref="System.ComponentModel.Win32Exception">作業系統拒絕開啟資料夾時。</exception>
    void Open(string directoryPath);
}
