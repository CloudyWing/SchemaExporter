# Schema Diff 格式

## 說明

Schema Diff 記錄兩份 schema snapshot 之間的差異，包含新增、移除、修改的物件、欄位、索引與程序。

Diff 可在匯出時自動產生（指定基準 snapshot），也可以單獨使用 `diff` 命令對任意兩份 snapshot 進行比對。

## 啟用方式

**方式一：匯出時自動產生**

在 `appsettings.json` 設定基準 snapshot 路徑：

```json
"ExportResultOptions": {
  "GenerateSchemaSnapshot": true,
  "DiffSourceSnapshotPath": "D:\\Snapshots\\baseline.snapshot.json"
}
```

或 CLI 傳入 `--snapshot --diff-from <path>` 參數。

**方式二：單獨比對**

使用 `diff` 命令直接比對兩份 snapshot：

```bash
schemaexporter diff --left baseline.snapshot.json --right current.snapshot.json --output diff.json
```

## 輸出格式

Diff 支援兩種輸出格式：

- **JSON**：結構化格式，適合程式處理。副檔名 `.json`。
- **Markdown**：人類可讀格式，適合直接閱讀或貼入文件。副檔名 `.md`。

## 檔案命名（匯出時自動產生）

與主要 Excel 輸出檔案同名，後綴為 `.diff.json` 或 `.diff.md`。

## JSON 結構

```json
{
  "schemaVersion": 2,
  "generatedAt": "2024-12-01T12:00:00+08:00",
  "leftSnapshotPath": "D:\\Snapshots\\baseline.snapshot.json",
  "rightSnapshotPath": "D:\\SchemaExports\\TableSchema_prod_20241201_120000.snapshot.json",
  "summary": { ... },
  "objectChanges": [ ... ],
  "columnChanges": [ ... ],
  "indexChanges": [ ... ],
  "routineChanges": [ ... ]
}
```

## 欄位說明

### 根層級

| 欄位 | 說明 |
| --- | --- |
| `schemaVersion` | Diff 格式版本號。 |
| `generatedAt` | 差異比對產生的時間，ISO 8601 格式含時區偏移。 |
| `leftSnapshotPath` | 基準（左側）snapshot 的來源路徑。 |
| `rightSnapshotPath` | 目前（右側）snapshot 的來源路徑。 |

### summary

各層級物件的變更統計：

| 欄位 | 說明 |
| --- | --- |
| `addedObjects` | 新增的資料表/檢視表數量。 |
| `removedObjects` | 移除的資料表/檢視表數量。 |
| `modifiedObjects` | 修改的資料表/檢視表數量（如描述變更）。 |
| `addedColumns` | 新增的欄位數量。 |
| `removedColumns` | 移除的欄位數量。 |
| `modifiedColumns` | 修改的欄位數量。 |
| `addedIndexes` | 新增的索引數量。 |
| `removedIndexes` | 移除的索引數量。 |
| `modifiedIndexes` | 修改的索引數量。 |
| `addedRoutines` | 新增的程序/函數數量。 |
| `removedRoutines` | 移除的程序/函數數量。 |
| `modifiedRoutines` | 修改的程序/函數數量。 |

### objectChanges / columnChanges / indexChanges / routineChanges

各層級的差異項目陣列，每筆結構相同：

| 欄位 | 說明 |
| --- | --- |
| `changeType` | 變更類型：`Added`、`Removed` 或 `Modified`。 |
| `identifier` | 識別此項目的字串，格式因層級而異（如物件為 `dbo.Users`，欄位為 `dbo.Users.Id`）。 |
| `propertyChanges` | 屬性變更的字典（僅 `Modified` 項目有值，其他為空）。每個 key 為屬性名稱，value 包含 `previous` 與 `current` 兩個欄位。 |

### propertyChanges 範例

```json
"propertyChanges": {
  "ColumnType": {
    "previous": "nvarchar(50)",
    "current": "nvarchar(100)"
  },
  "IsNullable": {
    "previous": "NO",
    "current": "YES"
  }
}
```
