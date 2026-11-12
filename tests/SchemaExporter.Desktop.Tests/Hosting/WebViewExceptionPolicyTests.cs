using System.Runtime.InteropServices;
using CloudyWing.SchemaExporter.Desktop.Hosting;

namespace CloudyWing.SchemaExporter.Desktop.Tests.Hosting;

[TestFixture]
public sealed class WebViewExceptionPolicyTests {
    private const string GraphicsItem = "Microsoft.Web.WebView2.Wpf.GraphicsItemD3DImage";
    private const string DirectHelper = "Microsoft.Web.WebView2.Wpf.Direct3DHelper";
    private const string Marshal = "System.Runtime.InteropServices.Marshal";
    private const string WriteableBitmap = "System.Windows.Media.Imaging.WriteableBitmap";

    private ManualTimeProvider time = null!;

    [SetUp]
    public void SetUp() {
        time = new ManualTimeProvider();
    }

    [TestCase(GraphicsItem)]
    [TestCase(DirectHelper)]
    public void Evaluate_ArgumentExceptionFromGraphicsType_Swallows(string typeName) {
        WebViewExceptionPolicy sut = CreateRenderedPolicy(typeName, "Some.Caller");

        WebViewExceptionDecision result = sut.Evaluate(new ArgumentException("bad"));

        Assert.That(result, Is.EqualTo(WebViewExceptionDecision.Swallow));
    }

    [Test]
    public void Evaluate_MarshalFramesBeforeGraphicsType_SkipsMarshalAndSwallows() {
        WebViewExceptionPolicy sut = CreateRenderedPolicy(Marshal, Marshal, GraphicsItem, "Some.Caller");

        WebViewExceptionDecision result = sut.Evaluate(new ArgumentException("bad"));

        Assert.That(result, Is.EqualTo(WebViewExceptionDecision.Swallow));
    }

    [Test]
    public void Evaluate_FirstNonMarshalTypeIsOther_DoesNotSwallow() {
        WebViewExceptionPolicy sut = CreateRenderedPolicy(Marshal, WriteableBitmap, GraphicsItem);

        WebViewExceptionDecision result = sut.Evaluate(new ArgumentException("bad"));

        Assert.That(result, Is.EqualTo(WebViewExceptionDecision.StackMismatch));
    }

    [Test]
    public void Evaluate_FirstNonMarshalFrameHasNoDeclaringType_DoesNotSwallow() {
        WebViewExceptionPolicy sut = CreateRenderedPolicy(Marshal, null, GraphicsItem);

        WebViewExceptionDecision result = sut.Evaluate(new ArgumentException("bad"));

        Assert.That(result, Is.EqualTo(WebViewExceptionDecision.StackMismatch));
    }

    [Test]
    public void Evaluate_OnlyMarshalFrames_DoesNotSwallow() {
        WebViewExceptionPolicy sut = CreateRenderedPolicy(Marshal);

        WebViewExceptionDecision result = sut.Evaluate(new ArgumentException("bad"));

        Assert.That(result, Is.EqualTo(WebViewExceptionDecision.StackMismatch));
    }

    [Test]
    public void Evaluate_ComExceptionInInnerChain_Swallows() {
        WebViewExceptionPolicy sut = CreateRenderedPolicy(GraphicsItem);
        InvalidOperationException exception = new("outer", new COMException("com"));

        WebViewExceptionDecision result = sut.Evaluate(exception);

        Assert.That(result, Is.EqualTo(WebViewExceptionDecision.Swallow));
    }

    [Test]
    public void Evaluate_NoArgumentOrComExceptionInChain_IsNotMatched() {
        WebViewExceptionPolicy sut = CreateRenderedPolicy(GraphicsItem);
        InvalidOperationException exception = new("outer", new NullReferenceException());

        WebViewExceptionDecision result = sut.Evaluate(exception);

        Assert.That(result, Is.EqualTo(WebViewExceptionDecision.NotMatched));
    }

