using System.Collections.ObjectModel;
using System.Data.Common;
using System.Windows;
using CloudyWing.SpreadsheetExporter;
using CloudyWing.SpreadsheetExporter.Templates.Grid;
using CloudyWing.SpreadsheetExporter.Templates.RecordSet;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace CloudyWing.SchemaExporter {
    public partial class ViewModel : ObservableObject {
        private const string QueryTablesSql = @"
                SELECT s.name AS SchemaName, 
                       t.name AS TableName,
                       'BASE TABLE' AS TableType,
                       CAST(ep.value AS NVARCHAR(MAX)) AS TableDescription
                FROM sys.tables t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                LEFT JOIN sys.extended_properties ep ON ep.major_id = t.object_id AND ep.minor_id = 0 AND ep.class = 1 AND ep.name = 'MS_Description'

                UNION

                -- 查詢視圖資訊
                SELECT s.name AS SchemaName, 
                       v.name AS ViewName,
                       'VIEW' AS TableType,
                       CAST(ep.value AS NVARCHAR(MAX)) AS TableDescription
                FROM sys.views v
                INNER JOIN sys.schemas s ON v.schema_id = s.schema_id
                LEFT JOIN sys.extended_properties ep ON ep.major_id = v.object_id AND ep.minor_id = 0 AND ep.class = 1 AND ep.name = 'MS_Description'
                ORDER BY SchemaName, TableName";

        private const string QueryColumnsSql = @"
                SELECT t.name AS TableName, 
                       c.name AS ColumnName,
                       CASE 
                           WHEN st.name IN ('char', 'varchar', 'nchar', 'nvarchar') THEN st.name + '(' + 
                                CASE 
                                    WHEN c.max_length = -1 THEN 'MAX' 
                                    WHEN st.name IN ('nchar', 'nvarchar') THEN CAST(c.max_length / 2 AS VARCHAR(MAX)) 
                                    ELSE CAST(c.max_length AS VARCHAR(MAX)) 
                                END + ')'
                           WHEN st.name IN ('decimal', 'numeric') THEN st.name + '(' + CAST(c.precision AS VARCHAR(MAX)) + ',' + CAST(c.scale AS VARCHAR(MAX)) + ')'
                           WHEN st.name IN ('datetime2', 'datetimeoffset', 'time') THEN st.name + '(' + CAST(c.scale AS VARCHAR(MAX)) + ')'
                           WHEN st.name IN ('binary', 'varbinary') THEN st.name + '(' + CASE WHEN c.max_length = -1 THEN 'MAX' ELSE CAST(c.max_length AS VARCHAR(MAX)) END + ')'
                           WHEN st.name = 'xml' THEN st.name
                           ELSE st.name
                       END AS ColumnType,
                       CASE WHEN c.is_nullable = 1 THEN 'Yes' ELSE 'No' END AS IsNullable,
                       COALESCE(d.definition, '') AS ColumnDefault,
                       CASE WHEN ic.column_id IS NOT NULL THEN 'Yes' ELSE 'No' END AS IsPrimaryKey,
                       CASE WHEN c.is_identity = 1 THEN 'Yes' ELSE 'No' END AS IsIdentity,
                       COALESCE(ep.value, '') AS ColumnDescription,
                       c.column_id
                FROM sys.columns AS c
                INNER JOIN sys.tables AS t ON t.object_id = c.object_id
                INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
                LEFT JOIN sys.index_columns AS ic ON ic.object_id = t.object_id AND ic.column_id = c.column_id AND ic.index_id = 1
                LEFT JOIN sys.default_constraints AS d ON c.default_object_id = d.object_id
                LEFT JOIN sys.extended_properties AS ep ON ep.major_id = c.object_id AND ep.minor_id = c.column_id AND ep.class = 1
                LEFT JOIN sys.types AS st ON c.user_type_id = st.user_type_id
                UNION
                SELECT v.name AS TableName, 
                       c.name AS ColumnName,
                       CASE 
                           WHEN st.name IN ('char', 'varchar', 'nchar', 'nvarchar') THEN st.name + '(' + 
                                CASE 
                                    WHEN c.max_length = -1 THEN 'MAX' 
                                    WHEN st.name IN ('nchar', 'nvarchar') THEN CAST(c.max_length / 2 AS VARCHAR(MAX)) 
                                    ELSE CAST(c.max_length AS VARCHAR(MAX)) 
                                END + ')'
                           WHEN st.name IN ('decimal', 'numeric') THEN st.name + '(' + CAST(c.precision AS VARCHAR(MAX)) + ',' + CAST(c.scale AS VARCHAR(MAX)) + ')'
                           WHEN st.name IN ('datetime2', 'datetimeoffset', 'time') THEN st.name + '(' + CAST(c.scale AS VARCHAR(MAX)) + ')'
                           WHEN st.name IN ('binary', 'varbinary') THEN st.name + '(' + CASE WHEN c.max_length = -1 THEN 'MAX' ELSE CAST(c.max_length AS VARCHAR(MAX)) END + ')'
                           WHEN st.name = 'xml' THEN st.name
                           ELSE st.name
                       END AS ColumnType,
                       CASE WHEN c.is_nullable = 1 THEN 'Yes' ELSE 'No' END AS IsNullable,
                       COALESCE(d.definition, '') AS ColumnDefault,
                       CASE WHEN ic.column_id IS NOT NULL THEN 'Yes' ELSE 'No' END AS IsPrimaryKey,
                       CASE WHEN c.is_identity = 1 THEN 'Yes' ELSE 'No' END AS IsIdentity,
                       COALESCE(ep.value, '') AS ColumnDescription,
                       c.column_id
                FROM sys.columns AS c
                INNER JOIN sys.views AS v ON v.object_id = c.object_id
                INNER JOIN sys.schemas AS s ON s.schema_id = v.schema_id
                LEFT JOIN sys.index_columns AS ic ON ic.object_id = v.object_id AND ic.column_id = c.column_id AND ic.index_id = 1
                LEFT JOIN sys.default_constraints AS d ON c.default_object_id = d.object_id
                LEFT JOIN sys.extended_properties AS ep ON ep.major_id = c.object_id AND ep.minor_id = c.column_id AND ep.class = 1
                LEFT JOIN sys.types AS st ON c.user_type_id = st.user_type_id
                ORDER BY TableName, column_id";

        private const string QueryIndexesSql = @"
                SELECT 
                TableName = t.name,
                IndexName = ind.name,
                IsPrimaryKey = CASE WHEN ind.is_primary_key = 1 THEN 'Yes' ELSE 'No' END,
                IsClustered = CASE WHEN ind.type_desc = 'CLUSTERED' THEN 'Yes' ELSE 'No' END,
                IsUnique = CASE WHEN ind.is_unique = 1 THEN 'Yes' ELSE 'No' END,
                IsForeignKey = 'No',
                Columns = STUFF((SELECT '\n' + COL_NAME(ic.object_id, ic.column_id) 
                                 FROM sys.index_columns ic 
                                 WHERE ind.object_id = ic.object_id AND ind.index_id = ic.index_id 
                                 ORDER BY ic.index_column_id 
                                 FOR XML PATH('')), 1, 2, ''),
                OtherColumns = STUFF((SELECT ',\n' + COL_NAME(inc.object_id, inc.column_id) 
                                      FROM sys.index_columns inc 
                                      WHERE ind.object_id = inc.object_id AND ind.index_id = inc.index_id AND inc.is_included_column = 1
                                      ORDER BY inc.key_ordinal 
                                      FOR XML PATH('')), 1, 2, '')
                FROM sys.indexes ind
                INNER JOIN sys.tables t ON ind.object_id = t.object_id
                WHERE t.is_ms_shipped = 0 AND ind.name IS NOT NULL
                UNION
                SELECT 
                    TableName = OBJECT_NAME(fkc.parent_object_id),
                    IndexName = (SELECT name FROM sys.foreign_keys WHERE object_id = fkc.constraint_object_id),
                    IsPrimaryKey = 'No',
                    IsClustered = 'No',
                    IsUnique = 'No',
                    IsForeignKey = 'Yes',
                    Columns = STUFF((SELECT '\n' + COL_NAME(fkc.parent_object_id, fkc.parent_column_id) 
                                     FROM sys.foreign_key_columns fkc1
                                     WHERE fkc1.constraint_object_id = fkc.constraint_object_id 
                                     ORDER BY fkc1.constraint_column_id 
                                     FOR XML PATH('')), 1, 2, ''),
                    OtherColumns = CONCAT(
                                        OBJECT_NAME(fkc.referenced_object_id), 
                                        ':\n', 
                                        STUFF((SELECT ',\n' + COL_NAME(fkc2.referenced_object_id, fkc2.referenced_column_id) 
                                               FROM sys.foreign_key_columns fkc2 
                                               WHERE fkc.constraint_object_id = fkc2.constraint_object_id 
                                               FOR XML PATH('')), 1, 3, '')
                                    )
                FROM sys.foreign_key_columns fkc
                INNER JOIN sys.tables t ON fkc.parent_object_id = t.object_id
                WHERE t.is_ms_shipped = 0
                ORDER BY TableName, IndexName";

        private readonly SchemaOptions schemaOptions;

        [ObservableProperty]
        private SchemaConnection connection;

        public ObservableCollection<SchemaConnection> Connections { get; }

        public ViewModel(IOptions<SchemaOptions> schemaAccessor) {
            ArgumentNullException.ThrowIfNull(schemaAccessor, nameof(schemaAccessor));

            schemaOptions = schemaAccessor.Value;

            Connections = new ObservableCollection<SchemaConnection>(schemaOptions.Connections);
        }

        [RelayCommand]
        private void Submit() {
            using DbConnection conn = new SqlConnection(Connection.ConnectionString);
            conn.Open();
            IEnumerable<TableInfo> tables = conn.Query<TableInfo>(QueryTablesSql);
            IEnumerable<ColumnInfo> columns = conn.Query<ColumnInfo>(QueryColumnsSql);
            IEnumerable<IndexInfo> indexes = conn.Query<IndexInfo>(QueryIndexesSql);

            ExporterBase exporter = SpreadsheetManager.CreateExporter();
            BuildTableListSheet(exporter, tables);
            BuildColumnListSheet(exporter, columns);
            BuildTableDetailSheets(exporter, tables, columns, indexes);
            string filePath = System.IO.Path.Combine(schemaOptions.ExportPath, $"TableSchema_{Connection.Name}{exporter.FileNameExtension}");
            exporter.ExportFile(filePath);

            MessageBox.Show($"檔案「{filePath}」產出成功");
        }

        private static void BuildTableListSheet(ExporterBase exporter, IEnumerable<TableInfo> tables) {
            CellStyle itemStyle = SpreadsheetManager.DefaultCellStyles.FieldStyle with {
                HorizontalAlignment = SpreadsheetExporter.HorizontalAlignment.Left
            };

            RecordSetTemplate<TableInfo> template = new(tables) {
                RecordHeight = Constants.AutoFiteRowHeight
            };
            template.Columns.Add("Schema", x => x.SchemaName);
            template.Columns.Add("名稱", x => x.TableName, fieldStyleGenerator: x => itemStyle);
            template.Columns.Add("類型", x => x.TableType, fieldStyleGenerator: x => itemStyle);
            template.Columns.Add("描述", x => x.TableDescription, fieldStyleGenerator: x => itemStyle);

            Sheeter sheeter = exporter.CreateSheeter("資料表清單");
            sheeter.AddTemplates(template);

            sheeter.SetColumnWidth(0, 10D);
            sheeter.SetColumnWidth(1, 40D);
            sheeter.SetColumnWidth(2, 15D);
            sheeter.SetColumnWidth(3, 50D);
        }

        private static void BuildColumnListSheet(ExporterBase exporter, IEnumerable<ColumnInfo> columns) {
            CellStyle itemStyle = SpreadsheetManager.DefaultCellStyles.FieldStyle with {
                HorizontalAlignment = SpreadsheetExporter.HorizontalAlignment.Left
            };
            CellStyle centerFieldStyle = SpreadsheetManager.DefaultCellStyles
                .FieldStyle with {
                HorizontalAlignment = SpreadsheetExporter.HorizontalAlignment.Center
            };

            RecordSetTemplate<ColumnInfo> template = new(columns) {
                RecordHeight = Constants.AutoFiteRowHeight
            };
            template.Columns.Add("資料表名稱", x => x.TableName);
            template.Columns.Add("欄位名稱", x => x.ColumnName);
            template.Columns.Add("欄位型別", x => x.ColumnType);
            template.Columns.Add("預設值", x => x.ColumnDefault);
            template.Columns.Add("是否允許 Null", x => x.IsNullable, fieldStyleGenerator: (x) => centerFieldStyle);
            template.Columns.Add("是否為 PK", x => x.IsPrimaryKey, fieldStyleGenerator: (x) => centerFieldStyle);
            template.Columns.Add("是否為 Identity", x => x.IsIdentity, fieldStyleGenerator: (x) => centerFieldStyle);
            template.Columns.Add("描述", x => x.ColumnDescription);

            Sheeter sheeter = exporter.CreateSheeter("資料表欄位清單");
            sheeter.AddTemplates(template);

            sheeter.SetColumnWidth(0, 40D);
            sheeter.SetColumnWidth(1, 40D);
            sheeter.SetColumnWidth(2, 30D);
            sheeter.SetColumnWidth(3, 15D);
            sheeter.SetColumnWidth(4, 15D);
            sheeter.SetColumnWidth(5, 15D);
            sheeter.SetColumnWidth(6, 15D);
            sheeter.SetColumnWidth(7, 50D);
        }

        private static void BuildTableDetailSheets(
            ExporterBase exporter, IEnumerable<TableInfo> tables,
            IEnumerable<ColumnInfo> columns, IEnumerable<IndexInfo> indexes) {
            foreach (TableInfo table in tables) {
                Sheeter sheeter = exporter.CreateSheeter(table.SheeterName);

                BuildTableDetailSheet(sheeter, table,
                    columns.Where(x => x.TableName == table.TableName),
                    indexes.Where(x => x.TableName == table.TableName)
                );
            }
        }

        private static void BuildTableDetailSheet(
            Sheeter sheeter, TableInfo table,
            IEnumerable<ColumnInfo> columns, IEnumerable<IndexInfo> indexes) {
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
                .CreateRow(Constants.AutoFiteRowHeight)
                .CreateCell("資料表描述：", cellStyle: headerLabelStyle)
                .CreateCell(table.TableDescription, 6);

            sheeter.AddTemplate(headerTemplate);

            CellStyle centerFieldStyle = SpreadsheetManager.DefaultCellStyles
                .FieldStyle with {
                HorizontalAlignment = SpreadsheetExporter.HorizontalAlignment.Center
            };

            RecordSetTemplate<ColumnInfo> columnsTemplate = new(columns);
            columnsTemplate.Columns.Add("欄位名稱", x => x.ColumnName);
            columnsTemplate.Columns.Add("欄位型別", x => x.ColumnType);
            columnsTemplate.Columns.Add("預設值", x => x.ColumnDefault);
            columnsTemplate.Columns.Add("是否允許 Null", x => x.IsNullable, fieldStyleGenerator: (x) => centerFieldStyle);
            columnsTemplate.Columns.Add("是否為 PK", x => x.IsPrimaryKey, fieldStyleGenerator: (x) => centerFieldStyle);
            columnsTemplate.Columns.Add("是否為 Identity", x => x.IsIdentity, fieldStyleGenerator: (x) => centerFieldStyle);
            columnsTemplate.Columns.Add("描述", x => x.ColumnDescription);

            sheeter.AddTemplate(columnsTemplate);

            if (indexes.Any()) {
                sheeter.AddTemplate(new GridTemplate().CreateRow());

                RecordSetTemplate<IndexInfo> indexesTemplate = new(indexes) {
                    RecordHeight = Constants.AutoFiteRowHeight
                };

                indexesTemplate.Columns.Add("索引名稱", x => x.IndexName);
                indexesTemplate.Columns.Add("是否為 PK", x => x.IsPrimaryKey, fieldStyleGenerator: (x) => centerFieldStyle);
                indexesTemplate.Columns.Add("是否為叢集索引", x => x.IsClustered, fieldStyleGenerator: (x) => centerFieldStyle);
                indexesTemplate.Columns.Add("是否為唯一索引", x => x.IsUnique, fieldStyleGenerator: (x) => centerFieldStyle);
                indexesTemplate.Columns.Add("是否為外鍵", x => x.IsForeignKey, fieldStyleGenerator: (x) => centerFieldStyle);
                indexesTemplate.Columns.Add("欄位", x => x.Columns, x => x.UseValue(x => x.Value?.Replace("\\n", Environment.NewLine)));
                indexesTemplate.Columns.Add("Include/外鍵 欄位", x => x.OtherColumns, x => x.UseValue(x => x.Value?.Replace("\\n", Environment.NewLine)));

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

        private class TableInfo {
            public string SchemaName { get; set; }

            public string TableName { get; set; }

            public string SheeterName => TableName.Length > 31
                    ? TableName[..31]
                    : TableName;

            public string TableType { get; set; }

            public string TableDescription { get; set; }
        }

        public class ColumnInfo {
            public string TableName { get; set; }

            public string ColumnName { get; set; }

            public string ColumnType { get; set; }

            public string IsNullable { get; set; }

            public string ColumnDefault { get; set; }

            public string IsPrimaryKey { get; set; }

            public string IsIdentity { get; set; }

            public string ColumnDescription { get; set; }
        }

        public class IndexInfo {
            public string TableName { get; set; }

            public string IndexName { get; set; }

            public string IsPrimaryKey { get; set; }

            public string IsClustered { get; set; }

            public string IsUnique { get; set; }

            public string IsForeignKey { get; set; }

            public string Columns { get; set; }

            public string OtherColumns { get; set; }
        }
    }
}
