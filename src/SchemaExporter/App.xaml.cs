using System.IO;
using System.Windows;
using CloudyWing.SpreadsheetExporter;
using CloudyWing.SpreadsheetExporter.Config;
using CloudyWing.SpreadsheetExporter.Excel.NPOI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CloudyWing.SchemaExporter {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            SpreadsheetManager.SetExporter(() => new ExcelExporter());
            SpreadsheetManager.DefaultCellStyles = new CellStyleConfiguration(x =>
            {
                CellStyle cellStyle = new(
                    SpreadsheetExporter.HorizontalAlignment.Center,
                    SpreadsheetExporter.VerticalAlignment.Middle,
                    false, true,
                    null,
                    new CellFont("微軟正黑體", 10, null, SpreadsheetExporter.FontStyles.None),
                    null,
                    false
                );

                CellFont headerFont = cellStyle.Font
                    .CloneAndSetStyle(cellStyle.Font.Style | SpreadsheetExporter.FontStyles.IsBold);

                x.CellStyle = cellStyle;
                x.GridCellStyle = cellStyle
                    .CloneAndSetHorizontalAlignment(SpreadsheetExporter.HorizontalAlignment.Left);
                x.HeaderStyle = cellStyle
                    .CloneAndSetFont(headerFont)
                    .CloneAndSetHorizontalAlignment(SpreadsheetExporter.HorizontalAlignment.Center)
                    .CloneAndSetBorder(true);
                x.FieldStyle = cellStyle
                    .CloneAndSetHorizontalAlignment(SpreadsheetExporter.HorizontalAlignment.Left)
                    .CloneAndSetBorder(true);
            });

            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            IConfiguration configuration = builder.Build();

            ServiceCollection serviceCollection = new();
            ConfigureServices(serviceCollection, configuration);

            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            MainWindow mainWindow = serviceProvider.GetRequiredService<MainWindow>()!;
            mainWindow.Show();
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<SchemaOptions>(configuration.GetSection(SchemaOptions.OptionsName));
            services.AddTransient<MainWindow>();
            services.AddTransient<ViewModel>();
        }
    }

}
