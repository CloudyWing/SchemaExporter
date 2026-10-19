# 註解 SQL 產生

## 說明

`comment-sql` 命令依 schema snapshot 與一份註解清單，產生為資料表、檢視表與欄位設定註解的 `.sql` 檔。命令只讀取兩個輸入檔並寫出一個 SQL 檔，不連線資料庫、不執行 SQL，也不對資料庫下任何查詢。

資料庫現有的註解取自 snapshot。清單中的註解與現有註解相同者略過，現有註解為空者列入新增段，現有註解不同者列入差異段，並在每筆語句上方附上資料庫既有值。使用者檢視 SQL 檔之後，再自行以 `sqlcmd`、SSMS 或 SQL*Plus 執行。

範圍限於資料表、檢視表與欄位，不處理 routine。

## 命令

```bash
schemaexporter comment-sql --snapshot <path> --comments <path> --output <path>
```

| 參數 | 說明 |
| --- | --- |
| `--snapshot <path>` | 必要。由 `export --snapshot` 產生的 snapshot 檔案。提供物件與欄位名稱、資料庫類型與現有註解。 |
| `--comments <path>` | 必要。註解清單 JSON 檔案，格式見下節。 |
| `--output <path>` | 必要。要寫入的 `.sql` 檔案路徑，父目錄不存在時會建立，既有檔案會被覆寫。 |
| `--help` / `-h` / `/?` | 顯示說明文字。 |

資料庫類型由 snapshot 的 `databaseType` 決定，命令不需要也不接受連線名稱與連線字串。

## 註解清單格式

註解清單是 UTF-8 編碼的 JSON 陣列（可含 BOM），每個元素有四個欄位，欄位名稱不分大小寫，出現未定義的欄位會視為格式錯誤。

| 欄位 | 說明 |
| --- | --- |
| `schema` | 必要。SQL Server 的 schema，Oracle 的 owner。 |
| `object` | 必要。資料表或檢視表名稱。 |
| `column` | 欄位名稱。資料表或檢視表層級的註解省略此欄位，或填入 `null` 或空字串。 |
| `comment` | 必要。註解內容。 |

```json
[
  { "schema": "dbo", "object": "Users", "column": "", "comment": "使用者資料表" },
  { "schema": "dbo", "object": "Users", "column": "Name", "comment": "顯示名稱" },
  { "schema": "dbo", "object": "Users", "column": "Note", "comment": "第一行\n第二行 it's fine" }
]
```

名稱先以大小寫完全相符的方式對應 snapshot，找不到時改用不分大小寫比對，唯一相符就採用 snapshot 內的寫法，有多個相符則回報錯誤。同一個目標在清單中只能出現一次。

## 輸出內容

SQL 檔分成新增段與差異段。兩段都可能為空，空段落會輸出 `-- (none)`。

| 項目 | 規則 |
| --- | --- |
| 新增段 | snapshot 的現有註解為空字串。SQL Server 與 Oracle 的 snapshot 都以空字串表示沒有註解。 |
| 差異段 | 現有註解不為空且與清單不同。每筆語句上方以 `-- existing: "..."` 附上既有值，值以 JSON 風格轉義，換行與 Tab 會顯示為 `\n` 與 `\t`。 |
| 只含空白的現有註解 | 視為已有註解，列入差異段，標示為 `-- existing (whitespace-only): "   "`。 |
| 相同 | 清單註解與現有註解逐字相同時不輸出語句，只計入檔頭的 `Unchanged` 筆數。 |
| 空字串註解 | 清單中 `comment` 為空字串的項目一律略過，只計入檔頭的 `Empty comment` 筆數。命令不會清除任何現有註解。 |
| 排序 | 每段依 schema、object、column 以序數比較排序，資料表層級的註解排在其欄位之前。清單的原始順序不影響輸出。 |
| 確定性 | 相同的 snapshot 與清單產生逐位元組相同的檔案，檔案內沒有時間戳記或路徑。 |
| 換行與編碼 | 檔案換行固定為 LF。SQL Server 檔案為 UTF-8 含 BOM，Oracle 檔案為 UTF-8 不含 BOM。 |

註解內容不會被修剪。只含空白、前後有空白、含 Tab 的註解都逐字寫入。

### SQL Server

使用 `sp_addextendedproperty` 與 `sp_updateextendedproperty` 設定 `MS_Description`。每筆語句先以 `fn_listextendedproperty` 在執行時檢查屬性是否存在，存在就更新，不存在就新增。Snapshot 無法區分「沒有屬性」與「屬性值為空字串」，單純呼叫新增程序會在後者失敗，因此新增段與差異段使用同一種寫法。

```sql
DECLARE @value nvarchar(4000) = N'顯示名稱';
IF EXISTS (SELECT 1 FROM sys.fn_listextendedproperty(N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Users', N'COLUMN', N'Name'))
    EXEC sys.sp_updateextendedproperty @name = N'MS_Description', @value = @value, @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'Name';
ELSE
    EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = @value, @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Users', @level2type = N'COLUMN', @level2name = N'Name';
GO
```

