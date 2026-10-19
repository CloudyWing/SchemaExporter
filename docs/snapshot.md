# Schema Snapshot 格式

## 說明

Schema Snapshot 是一份完整記錄資料庫結構的 JSON 檔案，用途是保存某個時間點的 schema 狀態，供後續差異比對使用。Snapshot v3 在根層級保存每個外鍵欄位配對。

Snapshot 本身不依賴資料庫連線即可使用，是 diff 命令的輸入來源。

## 啟用方式

在 `appsettings.json` 設定：

```json
"ExportResultOptions": {
  "GenerateSchemaSnapshot": true
}
```

或 CLI 傳入 `--snapshot` 參數。

## 檔案命名

與主要 Excel 輸出檔案同名，後綴為 `.snapshot.json`。例如：

```text
TableSchema_prod_20241201_120000.xlsx
TableSchema_prod_20241201_120000.snapshot.json
```

## JSON 結構

```json
{
  "schemaVersion": 3,
  "exportedAt": "2024-12-01T12:00:00+08:00",
  "connectionName": "prod",
  "databaseType": "SqlServer",
  "profileName": "Default",
  "outputFilePath": "...",
  "counts": { "objects": 1, "columns": 1, "indexes": 0, "routines": 0, "relationships": 0 },
  "diagnostics": [ ... ],
  "objects": [ ... ],
  "routines": [ ... ],
  "relationships": [ ... ]
}
```

## 欄位說明

### 根層級

| 欄位 | 說明 |
| --- | --- |
| `schemaVersion` | Snapshot 格式版本號，用於相容性判斷。 |
| `exportedAt` | 建立時間，ISO 8601 格式含時區偏移。 |
| `connectionName` | 產生此 snapshot 的連線名稱。 |
| `databaseType` | 資料庫類型（`SqlServer` 或 `Oracle`）。 |
| `profileName` | 使用的匯出設定檔名稱。 |
| `outputFilePath` | 對應的 Excel 輸出檔案路徑。 |

### counts

| 欄位 | 說明 |
| --- | --- |
| `objects` | 資料表與檢視表總數。 |
| `columns` | 欄位總數。 |
| `indexes` | 索引總數。 |
| `routines` | 預存程序與函數總數。 |
| `relationships` | 外鍵欄位配對總數。 |

### diagnostics

產生此 snapshot 時的診斷項目陣列，結構同 [manifest.md](manifest.md) 的 `diagnostics` 欄位。

### objects

資料表與檢視表的陣列，每筆包含：

| 欄位 | 說明 |
| --- | --- |
| `schemaName` | 結構描述名稱（如 `dbo`）。 |
| `objectName` | 物件名稱（如 `Users`）。 |
| `objectType` | 物件類型（如 `TABLE`、`VIEW`）。 |
| `objectDescription` | 物件描述（來自資料庫 extended properties，若無則為空字串）。 |
| `columns` | 欄位陣列，詳見下方。 |
| `indexes` | 索引陣列，詳見下方。 |

#### columns

| 欄位 | 說明 |
| --- | --- |
| `columnName` | 欄位名稱。 |
| `columnType` | 資料型別（如 `nvarchar(50)`、`int`）。 |
| `isNullable` | 是否允許 NULL（`"YES"` 或 `"NO"`）。 |
| `columnDefault` | 欄位預設值（若無則為空字串）。 |
| `isPrimaryKey` | 是否為主鍵（`"YES"` 或 `"NO"`）。 |
| `isIdentity` | 是否為識別欄位（`"YES"` 或 `"NO"`）。 |
| `columnDescription` | 欄位描述（來自資料庫 extended properties，若無則為空字串）。 |
| `columnOrder` | 欄位在資料表中的排列順序（從 1 開始）。 |

#### indexes

| 欄位 | 說明 |
| --- | --- |
| `indexName` | 索引名稱。 |
| `isPrimaryKey` | 是否為主鍵索引。 |
| `isClustered` | 是否為叢集索引。 |
| `isUnique` | 是否為唯一索引。 |
| `columns` | 此索引包含的索引鍵欄位名稱陣列。 |
| `includedColumns` | 包含欄位（INCLUDE 欄位）名稱陣列（SQL Server 非叢集索引適用）。 |

### relationships

根層級的外鍵欄位配對陣列，每筆包含：

| 欄位 | 說明 |
| --- | --- |
| `foreignKeyName` | 外鍵約束名稱。 |
| `sourceSchemaName` | 來源結構描述名稱。 |
| `sourceTableName` | 來源資料表名稱。 |
| `sourceColumnName` | 來源資料行名稱。 |
| `referencedSchemaName` | 參考結構描述名稱。 |
| `referencedTableName` | 參考資料表名稱。 |
| `referencedColumnName` | 參考資料行名稱。 |
| `columnOrder` | 複合外鍵中的欄位順序。 |

### v2 相容性

讀取 Snapshot v2 時，缺少的 `relationships` 會視為空集合。v2 索引中的 `otherColumns` 會映射至 v3 的 `includedColumns`，歷史 `isForeignKey` 欄位不會轉換為關聯資料。v2 與 v3 比對時保留物件、欄位、索引與 routine 差異，關聯差異集合保持空白。

### routines

預存程序與函數的陣列，每筆包含：

| 欄位 | 說明 |
| --- | --- |
| `schemaName` | 結構描述名稱。 |
| `containerName` | 所屬套件或類別名稱（Oracle Package 適用，SQL Server 為空字串）。 |
| `routineName` | 程序或函數名稱。 |
| `routineType` | 類型（如 `PROCEDURE`、`FUNCTION`）。 |
| `overloadIdentifier` | 多載識別字（Oracle 適用，SQL Server 為空字串）。 |
| `parameterSignature` | 參數簽章的文字描述。 |
| `returnType` | 回傳型別（函數適用，程序為空字串）。 |
| `routineDescription` | 程序或函數描述（來自資料庫 extended properties，若無則為空字串）。 |
| `routineDefinition` | 程序或函數定義的原始 SQL 文字（若資料庫支援）。 |
