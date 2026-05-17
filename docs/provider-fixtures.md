# Provider Fixture 資料庫

## 目的

Provider fixture 資料庫提供可重現的 schema metadata，供 SQL Server provider integration tests 驗證實際資料庫查詢行為。

目前固定支援 SQL Server fixture。Oracle fixture SQL 保留在 repository 中，但尚未接入 Docker Compose。

## SQL Server fixture

SQL Server fixture 使用 Docker Compose 啟動 SQL Server 2025 container，並套用 `tests/SchemaExporter.ProviderFixtures/sqlserver/schema.sql`。

Fixture 會建立以下資料庫：

- `SchemaExporterFixture`

Fixture schema 會建立以下測試物件：

- `dbo.SE_Customers`
- `dbo.SE_Orders`
- `dbo.SE_ActiveCustomers`
- `dbo.usp_SE_GetCustomers`
- `dbo.ufn_SE_CustomerDisplayName`

## 啟動方式

先設定本機測試密碼。此密碼只用於本機 fixture，不應作為正式環境密碼，且需符合 SQL Server 密碼複雜度規則。

```powershell
$env:SCHEMAEXPORTER_SQLSERVER_PASSWORD = Read-Host "SQL Server fixture password"
```

啟動 SQL Server container 並套用 schema：

```powershell
docker compose -f .\tests\SchemaExporter.ProviderFixtures\compose.yml up -d sqlserver
docker compose -f .\tests\SchemaExporter.ProviderFixtures\compose.yml up --force-recreate sqlserver-init
```

第二個指令會以前景執行 init container，方便直接確認 schema 套用是否成功。

若本機 `11433` port 已被使用，可改用其他 port：

```powershell
$env:SCHEMAEXPORTER_SQLSERVER_PORT = "21433"
docker compose -f .\tests\SchemaExporter.ProviderFixtures\compose.yml up -d sqlserver
docker compose -f .\tests\SchemaExporter.ProviderFixtures\compose.yml up --force-recreate sqlserver-init
```

## 執行 provider integration tests

設定測試連線字串：

```powershell
$env:SCHEMAEXPORTER_SQLSERVER_TEST_CONNECTION = "Server=localhost,11433;Database=SchemaExporterFixture;User Id=sa;Password=$env:SCHEMAEXPORTER_SQLSERVER_PASSWORD;Encrypt=True;TrustServerCertificate=True"
```

若使用自訂 port，連線字串需同步調整。

只執行 provider integration tests：

```powershell
dotnet test .\tests\SchemaExporter.Core.Tests\SchemaExporter.Core.Tests.csproj --filter "FullyQualifiedName~ProviderIntegrationTests" -v minimal
```

執行完整測試：

```powershell
dotnet test .\SchemaExporter.slnx -v minimal
```

若未設定 `SCHEMAEXPORTER_SQLSERVER_TEST_CONNECTION`，SQL Server provider integration test 會標示為 skipped，不影響一般測試。

## 重建 fixture

重新套用 schema：

```powershell
docker compose -f .\tests\SchemaExporter.ProviderFixtures\compose.yml up --force-recreate sqlserver-init
```

停止並移除 fixture container：

```powershell
docker compose -f .\tests\SchemaExporter.ProviderFixtures\compose.yml down
```

## Oracle fixture 狀態

Oracle fixture SQL 位於 `tests/SchemaExporter.ProviderFixtures/oracle/schema.sql`。目前 Oracle provider integration test 仍透過 `SCHEMAEXPORTER_ORACLE_TEST_CONNECTION` 連接既有測試資料庫，不由 Compose 啟動。
