using CloudyWing.SchemaExporter.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CloudyWing.SchemaExporter.Models;

internal sealed class EditableConnection : ObservableObject {
    private string name = "";
    private DatabaseType databaseType = DatabaseType.SqlServer;
    private string connectionString = "";
    private string? exportProfileName;

    public string Name {
        get => name;
        set => SetProperty(ref name, value);
    }

    public DatabaseType DatabaseType {
        get => databaseType;
        set => SetProperty(ref databaseType, value);
    }

    public string ConnectionString {
        get => connectionString;
        set => SetProperty(ref connectionString, value);
    }

    public string? ExportProfileName {
        get => exportProfileName;
        set => SetProperty(ref exportProfileName, value);
    }

    public static EditableConnection FromSchemaConnection(SchemaConnection connection) {
        ArgumentNullException.ThrowIfNull(connection, nameof(connection));

        return new EditableConnection {
            Name = connection.Name,
            DatabaseType = connection.DatabaseType,
            ConnectionString = connection.ConnectionString,
            ExportProfileName = connection.ExportProfileName
        };
    }

    public SchemaConnection ToSchemaConnection() {
        return new SchemaConnection {
            Name = Name.Trim(),
            DatabaseType = DatabaseType,
            ConnectionString = ConnectionString.Trim(),
            ExportProfileName = string.IsNullOrWhiteSpace(ExportProfileName)
                ? null
                : ExportProfileName.Trim()
        };
    }
}