# CI 使用範例

## 目的

CI 可使用 SchemaExporter CLI 產生 schema snapshot、Schema Summary 與 diff artifact，讓 pull request 能檢查資料庫 schema drift。

常見流程如下：

1. 從受控測試資料庫匯出目前 schema snapshot。
2. 與版控中的 baseline snapshot 產生 diff JSON。
3. 將 workbook、snapshot、Schema Summary 與 diff 報告上傳為 CI artifact。
4. 若 diff summary 內有任何變更，讓 workflow 失敗，要求審查者確認是否要更新 baseline。

## 前置條件

- CI runner 可連線到測試資料庫。
- Repository 內保留一份 baseline snapshot，例如 `schema-baseline/baseline.snapshot.json`。
- 連線字串存放於 CI secret，不寫入 repository。
- `Schema.Redaction.Enabled` 依資料庫內容決定是否啟用。

## GitHub Actions 範例

以下 workflow 示範以 Windows runner 執行 build、test、schema export 與 schema diff。

```yaml
name: Schema Drift

on:
  pull_request:
    paths:
      - "src/**"
      - "schema-baseline/**"
      - ".github/workflows/schema-drift.yml"

permissions:
  contents: read

jobs:
  schema-drift:
    runs-on: windows-latest

    env:
      SCHEMAEXPORTER_CONNECTION_STRING: ${{ secrets.SCHEMAEXPORTER_CONNECTION_STRING }}

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - name: Restore
        run: dotnet restore .\SchemaExporter.slnx

      - name: Build
        run: dotnet build .\SchemaExporter.slnx -c Release --no-restore

      - name: Test
        run: dotnet test .\SchemaExporter.slnx -c Release --no-build --verbosity minimal

      - name: Prepare SchemaExporter settings
        shell: pwsh
        run: |
          $configDir = Join-Path $env:LOCALAPPDATA "SchemaExporter"
          $outputDir = Join-Path $env:GITHUB_WORKSPACE "artifacts\schema"
          New-Item -ItemType Directory -Path $configDir -Force | Out-Null
          New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

          $settings = @{
            Schema = @{
              ExportPath = $outputDir
              Connections = @(
                @{
                  Name = "ci"
                  DatabaseType = "SqlServer"
                  ConnectionString = $env:SCHEMAEXPORTER_CONNECTION_STRING
                  ExportProfileName = "Default"
                }
              )
              ExportProfiles = @(
                @{
                  Name = "Default"
                  IncludeSchemas = @()
                  ExcludeSchemas = @()
                  IncludeObjects = @()
                  ExcludeObjects = @()
                  IncludeViews = $true
                }
              )
              ExportResultOptions = @{
                UseTimestamp = $false
                TimestampFormat = "yyyyMMdd_HHmmss"
                OverwriteStrategy = "Overwrite"
                OpenOutputFolder = $false
                GenerateManifest = $true
                GenerateJsonSidecar = $true
                GenerateMarkdownSidecar = $true
                GenerateSchemaSummary = $true
                GenerateSchemaSnapshot = $true
                DiffSourceSnapshotPath = $null
              }
              Redaction = @{
                Enabled = $true
                ReplacementText = "[REDACTED]"
                SensitiveNamePatterns = @(
                  "password",
                  "passwd",
                  "pwd",
                  "secret",
                  "token",
                  "api[_-]?key",
                  "credential",
                  "private[_-]?key",
                  "connection[_-]?string",
                  "salt"
                )
                SensitiveTextPatterns = @()
              }
            }
          }

          $settingsPath = Join-Path $configDir "appsettings.json"
          $settings | ConvertTo-Json -Depth 20 | Set-Content -Path $settingsPath -Encoding utf8

      - name: Export schema snapshot
        run: >
          dotnet run --project .\src\SchemaExporter\SchemaExporter.csproj -c Release --
          export --connection ci --profile Default --output .\artifacts\schema
          --manifest --json-sidecar --markdown-sidecar --schema-summary --snapshot
          --no-timestamp --no-open-output-folder

      - name: Compare schema baseline
        shell: pwsh
        run: |
          dotnet run --project .\src\SchemaExporter\SchemaExporter.csproj -c Release -- `
            diff `
            --left .\schema-baseline\baseline.snapshot.json `
            --right .\artifacts\schema\TableSchema_ci.snapshot.json `
            --output .\artifacts\schema\schema.diff.json `
            --format json

          dotnet run --project .\src\SchemaExporter\SchemaExporter.csproj -c Release -- `
            diff `
            --left .\schema-baseline\baseline.snapshot.json `
            --right .\artifacts\schema\TableSchema_ci.snapshot.json `
            --output .\artifacts\schema\schema.diff.md `
            --format markdown

          $diff = Get-Content .\artifacts\schema\schema.diff.json -Raw | ConvertFrom-Json
          $summary = $diff.summary
          $changeCount =
            $summary.addedObjects +
            $summary.removedObjects +
            $summary.modifiedObjects +
            $summary.addedColumns +
            $summary.removedColumns +
            $summary.modifiedColumns +
            $summary.addedIndexes +
            $summary.removedIndexes +
            $summary.modifiedIndexes +
            $summary.addedRoutines +
            $summary.removedRoutines +
            $summary.modifiedRoutines

          if ($changeCount -gt 0) {
            throw "Schema drift detected. Review artifacts/schema/schema.diff.md and update baseline if the change is expected."
          }

      - name: Upload schema artifacts
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: schema-artifacts
          path: artifacts/schema/*
```

## Baseline 更新流程

當 schema 變更是預期行為時，將 CI 產生的 `.snapshot.json` 取代 repository 內的 baseline snapshot，並和資料庫 migration 或 schema 變更一併提交。

若只需要人工審查，不希望 workflow 因 drift 失敗，可移除 `Compare schema baseline` 步驟中的 `throw`，保留 artifact 上傳即可。