- 註解值一律使用 `N'...'` 字面值，單引號加倍。名稱參數的單引號同樣加倍。
- 換行以 `NCHAR(13)` 與 `NCHAR(10)` 串接，不在字面值內輸出實際換行，因此註解中單獨一行 `GO` 不會切斷批次。
- 註解中的 `$(` 以 `NCHAR(36)` 取代 `$`，避免 sqlcmd 把它當成變數替換。
- 長度上限為 3,750 個 UTF-16 字元（7,500 位元組）。超出時回報錯誤，不截斷。
- 檢視表使用 `N'VIEW'` 作為 level 1 類型。

### Oracle

使用 `COMMENT ON TABLE` 與 `COMMENT ON COLUMN`，檢視表同樣使用 `COMMENT ON TABLE`。識別字一律加雙引號以保留大小寫。檔案開頭輸出 `SET DEFINE OFF` 與 `SET SQLBLANKLINES ON`。

```sql
SET DEFINE OFF
SET SQLBLANKLINES ON
COMMENT ON COLUMN "APP"."USERS"."NAME" IS '顯示名稱';
```

註解不含換行且語句不超過 2,000 位元組時，輸出單行 `COMMENT ON`。其他情況改用 PL/SQL 區塊，以 `CHR(13)` 與 `CHR(10)` 串接換行，並以 `EXECUTE IMMEDIATE` 執行相同的 `COMMENT ON`。原因是 SQL*Plus 會把字串內以 `/`、`.`、`#`、`@` 開頭的行當成指令，單行超過約 3,000 字元也會失敗，這兩種情況都無法在 `COMMENT ON` 字面值內迴避。

- 長度上限為 4,000 位元組，以 UTF-8（AL32UTF8）計算。超出時回報錯誤，不截斷。
- Oracle 把空字串視為 NULL。清單中的空字串註解被略過，snapshot 中的 NULL 註解以空字串表示並列入新增段。
- 識別字不可包含雙引號，遇到時回報錯誤。
- 以 SQL*Plus 執行時，需設定 `NLS_LANG=AMERICAN_AMERICA.AL32UTF8`，中文與 emoji 才會正確寫入。

## 結束代碼與錯誤訊息

| 代碼 | 說明 |
| --- | --- |
| `0` | 成功，已寫入 SQL 檔。 |
| `1` | 參數錯誤，例如缺少必要參數或出現未知參數。訊息格式為 `--snapshot is required.`、`--comments is required.`、`--output is required.`、`Missing value for --<name>.`、`Unknown argument: <argument>`。 |
| `2` | 輸入檔無法讀取或格式錯誤，或清單有無法產生 SQL 的項目。訊息格式為 `Comment SQL failed: <原因>`，輸出至 stderr，不會建立或修改輸出檔。 |
| `3` | 未預期的錯誤。 |

清單有問題時，訊息會逐項列出全部問題，每項標示第幾筆與目標，例如：

```text
Comment SQL failed: 註解清單有 2 個問題，未產生 SQL：
- 第 1 筆（dbo.Missing）：Snapshot 找不到物件 dbo.Missing
- 第 3 筆（dbo.Users.Name）：註解長度 3751 個 UTF-16 字元，超過 SQL Server 擴充屬性值上限 3750 個字元（7,500 位元組）
```

會回報錯誤的情況包含下列項目。

- 找不到物件或欄位，或只差大小寫的物件有多個。
- 物件類型不是資料表或檢視表。
- 同一目標重複出現，或缺少 `comment` 欄位。
- 註解超出長度上限。
- 註解含有 Tab、CR、LF 以外的控制字元、行分隔或段落分隔字元，或含有未成對的 surrogate。
- 名稱含控制字元，或 SQL Server 名稱含 `$(`，或 Oracle 名稱含雙引號。

成功時輸出至 stdout 的內容如下。

```text
Comment SQL written: D:\work\comments.sql
Database type: SqlServer
Add: 12, Update: 3, Unchanged (skipped): 40, Empty comment (skipped): 0
```

Snapshot 套用過敏感 metadata 遮罩時，其中的現有註解可能已被取代，命令會多輸出一行 `Warning: ...`，新增與差異的判定可能不準確。

## 已知限制

- 容器內的整合測試涵蓋 `sqlcmd` 與 SQL*Plus（Linux）。SSMS 與 Windows 版 SQL*Plus 沒有實機驗證。
- Oracle 的 4,000 位元組上限依 AL32UTF8 計算。資料庫字元集不同時，位元組數可能不同，超出時由 Oracle 在執行階段回報錯誤。
- 差異判定只比對 snapshot 當下的值。Snapshot 產生後資料庫又被修改，SQL 仍會依語句內容覆寫。
