# Provider Fixture 資料庫

## 目的

Provider fixture 資料庫提供可重現的 schema metadata，供 provider integration tests 驗證實際資料庫查詢行為。

Integration tests 位於 `tests/SchemaExporter.Core.IntegrationTests`，透過 [Testcontainers](tech-stack.md#testcontainers) 自動啟動資料庫 container，並套用 `tests/SchemaExporter.ProviderFixtures` 下的 schema scripts。測試不需要手動設定連線字串環境變數。

## 前置條件

- Docker Desktop 或其他 Docker API 相容的 container runtime。
- 第一次執行時需能從 registry 拉取測試映像檔。

## Fixture 內容

SQL Server fixture 使用 `mcr.microsoft.com/mssql/server:2025-CU4-GDR1-ubuntu-24.04`，並套用 `tests/SchemaExporter.ProviderFixtures/sqlserver/schema.sql`。

Fixture 會建立以下資料庫：

- `SchemaExporterFixture`

Fixture schema 會建立以下測試物件：

- `dbo.SE_Customers`
- `dbo.SE_Orders`
- `dbo.SE_ActiveCustomers`
- `dbo.usp_SE_GetCustomers`
- `dbo.ufn_SE_CustomerDisplayName`

Oracle fixture 使用 `gvenzl/oracle-xe:21.3.0-slim-faststart`，並套用 `tests/SchemaExporter.ProviderFixtures/oracle/schema.sql`。

Oracle fixture schema 會建立以下測試物件：

- `SE_CUSTOMERS`
- `SE_ORDERS`
- `SE_ACTIVE_CUSTOMERS`
- `SE_GET_CUSTOMERS`
- `SE_CUSTOMER_DISPLAY_NAME`
- `SE_CUSTOMER_PACKAGE`

## 執行方式

執行一般測試時，provider integration tests 會因 `[Explicit]` 保持 skipped，不會啟動 container：

```powershell
dotnet test .\SchemaExporter.slnx -v minimal
```

執行全部 provider integration tests：

```powershell
dotnet test .\tests\SchemaExporter.Core.IntegrationTests\SchemaExporter.Core.IntegrationTests.csproj --filter "TestCategory=Integration" -v minimal
```

只執行 SQL Server provider integration tests：

```powershell
dotnet test .\tests\SchemaExporter.Core.IntegrationTests\SchemaExporter.Core.IntegrationTests.csproj --filter "TestCategory=SqlServer" -v minimal
```

只執行 Oracle provider integration tests：

```powershell
dotnet test .\tests\SchemaExporter.Core.IntegrationTests\SchemaExporter.Core.IntegrationTests.csproj --filter "TestCategory=Oracle" -v minimal
```

## 生命週期

Testcontainers 會為每次測試建立暫時性 container，測試結束後自動移除。SQL Server fixture 會使用隨機 host port，避免與本機既有 SQL Server 或其他測試執行互相衝突。

Oracle fixture image 較大，首次執行時間主要取決於 Docker registry 下載速度與本機資源。
