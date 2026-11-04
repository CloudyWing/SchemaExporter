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
- 連線字串存放於 CI secret，不寫入 repository。設定檔的 `ConnectionString` 使用 `${env:SCHEMAEXPORTER_CONNECTION_STRING}` 參照，CLI 匯出時才讀取環境變數，連線字串不會寫入 runner 上的設定檔。
- `Schema.Redaction.Enabled` 依資料庫內容決定是否啟用。

## GitHub Actions 範例

以下 workflow 示範以 Windows runner 執行 build、test、schema export 與 schema diff。CLI 是獨立的 Console 專案 `src/SchemaExporter.Cli`，以 `dotnet run` 執行時等同呼叫 `CloudyWing.SchemaExporter.Cli.exe`。

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
                  # 只寫入環境變數參照，實際連線字串在匯出時才從 secret 讀取，不會落地到設定檔。
                  ConnectionString = '${env:SCHEMAEXPORTER_CONNECTION_STRING}'
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
          dotnet run --project .\src\SchemaExporter.Cli\SchemaExporter.Cli.csproj -c Release --
          export --connection ci --profile Default --output .\artifacts\schema
          --manifest --json-sidecar --markdown-sidecar --schema-summary --snapshot
          --no-timestamp --no-open-output-folder

      - name: Compare schema baseline
        shell: pwsh
        run: |
          dotnet run --project .\src\SchemaExporter.Cli\SchemaExporter.Cli.csproj -c Release -- `
            diff `
            --left .\schema-baseline\baseline.snapshot.json `
            --right .\artifacts\schema\TableSchema_ci.snapshot.json `
            --output .\artifacts\schema\schema.diff.json `
            --format json

          dotnet run --project .\src\SchemaExporter.Cli\SchemaExporter.Cli.csproj -c Release -- `
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
            $summary.addedRelationships +
            $summary.removedRelationships +
            $summary.modifiedRelationships +
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

## 使用 Release 的 CLI 執行檔

不想在 CI 內建置原始碼時，可改用 GitHub Release 的 `CloudyWing.SchemaExporter.Cli-<tag>-win-x64.zip`。ZIP 內是 self-contained single-file 的 `CloudyWing.SchemaExporter.Cli.exe` 與出廠範本 `appsettings.json`，runner 不需要安裝 .NET runtime。

以下步驟可取代上方範例的 `Setup .NET`、`Restore`、`Build`、`Test` 與 `dotnet run`，`Prepare SchemaExporter settings` 步驟維持不變：

```yaml
      - name: Download SchemaExporter CLI
        shell: pwsh
        env:
          GH_TOKEN: ${{ github.token }}
        run: |
          $tag = "v1.0.0"
          gh release download $tag --repo CloudyWing/SchemaExporter --pattern "CloudyWing.SchemaExporter.Cli-$tag-win-x64.zip" --dir .\tools
          Expand-Archive -Path ".\tools\CloudyWing.SchemaExporter.Cli-$tag-win-x64.zip" -DestinationPath .\tools\schemaexporter -Force

      - name: Export schema snapshot
        run: >
          .\tools\schemaexporter\CloudyWing.SchemaExporter.Cli.exe
          export --connection ci --profile Default --output .\artifacts\schema
          --manifest --json-sidecar --markdown-sidecar --schema-summary --snapshot
          --no-timestamp --no-open-output-folder
```

`diff` 命令同樣改為呼叫 `.\tools\schemaexporter\CloudyWing.SchemaExporter.Cli.exe diff ...`。本機已安裝 Desktop 時，CLI 也位於安裝目錄的 `tools\` 子目錄，例如 `%LocalAppData%\CloudyWing.SchemaExporter\current\tools\CloudyWing.SchemaExporter.Cli.exe`（推測：依 Velopack 預設安裝位置）。

CLI 不沿用設定檔的 `OpenOutputFolder`，只有指定 `--open-output-folder` 才會開啟資料夾，因此 CI 內不會跳出檔案總管視窗。範例中的 `--no-open-output-folder` 可保留作為明確宣告。以 Ctrl+C 取消時 CLI 會回傳結束代碼 `4`，已寫出的檔案可能不完整，不應視為成功結果。

## Baseline 更新流程

當 schema 變更是預期行為時，將 CI 產生的 `.snapshot.json` 取代 repository 內的 baseline snapshot，並和資料庫 migration 或 schema 變更一併提交。

若只需要人工審查，不希望 workflow 因 drift 失敗，可移除 `Compare schema baseline` 步驟中的 `throw`，保留 artifact 上傳即可。