    [TestCaseSource(nameof(ExcludedInnerExceptions))]
    public void Evaluate_ExcludedExceptionInChain_DoesNotSwallow(Exception inner) {
        WebViewExceptionPolicy sut = CreateRenderedPolicy(GraphicsItem);
        ArgumentException exception = new("outer", inner);

        WebViewExceptionDecision result = sut.Evaluate(exception);

        Assert.That(result, Is.EqualTo(WebViewExceptionDecision.Excluded));
    }

    [Test]
    public void Evaluate_BeforeFirstRender_DoesNotSwallow() {
        WebViewExceptionPolicy sut = new(time, _ => [GraphicsItem]);

        WebViewExceptionDecision result = sut.Evaluate(new ArgumentException("bad"));

        Assert.That(result, Is.EqualTo(WebViewExceptionDecision.NotYetRendered));
    }

    [Test]
    public void Evaluate_AfterFirstRender_Swallows() {
        WebViewExceptionPolicy sut = new(time, _ => [GraphicsItem]);
        sut.Evaluate(new ArgumentException("bad"));

        sut.MarkFirstRenderCompleted();
        WebViewExceptionDecision result = sut.Evaluate(new ArgumentException("bad"));

        Assert.That(result, Is.EqualTo(WebViewExceptionDecision.Swallow));
    }

    [Test]
    public void MarkFirstRenderCompleted_CalledTwice_ReturnsTrueOnlyFirstTime() {
        WebViewExceptionPolicy sut = new(time);

        bool first = sut.MarkFirstRenderCompleted();
        bool second = sut.MarkFirstRenderCompleted();

        using (Assert.EnterMultipleScope()) {
            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
            Assert.That(sut.IsFirstRenderCompleted, Is.True);
        }
    }

    [Test]
    public void Evaluate_FourthSwallowWithinWindow_DoesNotSwallow() {
        WebViewExceptionPolicy sut = CreateRenderedPolicy(GraphicsItem);

        WebViewExceptionDecision[] results = [
            sut.Evaluate(new ArgumentException("1")),
            sut.Evaluate(new ArgumentException("2")),
            sut.Evaluate(new ArgumentException("3")),
            sut.Evaluate(new ArgumentException("4"))
        ];

        Assert.That(results, Is.EqualTo(new[] {
            WebViewExceptionDecision.Swallow,
            WebViewExceptionDecision.Swallow,
            WebViewExceptionDecision.Swallow,
            WebViewExceptionDecision.OverLimit
        }));
    }

    [Test]
    public void Evaluate_AfterWindowElapsed_SwallowsAgain() {
        WebViewExceptionPolicy sut = CreateRenderedPolicy(GraphicsItem);
        sut.Evaluate(new ArgumentException("1"));
        time.Advance(TimeSpan.FromSeconds(30));
        sut.Evaluate(new ArgumentException("2"));
        time.Advance(TimeSpan.FromSeconds(20));
        sut.Evaluate(new ArgumentException("3"));

        time.Advance(TimeSpan.FromSeconds(9));
        WebViewExceptionDecision stillInWindow = sut.Evaluate(new ArgumentException("4"));
        time.Advance(TimeSpan.FromSeconds(2));
        WebViewExceptionDecision afterFirstExpired = sut.Evaluate(new ArgumentException("5"));

        using (Assert.EnterMultipleScope()) {
            Assert.That(stillInWindow, Is.EqualTo(WebViewExceptionDecision.OverLimit));
            Assert.That(afterFirstExpired, Is.EqualTo(WebViewExceptionDecision.Swallow));
        }
    }

    [Test]
    public void Evaluate_RejectedExceptions_DoNotConsumeSwallowCount() {
        WebViewExceptionPolicy sut = new(time, e => e.Message == "known" ? [GraphicsItem] : [WriteableBitmap]);
        sut.MarkFirstRenderCompleted();

        for (int i = 0; i < 10; i++) {
            sut.Evaluate(new ArgumentException("unknown"));
        }

        WebViewExceptionDecision result = sut.Evaluate(new ArgumentException("known"));

        Assert.That(result, Is.EqualTo(WebViewExceptionDecision.Swallow));
    }

