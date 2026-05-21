using CloudyWing.SchemaExporter.Core;

namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// 提供各資料庫 provider 的 schema metadata 支援矩陣。
/// </summary>
internal static class ProviderCapabilityMatrix {
    /// <summary>
    /// 依資料庫類型名稱取得 provider capability 清單。
    /// </summary>
    /// <param name="databaseType">資料庫類型名稱。</param>
    /// <returns>provider capability 清單。</returns>
    internal static IReadOnlyList<ProviderCapability> GetCapabilities(string databaseType) {
        return databaseType switch {
            nameof(DatabaseType.SqlServer) => GetSqlServerCapabilities(),
            nameof(DatabaseType.Oracle) => GetOracleCapabilities(),
            _ => []
        };
    }

    private static IReadOnlyList<ProviderCapability> GetSqlServerCapabilities() {
        return [
            new ProviderCapability {
                Area = "Tables",
                SupportLevel = ExportSupportLevel.Full,
                Notes = "Exports user table names, schemas and MS_Description comments."
            },
            new ProviderCapability {
                Area = "Views",
                SupportLevel = ExportSupportLevel.Partial,
                Notes = "Exports view objects and columns; view SQL, dependencies and index details are not exported."
            },
            new ProviderCapability {
                Area = "Columns",
                SupportLevel = ExportSupportLevel.Full,
                Notes = "Exports type, nullability, default, primary key, identity and MS_Description comments."
            },
            new ProviderCapability {
                Area = "Indexes",
                SupportLevel = ExportSupportLevel.Full,
                Notes = "Exports table indexes, uniqueness, clustering, included columns, primary keys "
                    + "and foreign keys."
            },
            new ProviderCapability {
                Area = "Routines",
                SupportLevel = ExportSupportLevel.Partial,
                Notes = "Exports procedures and functions with signatures and definitions when sys.sql_modules "
                    + "exposes them."
            }
        ];
    }

    private static IReadOnlyList<ProviderCapability> GetOracleCapabilities() {
        return [
            new ProviderCapability {
                Area = "Tables",
                SupportLevel = ExportSupportLevel.Full,
                Notes = "Exports user table names, owner and comments."
            },
            new ProviderCapability {
                Area = "Views",
                SupportLevel = ExportSupportLevel.Partial,
                Notes = "Exports view objects and columns; view SQL, dependencies and index details are not exported."
            },
            new ProviderCapability {
                Area = "Columns",
                SupportLevel = ExportSupportLevel.Partial,
                Notes = "Exports type, nullability, primary key, identity when available and comments; defaults "
                    + "are not exported."
            },
            new ProviderCapability {
                Area = "Indexes",
                SupportLevel = ExportSupportLevel.Partial,
                Notes = "Exports table indexes, uniqueness, primary keys and foreign keys; generated indexes "
                    + "are omitted."
            },
            new ProviderCapability {
                Area = "Routines",
                SupportLevel = ExportSupportLevel.Partial,
                Notes = "Exports procedures, functions and package routines; descriptions are empty and definitions "
                    + "can be unavailable."
            }
        ];
    }
}
