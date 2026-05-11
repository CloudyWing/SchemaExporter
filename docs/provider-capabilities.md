# Provider Capability Matrix

## 說明

Provider capability matrix 描述各資料庫 provider 對 schema metadata 的支援範圍。此矩陣用於說明輸出限制，也會出現在 Schema Summary 檔案中，協助讀者判斷哪些資訊可直接信任、哪些資訊可能不完整。

## SQL Server

| 項目 | 支援層級 | 說明 |
| --- | --- | --- |
| Tables | `Full` | 匯出使用者資料表名稱、schema 與 `MS_Description` 註解。 |
| Views | `Partial` | 匯出 view 物件與欄位；不匯出 view SQL、相依性與索引明細。 |
| Columns | `Full` | 匯出型別、nullability、default、primary key、identity 與 `MS_Description` 註解。 |
| Indexes | `Full` | 匯出資料表索引、唯一性、clustered、included columns、primary key 與 foreign key。 |
| Routines | `Partial` | 匯出 procedures 與 functions 的 signature；當 `sys.sql_modules` 可讀時匯出定義。 |

## Oracle

| 項目 | 支援層級 | 說明 |
| --- | --- | --- |
| Tables | `Full` | 匯出使用者資料表名稱、owner 與 comments。 |
| Views | `Partial` | 匯出 view 物件與欄位；不匯出 view SQL、相依性與索引明細。 |
| Columns | `Partial` | 匯出型別、nullability、primary key、可用時的 identity 與 comments；不匯出 default。 |
| Indexes | `Partial` | 匯出資料表索引、唯一性、primary key 與 foreign key；省略 generated indexes。 |
| Routines | `Partial` | 匯出 procedures、functions 與 package routines；description 為空，definition 可能因版本或權限限制而缺漏。 |

## 支援層級

| 值 | 說明 |
| --- | --- |
| `Full` | 目前 provider 可穩定匯出該項 metadata。 |
| `Partial` | 目前 provider 只匯出部分 metadata，或受資料庫版本、權限、物件型態限制。 |
| `Unsupported` | 目前 provider 不匯出該項 metadata。 |
