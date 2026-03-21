using System.Drawing;
using System.IO;
using System.Windows;
using CloudyWing.SchemaExporter.SchemaProviders;
using CloudyWing.SpreadsheetExporter;
using CloudyWing.SpreadsheetExporter.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CloudyWing.SchemaExporter;

/// <summary>
/// Interaction logic for App.xaml.
/// </summary>
public partial class App : Application {
    protected override void OnStartup(StartupEventArgs e) {
        SpreadsheetManager.SetExporter(() => new NpoiExcelExporter());
        SpreadsheetManager.DefaultCellStyles = new CellStyleConfiguration(x => {
            CellStyle cellStyle = new(
                SpreadsheetExporter.HorizontalAlignment.Center,
                SpreadsheetExporter.VerticalAlignment.Middle,
                false,
                true,
                Color.Empty,
                new CellFont("微軟正黑體", 10, Color.Empty, SpreadsheetExporter.FontStyles.None),
                null,
                false
            );

            CellFont headerFont = cellStyle.Font with {
                Style = cellStyle.Font.Style | SpreadsheetExporter.FontStyles.IsBold
            };

            x.CellStyle = cellStyle;
            x.GridCellStyle = cellStyle with {
                HorizontalAlignment = SpreadsheetExporter.HorizontalAlignment.Left
            };
            x.HeaderStyle = cellStyle with {
                Font = headerFont,
                HorizontalAlignment = SpreadsheetExporter.HorizontalAlignment.Center,
                HasBorder = true
            };
            x.FieldStyle = cellStyle with {
                HorizontalAlignment = SpreadsheetExporter.HorizontalAlignment.Left,
                HasBorder = true
            };
        });

        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        ServiceCollection serviceCollection = new();
        serviceCollection.Configure<SchemaOptions>(configuration.GetSection(SchemaOptions.OptionsName));
        serviceCollection.AddSingleton<IDatabaseSchemaProvider, SqlServerDatabaseSchemaProvider>();
        serviceCollection.AddSingleton<IDatabaseSchemaProvider, OracleDatabaseSchemaProvider>();
        serviceCollection.AddSingleton<IDatabaseSchemaProviderFactory, DatabaseSchemaProviderFactory>();
        serviceCollection.AddTransient<MainWindow>();
        serviceCollection.AddTransient<ViewModel>();

        ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        MainWindow mainWindow = serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}
