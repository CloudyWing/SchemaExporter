using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CloudyWing.SchemaExporter.Desktop.Hosting;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace CloudyWing.SchemaExporter.Desktop;

/// <summary>
/// 承載 Blazor 介面的 WPF 主視窗。
/// </summary>
public partial class MainWindow : Window {
    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RouteCaptureTimeout = TimeSpan.FromSeconds(1);

    private readonly IServiceProvider serviceProvider;
    private readonly WebViewRecoveryCoordinator recovery;
    private readonly WebViewReleaser releaser;
    private readonly WebViewRouteRestorer routeRestorer;
    private volatile BlazorWebView blazorWebView;
    private volatile bool isClosing;

    /// <summary>
    /// 初始化 <see cref="MainWindow"/> 類別的新執行個體。
    /// </summary>
    /// <param name="serviceProvider">提供 Razor Component 注入服務的服務提供者。</param>
    /// <param name="recovery">負責 WebView 白屏復原的協調器。</param>
    internal MainWindow(IServiceProvider serviceProvider, WebViewRecoveryCoordinator recovery) {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(recovery);

        this.serviceProvider = serviceProvider;
        this.recovery = recovery;
        releaser = new WebViewReleaser(DisposeTimeout, TimeProvider.System, recovery.WriteLog);
        routeRestorer = new WebViewRouteRestorer(TimeProvider.System, recovery.WriteLog);

        InitializeComponent();
        blazorWebView = CreateBlazorWebView();
        WebViewHost.Children.Add(blazorWebView);

        recovery.Start(RebuildWebViewAsync);
        Closing += HandleClosing;
        Closed += HandleClosed;
#if DEBUG
        PreviewKeyDown += HandlePreviewKeyDown;
#endif
    }

    /// <summary>
    /// 重建 <see cref="BlazorWebView"/>：先擷取目前路由並建立新控制項疊在上方，再釋放並移除舊控制項，最後還原路由。
    /// </summary>
    /// <returns>表示重建作業的工作。</returns>
    internal async Task RebuildWebViewAsync() {
        BlazorWebView oldView = blazorWebView;
        string? route = await CaptureRouteAsync(oldView);

        BlazorWebView newView = CreateBlazorWebView();
        WebViewHost.Children.Add(newView);
        // 舊控制項留在視覺樹內直到釋放結束，BlazorWebView 的釋放需要控制項仍可回應。
        oldView.Visibility = Visibility.Collapsed;
        blazorWebView = newView;

        await releaser.ReleaseAsync(
            oldView.DisposeAsync,
            () => ForceDisposeWebView(oldView),
            () => WebViewHost.Children.Remove(oldView)
        );

        if (route is not null) {
            await RestoreRouteAsync(newView, route);
        }
    }

    private static void HandleUrlLoading(object? sender, UrlLoadingEventArgs e) {
        // 介面只顯示本機打包的資源；任何外部網址都不在 WebView 內開啟，也不交給瀏覽器。
        if (e.UrlLoadingStrategy != UrlLoadingStrategy.OpenInWebView) {
            e.UrlLoadingStrategy = UrlLoadingStrategy.CancelLoad;
        }
    }

    private async Task<string?> CaptureRouteAsync(BlazorWebView view) {
        try {
            string? route = null;
            bool dispatched = await view.TryDispatchAsync(services => {
                NavigationManager navigation = services.GetRequiredService<NavigationManager>();
                route = navigation.ToBaseRelativePath(navigation.Uri);
            }).WaitAsync(RouteCaptureTimeout);

            string? normalized = dispatched ? WebViewRoute.Normalize(route) : null;
            recovery.WriteLog("RouteCaptured", normalized ?? "none");
            return normalized;
        } catch (Exception ex) {
            // 舊控制項可能已失去回應，取不到路由時改回首頁即可，不影響重建。
            recovery.WriteLog("RouteCaptureFailed", WebViewRecoveryLog.FormatException(ex));
            return null;
        }
    }

    private Task RestoreRouteAsync(BlazorWebView target, string route) {
        return routeRestorer.RestoreAsync(
            route,
            target.TryDispatchAsync,
            () => ReferenceEquals(blazorWebView, target) && !isClosing
        );
    }

    private static bool ForceDisposeWebView(DependencyObject view) {
        // BlazorWebView 的正常釋放卡住時，內部的 WebView2 控制項不會被釋放而殘留 renderer 程序，
        // 必須在控制項仍位於視覺樹時直接釋放底層的 WebView2 控制項。
        WebView2CompositionControl? control = FindVisualChild<WebView2CompositionControl>(view);
        if (control is null) {
            return false;
        }

        control.Dispose();
        return true;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++) {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) {
                return match;
            }

            T? nested = FindVisualChild<T>(child);
            if (nested is not null) {
                return nested;
            }
        }

        return null;
    }

    private BlazorWebView CreateBlazorWebView() {
        BlazorWebView view = new() {
            HostPage = @"wwwroot\index.html",
            Services = serviceProvider
        };
        RootComponent rootComponent = new() {
            Selector = "#app",
            ComponentType = typeof(Components.App)
        };
        view.RootComponents.Add(rootComponent);
        view.UrlLoading += HandleUrlLoading;
        view.BlazorWebViewInitialized += HandleBlazorWebViewInitialized;
        return view;
    }

    private void HandleBlazorWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e) {
        e.WebView.CoreWebView2.NavigationCompleted += HandleNavigationCompleted;
    }

    private void HandleNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e) {
        if (e.IsSuccess) {
            recovery.MarkFirstRenderCompleted();
        }
    }

    private void HandleClosing(object? sender, CancelEventArgs e) {
        isClosing = true;
    }

    private void HandleClosed(object? sender, EventArgs e) {
        isClosing = true;
        recovery.Stop();
    }

#if DEBUG
    private void HandlePreviewKeyDown(object sender, KeyEventArgs e) {
        if (e.Key == Key.F12 && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) {
            e.Handled = true;
            recovery.RequestRebuild("DebugShortcut", skipDelay: true);
        }
    }
#endif
}
