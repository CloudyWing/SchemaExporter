using System.Windows;
using Microsoft.AspNetCore.Components.WebView;

namespace CloudyWing.SchemaExporter.Desktop;

/// <summary>
/// 承載 Blazor 介面的 WPF 主視窗。
/// </summary>
public partial class MainWindow : Window {
    /// <summary>
    /// 初始化 <see cref="MainWindow"/> 類別的新執行個體。
    /// </summary>
    /// <param name="serviceProvider">提供 Razor Component 注入服務的服務提供者。</param>
    internal MainWindow(IServiceProvider serviceProvider) {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        InitializeComponent();
        BlazorWebView.Services = serviceProvider;
        BlazorWebView.UrlLoading += HandleUrlLoading;
    }

    private static void HandleUrlLoading(object? sender, UrlLoadingEventArgs e) {
        // 介面只顯示本機打包的資源；任何外部網址都不在 WebView 內開啟，也不交給瀏覽器。
        if (e.UrlLoadingStrategy != UrlLoadingStrategy.OpenInWebView) {
            e.UrlLoadingStrategy = UrlLoadingStrategy.CancelLoad;
        }
    }
}
