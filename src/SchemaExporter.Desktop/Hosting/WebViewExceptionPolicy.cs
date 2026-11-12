using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace CloudyWing.SchemaExporter.Desktop.Hosting;

/// <summary>
/// 判斷 UI 執行緒上未處理的例外是否為 WebView2 圖形資源遺失造成的已知例外，並限制吞下例外的頻率。
/// </summary>
/// <remarks>
/// 本類別只能在 UI 執行緒使用，不具執行緒安全。
/// </remarks>
internal sealed class WebViewExceptionPolicy {
    /// <summary>
    /// 一個時間視窗內最多可吞下的次數。
    /// </summary>
    internal const int MaxSwallowCount = 3;

    /// <summary>
    /// <see cref="Marshal"/> 的完整型別名稱，堆疊判斷時略過。
    /// </summary>
    internal const string MarshalTypeName = "System.Runtime.InteropServices.Marshal";

    /// <summary>
    /// WebView2 擷取畫面用的 D3DImage 型別名稱。
    /// </summary>
    internal const string GraphicsItemTypeName = "Microsoft.Web.WebView2.Wpf.GraphicsItemD3DImage";

    /// <summary>
    /// WebView2 Direct3D 輔助型別名稱。
    /// </summary>
    internal const string DirectHelperTypeName = "Microsoft.Web.WebView2.Wpf.Direct3DHelper";

    /// <summary>
    /// 計算吞下次數的時間視窗。
    /// </summary>
    internal static readonly TimeSpan SwallowWindow = TimeSpan.FromSeconds(60);

    private readonly TimeProvider timeProvider;
    private readonly Func<Exception, IReadOnlyList<string?>> frameReader;
    private readonly Queue<long> swallowTimestamps = new();

    /// <summary>
    /// 初始化 <see cref="WebViewExceptionPolicy"/> 類別的新執行個體。
    /// </summary>
    /// <param name="timeProvider">提供單調時鐘的時間來源。</param>
    /// <param name="frameReader">
    /// 取出例外堆疊中各 frame 宣告型別名稱（由內往外）的函式，未提供時讀取真實堆疊。
    /// </param>
    internal WebViewExceptionPolicy(
        TimeProvider timeProvider,
        Func<Exception, IReadOnlyList<string?>>? frameReader = null
    ) {
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.timeProvider = timeProvider;
        this.frameReader = frameReader ?? GetDeclaringTypeNames;
    }

    /// <summary>
    /// 取得一個值，用以指出是否已完成第一次成功渲染。旗標成立後不會重設。
    /// </summary>
    internal bool IsFirstRenderCompleted { get; private set; }

    /// <summary>
    /// 標記第一次成功渲染已完成。
    /// </summary>
    /// <returns>本次呼叫才使旗標成立時為 <see langword="true"/>；先前已成立則為 <see langword="false"/>。</returns>
    internal bool MarkFirstRenderCompleted() {
        if (IsFirstRenderCompleted) {
            return false;
        }

        IsFirstRenderCompleted = true;
        return true;
    }

    /// <summary>
    /// 依型別、堆疊、時機與次數四項條件判定例外是否可以吞下。只有回傳 <see cref="WebViewExceptionDecision.Swallow"/> 時才會消耗一次次數。
    /// </summary>
    /// <param name="exception">未處理的例外。</param>
    /// <returns>判定結果。</returns>
    internal WebViewExceptionDecision Evaluate(Exception exception) {
        ArgumentNullException.ThrowIfNull(exception);

        Exception? matched = FindMatchedException(exception);
        if (matched is null) {
            return WebViewExceptionDecision.NotMatched;
        }

        if (ContainsExcludedException(exception)) {
            return WebViewExceptionDecision.Excluded;
        }

        if (!IsKnownGraphicsStack(frameReader(matched))) {
            return WebViewExceptionDecision.StackMismatch;
        }

        if (!IsFirstRenderCompleted) {
            return WebViewExceptionDecision.NotYetRendered;
        }

        return TryAcquireSwallowSlot()
            ? WebViewExceptionDecision.Swallow
            : WebViewExceptionDecision.OverLimit;
    }

