#nullable enable

using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CloudyWing.SchemaExporter.SchemaProviders;
using CloudyWing.SpreadsheetExporter;
using CloudyWing.SpreadsheetExporter.Templates.Grid;
using CloudyWing.SpreadsheetExporter.Templates.RecordSet;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;

namespace CloudyWing.SchemaExporter;

public partial class ViewModel : ObservableObject {
    private readonly SchemaOptions schemaOptions;
    private readonly IDatabaseSchemaProviderFactory providerFactory;

    [ObservableProperty]
    private SchemaConnection? connection;

    public ObservableCollection<SchemaConnection> Connections { get; }

    public ViewModel(
        IOptions<SchemaOptions> schemaAccessor,
        IDatabaseSchemaProviderFactory providerFactory
    ) {
        ArgumentNullException.ThrowIfNull(schemaAccessor, nameof(schemaAccessor));
        ArgumentNullException.ThrowIfNull(providerFactory, nameof(providerFactory));

        schemaOptions = schemaAccessor.Value;
        this.providerFactory = providerFactory;

        Connections = new ObservableCollection<SchemaConnection>(schemaOptions.Connections);
        Connection = Connections.FirstOrDefault();
    }

    [RelayCommand]
    private async Task SubmitAsync() {
        if (Connection is null) {
            MessageBox.Show("請先選擇連線設定。", "匯出驗證", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(schemaOptions.ExportPath)) {
            MessageBox.Show("請先設定匯出路徑。", "匯出驗證", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DatabaseSchemaExport schemaExport;
        try {
            schemaExport = await providerFactory.LoadSchemaAsync(Connection.DatabaseType, Connection.ConnectionString);
        } catch (Exception ex) {
            MessageBox.Show($"載入資料庫結構失敗：{ex.Message}", "匯出失敗", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        IEnumerable<TableInfo> tables = schemaExport.Objects
            .Select(x => new TableInfo {
                SchemaName = x.SchemaName,
                TableName = x.ObjectName,
                TableType = x.ObjectType,
                TableDescription = x.ObjectDescription
            })
            .OrderBy(x => x.SchemaName)
            .ThenBy(x => x.TableName)
            .ToArray();

        IEnumerable<ColumnInfo> columns = schemaExport.Columns
            .Select(x => new ColumnInfo {
                SchemaName = x.SchemaName,
                TableName = x.ObjectName,
                ColumnName = x.ColumnName,
                ColumnType = x.ColumnType,
                IsNullable = x.IsNullable,
                ColumnDefault = x.ColumnDefault,
                IsPrimaryKey = x.IsPrimaryKey,
                IsIdentity = x.IsIdentity,
                ColumnDescription = x.ColumnDescription,
                ColumnOrder = x.ColumnOrder
            })
            .OrderBy(x => x.SchemaName)
            .ThenBy(x => x.TableName)
            .ThenBy(x => x.ColumnOrder)
            .ToArray();

        IEnumerable<IndexInfo> indexes = schemaExport.Indexes
            .Select(x => new IndexInfo {
                SchemaName = x.SchemaName,
                TableName = x.ObjectName,
                IndexName = x.IndexName,
                IsPrimaryKey = x.IsPrimaryKey,
                IsClustered = x.IsClustered,
                IsUnique = x.IsUnique,
                IsForeignKey = x.IsForeignKey,
                Columns = x.Columns,
                OtherColumns = x.OtherColumns
            })
            .OrderBy(x => x.SchemaName)
            .ThenBy(x => x.TableName)
            .ThenBy(x => x.IndexName)
            .ToArray();

        ISpreadsheetExporter exporter = SpreadsheetManager.CreateExporter();
        BuildTableListSheet(exporter, tables);
        BuildColumnListSheet(exporter, columns);
        BuildTableDetailSheets(exporter, tables, columns, indexes);

        Directory.CreateDirectory(schemaOptions.ExportPath);
        string filePath = Path.Combine(schemaOptions.ExportPath, $"TableSchema_{Connection.Name}{exporter.FileNameExtension}");
        exporter.ExportFile(filePath);

        MessageBox.Show($"檔案「{filePath}」產出成功");
    }

    private static void BuildTableListSheet(ISpreadsheetExporter exporter, IEnumerable<TableInfo> tables) {
        CellStyle itemStyle = SpreadsheetManager.DefaultCellStyles.FieldStyle with {
            HorizontalAlignment = SpreadsheetExporter.HorizontalAlignment.Left
        };

        RecordSetTemplate<TableInfo> template = new(tables) {
            RecordHeight = Constants.AutoFitRowHeight
        };
        template.Columns.Add("Schema", x => x.SchemaName);
        template.Columns.Add("名稱", x => x.TableName, fieldStyleGenerator: x => itemStyle);
        template.Columns.Add("類型", x => x.TableType, fieldStyleGenerator: x => itemStyle);
        template.Columns.Add("描述", x => x.TableDescription, fieldStyleGenerator: x => itemStyle);

        Sheeter sheeter = exporter.CreateSheeter("資料表清單");
        sheeter.AddTemplates(template);

        sheeter.SetColumnWidth(0, 16D);
        sheeter.SetColumnWidth(1, 40D);
        sheeter.SetColumnWidth(2, 15D);
        sheeter.SetColumnWidth(3, 50D);
    }

    private static void BuildColumnListSheet(ISpreadsheetExporter exporter, IEnumerable<ColumnInfo> columns) {
        CellStyle centerFieldStyle = SpreadsheetManager.DefaultCellStyles.FieldStyle with {
            HorizontalAlignment = SpreadsheetExporter.HorizontalAlignment.Center
        };

        RecordSetTemplate<ColumnInfo> template = new(columns) {
            RecordHeight = Constants.AutoFitRowHeight
        };
        template.Columns.Add("Schema", x => x.SchemaName);
        template.Columns.Add("資料表名稱", x => x.TableName);
        template.Columns.Add("欄位名稱", x => x.ColumnName);
        template.Columns.Add("欄位型別", x => x.ColumnType);
        template.Columns.Add("預設值", x => x.ColumnDefault);
        template.Columns.Add("是否允許 Null", x => x.IsNullable, fieldStyleGenerator: _ => centerFieldStyle);
        template.Columns.Add("是否為 PK", x => x.IsPrimaryKey, fieldStyleGenerator: _ => centerFieldStyle);
        template.Columns.Add("是否為 Identity", x => x.IsIdentity, fieldStyleGenerator: _ => centerFieldStyle);
        template.Columns.Add("描述", x => x.ColumnDescription);

        Sheeter sheeter = exporter.CreateSheeter("資料表欄位清單");
        sheeter.AddTemplates(template);

        sheeter.SetColumnWidth(0, 16D);
        sheeter.SetColumnWidth(1, 36D);
        sheeter.SetColumnWidth(2, 28D);
        sheeter.SetColumnWidth(3, 30D);
        sheeter.SetColumnWidth(4, 15D);
        sheeter.SetColumnWidth(5, 15D);
        sheeter.SetColumnWidth(6, 15D);
        sheeter.SetColumnWidth(7, 15D);
        sheeter.SetColumnWidth(8, 50D);
    }

    private static void BuildTableDetailSheets(
        ISpreadsheetExporter exporter,
        IEnumerable<TableInfo> tables,
        IEnumerable<ColumnInfo> columns,
        IEnumerable<IndexInfo> indexes
    ) {
        foreach (TableInfo table in tables) {
            Sheeter sheeter = exporter.CreateSheeter(table.SheeterName);
            BuildTableDetailSheet(
                sheeter,
                table,
                columns.Where(x => x.SchemaName == table.SchemaName && x.TableName == table.TableName),
                indexes.Where(x => x.SchemaName == table.SchemaName && x.TableName == table.TableName)
            );
        }
    }

    private static void BuildTableDetailSheet(
        Sheeter sheeter,
        TableInfo table,
        IEnumerable<ColumnInfo> columns,
        IEnumerable<IndexInfo> indexes
    ) {
        CellStyle defaultGridStyle = SpreadsheetManager.DefaultCellStyles.GridCellStyle;
        CellFont defaultFont = SpreadsheetManager.DefaultCellStyles.GridCellStyle.Font;
        CellStyle headerLabelStyle = defaultGridStyle with {
            HorizontalAlignment = SpreadsheetExporter.HorizontalAlignment.Right,
            Font = defaultFont with {
                Style = defaultFont.Style | SpreadsheetExporter.FontStyles.IsBold
            }
        };

        GridTemplate headerTemplate = new();
        headerTemplate.CreateRow()
            .CreateCell("Schema：", cellStyle: headerLabelStyle)
            .CreateCell(table.SchemaName, 2)
            .CreateCell("資料表名稱：", cellStyle: headerLabelStyle)
            .CreateCell(table.TableName, 3)
            .CreateRow(Constants.AutoFitRowHeight)
            .CreateCell("資料表描述：", cellStyle: headerLabelStyle)
            .CreateCell(table.TableDescription, 6);

        sheeter.AddTemplate(headerTemplate);

        CellStyle centerFieldStyle = SpreadsheetManager.DefaultCellStyles.FieldStyle with {
            HorizontalAlignment = SpreadsheetExporter.HorizontalAlignment.Center
        };

        RecordSetTemplate<ColumnInfo> columnsTemplate = new(columns);
        columnsTemplate.Columns.Add("欄位名稱", x => x.ColumnName);
        columnsTemplate.Columns.Add("欄位型別", x => x.ColumnType);
        columnsTemplate.Columns.Add("預設值", x => x.ColumnDefault);
        columnsTemplate.Columns.Add("是否允許 Null", x => x.IsNullable, fieldStyleGenerator: _ => centerFieldStyle);
        columnsTemplate.Columns.Add("是否為 PK", x => x.IsPrimaryKey, fieldStyleGenerator: _ => centerFieldStyle);
        columnsTemplate.Columns.Add("是否為 Identity", x => x.IsIdentity, fieldStyleGenerator: _ => centerFieldStyle);
        columnsTemplate.Columns.Add("描述", x => x.ColumnDescription);

        sheeter.AddTemplate(columnsTemplate);

        if (indexes.Any()) {
            sheeter.AddTemplate(new GridTemplate().CreateRow());

            RecordSetTemplate<IndexInfo> indexesTemplate = new(indexes) {
                RecordHeight = Constants.AutoFitRowHeight
            };

            indexesTemplate.Columns.Add("索引名稱", x => x.IndexName);
            indexesTemplate.Columns.Add("是否為 PK", x => x.IsPrimaryKey, fieldStyleGenerator: _ => centerFieldStyle);
            indexesTemplate.Columns.Add("是否為叢集索引", x => x.IsClustered, fieldStyleGenerator: _ => centerFieldStyle);
            indexesTemplate.Columns.Add("是否為唯一索引", x => x.IsUnique, fieldStyleGenerator: _ => centerFieldStyle);
            indexesTemplate.Columns.Add("是否為外鍵", x => x.IsForeignKey, fieldStyleGenerator: _ => centerFieldStyle);
            indexesTemplate.Columns.Add("欄位", x => x.Columns, x => x.UseValue(v => v.Value?.Replace("\n", Environment.NewLine)));
            indexesTemplate.Columns.Add("Include/外鍵 欄位", x => x.OtherColumns, x => x.UseValue(v => v.Value?.Replace("\n", Environment.NewLine)));

            sheeter.AddTemplate(indexesTemplate);
        }

        sheeter.SetColumnWidth(0, 40D);
        sheeter.SetColumnWidth(1, 15D);
        sheeter.SetColumnWidth(2, 15D);
        sheeter.SetColumnWidth(3, 15D);
        sheeter.SetColumnWidth(4, 15D);
        sheeter.SetColumnWidth(5, 25D);
        sheeter.SetColumnWidth(6, 50D);
    }

}