    [Test]
    public void GetOutermostTypeName_NestedType_ReturnsOutermostDeclaringType() {
        string? result = WebViewExceptionPolicy.GetOutermostTypeName(typeof(Outer.Inner.Deepest));

        Assert.That(result, Is.EqualTo(typeof(WebViewExceptionPolicyTests).FullName));
    }

    [Test]
    public void GetOutermostTypeName_CompilerGeneratedClosureType_ReturnsContainingType() {
        Func<int> lambda = () => 1;
        Type closureType = lambda.Method.DeclaringType!;

        string? result = WebViewExceptionPolicy.GetOutermostTypeName(closureType);

        Assert.That(result, Is.EqualTo(typeof(WebViewExceptionPolicyTests).FullName));
    }

    [Test]
    public void GetOutermostTypeName_TopLevelType_ReturnsFullName() {
        string? result = WebViewExceptionPolicy.GetOutermostTypeName(typeof(string));

        Assert.That(result, Is.EqualTo("System.String"));
    }

    [Test]
    public void GetDeclaringTypeNames_ExceptionFromMarshal_StartsInnermostAndReachesCaller() {
        Exception exception = CaptureMarshalException();

        IReadOnlyList<string?> names = WebViewExceptionPolicy.GetDeclaringTypeNames(exception);

        string? firstNonMarshal = names.First(name => name != Marshal);
        Assert.That(firstNonMarshal, Is.EqualTo(typeof(WebViewExceptionPolicyTests).FullName));
    }

    [Test]
    public void Evaluate_WithRealStackFromOtherType_DoesNotSwallow() {
        WebViewExceptionPolicy sut = new(time);
        sut.MarkFirstRenderCompleted();
        Exception exception = CaptureMarshalException();

        WebViewExceptionDecision result = sut.Evaluate(exception);

        Assert.That(result, Is.EqualTo(WebViewExceptionDecision.StackMismatch));
    }

    [TestCase(new[] { Marshal, GraphicsItem }, true)]
    [TestCase(new[] { DirectHelper }, true)]
    [TestCase(new[] { WriteableBitmap, DirectHelper }, false)]
    [TestCase(new string[0], false)]
    public void IsKnownGraphicsStack_Sequence_ReturnsExpected(string[] names, bool expected) {
        bool result = WebViewExceptionPolicy.IsKnownGraphicsStack(names);

        Assert.That(result, Is.EqualTo(expected));
    }

    private static IEnumerable<TestCaseData> ExcludedInnerExceptions() {
        yield return new TestCaseData(new FileNotFoundException("missing")).SetName("FileNotFoundException");
        yield return new TestCaseData(new FileLoadException("load")).SetName("FileLoadException");
        yield return new TestCaseData(new TypeLoadException("type")).SetName("TypeLoadException");
        yield return new TestCaseData(new BadImageFormatException("image")).SetName("BadImageFormatException");
    }

    private static Exception CaptureMarshalException() {
        try {
            // E_INVALIDARG 會由 Marshal 轉成 ArgumentException，堆疊最內層為 Marshal。
            System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(unchecked((int)0x80070057));
        } catch (ArgumentException ex) {
            return ex;
        }

        throw new InvalidOperationException("Marshal.ThrowExceptionForHR 沒有拋出 ArgumentException。");
    }

    private WebViewExceptionPolicy CreateRenderedPolicy(params string?[] frames) {
        WebViewExceptionPolicy policy = new(time, _ => frames);
        policy.MarkFirstRenderCompleted();
        return policy;
    }

    private static class Outer {
        public static class Inner {
            public sealed class Deepest {
            }
        }
    }
}
