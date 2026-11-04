using System.Text;
using System.Text.Json;
using CloudyWing.SchemaExporter.Cli;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Core.Exporting;
using CloudyWing.SchemaExporter.Infrastructure;
using CloudyWing.SchemaExporter.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

Console.OutputEncoding = new UTF8Encoding(false);

using CancellationTokenSource cancellation = new();
Console.CancelKeyPress += (_, eventArgs) => {
    // 第一次 Ctrl+C 交給命令自行收尾並回傳 Canceled；再按一次才由系統直接終止。
    if (cancellation.IsCancellationRequested) {
        return;
    }

    eventArgs.Cancel = true;
    cancellation.Cancel();
};

ServiceProvider serviceProvider;
try {
    SpreadsheetExporterBootstrapper.Configure();
    IConfiguration configuration = SchemaExporterConfiguration.Build(CliArguments.RequiresUserConfig(args));

    ServiceCollection services = new();
    services.AddLogging(logging => {
        logging.ClearProviders();
        logging.AddSimpleConsole(options => {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });
        logging.SetMinimumLevel(LogLevel.Information);
    });
    // stdout 只放進度與結果，錯誤等級的記錄改寫到 stderr，讓呼叫端能分開解析。
    services.Configure<ConsoleLoggerOptions>(options => options.LogToStandardErrorThreshold = LogLevel.Error);
    services.AddSchemaExporterCore(configuration);
    services.AddSchemaExporterInfrastructure();
    services.AddSingleton<CliRunner>();
    serviceProvider = services.BuildServiceProvider();
} catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or JsonException) {
    // 沿用舊版單一 EXE 的啟動失敗代碼 1，避免既有 Agent Skill 與排程的判斷失準。
    Console.Error.WriteLine($"Startup failed: {ex.Message}");
    return 1;
}

await using (serviceProvider) {
    return await serviceProvider.GetRequiredService<CliRunner>().RunAsync(args, cancellation.Token);
}
