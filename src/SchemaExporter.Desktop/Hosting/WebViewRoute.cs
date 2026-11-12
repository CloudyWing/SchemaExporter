namespace CloudyWing.SchemaExporter.Desktop.Hosting;

/// <summary>
/// 判定重建前擷取到的路由字串是否可在重建後還原。
/// </summary>
internal static class WebViewRoute {
    /// <summary>
    /// 正規化擷取到的 base 相對路由。
    /// </summary>
    /// <param name="route">以 <c>NavigationManager.ToBaseRelativePath</c> 取得的路由。</param>
    /// <returns>可還原的路由；為 <see langword="null"/>、空字串或含 <c>://</c>（絕對網址）時回傳 <see langword="null"/>，表示沒有可還原的路由。</returns>
    internal static string? Normalize(string? route) {
        if (string.IsNullOrEmpty(route) || route.Contains("://", StringComparison.Ordinal)) {
            return null;
        }

        return route;
    }
}
