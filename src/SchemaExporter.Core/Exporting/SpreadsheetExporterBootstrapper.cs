using System.Drawing;
using CloudyWing.SpreadsheetExporter;
using CloudyWing.SpreadsheetExporter.Config;
using CloudyWing.SpreadsheetExporter.Renderer.ClosedXML;

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

            SpreadsheetManager.DefaultCellStyles = new CellStyleConfiguration {
                CellStyle = cellStyle,
                GridCellStyle = cellStyle with {
                    HorizontalAlignment = HorizontalAlignment.Left
                },
                HeaderStyle = cellStyle with {
                    Font = headerFont,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    HasBorder = true
                },
                FieldStyle = cellStyle with {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    HasBorder = true
                }
            };

            isConfigured = true;
        }
    }
}

