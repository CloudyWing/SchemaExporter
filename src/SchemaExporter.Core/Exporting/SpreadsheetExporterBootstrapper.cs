using System.Drawing;
using CloudyWing.SpreadsheetExporter;
using CloudyWing.SpreadsheetExporter.Config;
using CloudyWing.SpreadsheetExporter.Renderer.ClosedXML;

namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 設定桌面端與 CLI 執行路徑共用的試算表匯出器預設值。
/// </summary>
public static class SpreadsheetExporterBootstrapper {
    private static readonly Lock SyncRoot = new();
    private static bool isConfigured;

    /// <summary>
    /// 套用預設的試算表匯出器實作與儲存格樣式設定。
    /// </summary>
    public static void Configure() {
        lock (SyncRoot) {
            if (isConfigured) {
                return;
            }

            SpreadsheetManager.SetRenderer(static () => new ExcelRenderer());
            CellStyle cellStyle = new(
                HorizontalAlignment.Center,
                VerticalAlignment.Middle,
                false,
                true,
                Color.Empty,
                new CellFont("微軟正黑體", 10, Color.Empty, FontStyles.None),
                null,
                false
            );

            CellFont headerFont = cellStyle.Font with {
                Style = cellStyle.Font.Style | FontStyles.Bold
            };

            CellStyle leftAlignedCellStyle = cellStyle with {
                HorizontalAlignment = HorizontalAlignment.Left
            };
            CellStyle headerStyle = cellStyle with {
                Font = headerFont,
                HorizontalAlignment = HorizontalAlignment.Center,
                HasBorder = true
            };
            CellStyle fieldStyle = leftAlignedCellStyle with {
                HasBorder = true
            };

            SpreadsheetManager.DefaultCellStyles = new CellStyleConfiguration {
                CellStyle = cellStyle,
                GridCellStyle = leftAlignedCellStyle,
                HeaderStyle = headerStyle,
                FieldStyle = fieldStyle
            };

            isConfigured = true;
        }
    }
}