    /// <summary>
    /// 從例外本身起沿 InnerException 鏈往內找第一個 <see cref="ArgumentException"/> 或 <see cref="COMException"/>。
    /// </summary>
    /// <param name="exception">鏈的起點。</param>
    /// <returns>命中的例外；沒有命中時為 <see langword="null"/>。</returns>
    internal static Exception? FindMatchedException(Exception exception) {
        for (Exception? current = exception; current is not null; current = current.InnerException) {
            if (current is ArgumentException or COMException) {
                return current;
            }
        }

        return null;
    }

    /// <summary>
    /// 判斷例外鏈中是否含有組件載入類的例外。
    /// </summary>
    /// <param name="exception">鏈的起點。</param>
    /// <returns>含有排除型別時為 <see langword="true"/>。</returns>
    internal static bool ContainsExcludedException(Exception exception) {
        for (Exception? current = exception; current is not null; current = current.InnerException) {
            if (current is FileNotFoundException
                or FileLoadException
                or TypeLoadException
                or BadImageFormatException
            ) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 從最內層往外略過 <see cref="Marshal"/> 的 frame，檢查第一個非 Marshal frame 的宣告型別是否為 WebView2 的圖形相關型別。
    /// </summary>
    /// <param name="declaringTypeNames">各 frame 最外層宣告型別的完整名稱（由內往外），無法取得型別時為 <see langword="null"/>。</param>
    /// <returns>符合時為 <see langword="true"/>。</returns>
    internal static bool IsKnownGraphicsStack(IEnumerable<string?> declaringTypeNames) {
        ArgumentNullException.ThrowIfNull(declaringTypeNames);

        foreach (string? name in declaringTypeNames) {
            if (name == MarshalTypeName) {
                continue;
            }

            return name is GraphicsItemTypeName or DirectHelperTypeName;
        }

        return false;
    }

    /// <summary>
    /// 取出例外堆疊中各 frame 最外層宣告型別的完整名稱，順序由內往外。
    /// </summary>
    /// <param name="exception">例外。</param>
    /// <returns>型別名稱序列；frame 沒有方法或宣告型別時該項為 <see langword="null"/>。</returns>
    internal static IReadOnlyList<string?> GetDeclaringTypeNames(Exception exception) {
        ArgumentNullException.ThrowIfNull(exception);

        StackFrame[] frames = new StackTrace(exception, false).GetFrames();
        List<string?> names = new(frames.Length);
        foreach (StackFrame frame in frames) {
            Type? declaringType = frame.GetMethod()?.DeclaringType;
            names.Add(declaringType is null ? null : GetOutermostTypeName(declaringType));
        }

        return names;
    }

    /// <summary>
    /// 取得型別所屬最外層宣告型別的完整名稱。巢狀型別與編譯器產生的型別（如 <c>&lt;&gt;c</c>）會折回包含它的型別。
    /// </summary>
    /// <param name="type">型別。</param>
    /// <returns>最外層宣告型別的完整名稱。</returns>
    internal static string? GetOutermostTypeName(Type type) {
        ArgumentNullException.ThrowIfNull(type);

        Type current = type;
        while (current.DeclaringType is not null) {
            current = current.DeclaringType;
        }

        return current.FullName;
    }

    private bool TryAcquireSwallowSlot() {
        long now = timeProvider.GetTimestamp();
        while (swallowTimestamps.Count > 0
            && timeProvider.GetElapsedTime(swallowTimestamps.Peek(), now) >= SwallowWindow
        ) {
            swallowTimestamps.Dequeue();
        }

        if (swallowTimestamps.Count >= MaxSwallowCount) {
            return false;
        }

        swallowTimestamps.Enqueue(now);
        return true;
    }
}
