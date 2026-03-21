using System.Drawing;
using CloudyWing.SpreadsheetExporter;
using CloudyWing.SpreadsheetExporter.Config;

namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// Configures spreadsheet exporter defaults shared by desktop and CLI execution paths.
/// </summary>
public static class SpreadsheetExporterBootstrapper {
    private static readonly Lock SyncRoot = new();
    private static bool isConfigured;

    /// <summary>
    /// Applies the default spreadsheet exporter implementation and cell styles.
    /// </summary>
    public static void Configure() {
        lock (SyncRoot) {
            if (isConfigured) {
                return;
            }

            SpreadsheetManager.SetExporter(static () => new NpoiExcelExporter());
            SpreadsheetManager.DefaultCellStyles = new CellStyleConfiguration(static styles => {
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
                    Style = cellStyle.Font.Style | FontStyles.IsBold
                };

                styles.CellStyle = cellStyle;
                styles.GridCellStyle = cellStyle with {
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                styles.HeaderStyle = cellStyle with {
                    Font = headerFont,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    HasBorder = true
                };
                styles.FieldStyle = cellStyle with {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    HasBorder = true
                };
            });

            isConfigured = true;
        }
    }
}

