using System.Security.Cryptography;
using AngleSharp.Dom;
using Bunit;
using CloudyWing.SchemaExporter.Core;
using CloudyWing.SchemaExporter.Desktop.Components.Pages;
using CloudyWing.SchemaExporter.Desktop.State;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CloudyWing.SchemaExporter.Desktop.Tests.Components;

[TestFixture]
public sealed class SettingsPageTests {
    [Test]
    public void Render_WhenConnectionIsEnvironmentReference_DisablesEncryptionOption() {
        using ComponentTestHost host = new(ComponentTestHost.CreateOptions(
            @"C:\Exports",
            new SchemaConnection { Name = "Ci", ConnectionString = "${env:CI_DB}" }
        ));

        IRenderedComponent<Settings> cut = RenderLoaded(host);

        using (Assert.EnterMultipleScope()) {
            Assert.That(FindEncryptionCheckbox(cut).HasAttribute("disabled"), Is.True);
            Assert.That(cut.Markup, Does.Contain("環境變數參照不需加密"));
        }
    }

    [Test]
    public void Render_SettingsFileHint_IsKeyboardFocusable() {
        using ComponentTestHost host = new(ComponentTestHost.CreateOptions(@"C:\Exports"));

        IRenderedComponent<Settings> cut = RenderLoaded(host);

        Assert.That(cut.Find(".settings-file-hint").GetAttribute("tabindex"), Is.EqualTo("0"));
    }

    [Test]
    public void Render_WhenConnectionIsPlainText_EnablesEncryptionOption() {
        using ComponentTestHost host = new(ComponentTestHost.CreateOptions(@"C:\Exports"));

        IRenderedComponent<Settings> cut = RenderLoaded(host);

        IElement checkbox = FindEncryptionCheckbox(cut);
        using (Assert.EnterMultipleScope()) {
            Assert.That(checkbox.HasAttribute("disabled"), Is.False);
            Assert.That(checkbox.HasAttribute("checked"), Is.False);
        }
    }

    [Test]
    public void Render_WhenEncryptedValueCannotBeDecrypted_ShowsReentryWarning() {
        using ComponentTestHost host = new(ComponentTestHost.CreateOptions(
            @"C:\Exports",
            new SchemaConnection { Name = "Prod", ConnectionString = "dpapi:FOREIGN" }
        ));
        host.Protector.Unprotect("dpapi:FOREIGN").Throws(new CryptographicException("key not valid"));

        IRenderedComponent<Settings> cut = RenderLoaded(host);

        Assert.That(cut.Markup, Does.Contain("無法解開，請重新輸入"));
    }

    [Test]
    public void Render_WhenConnectionStringIsShown_MasksItByDefault() {
        using ComponentTestHost host = new(ComponentTestHost.CreateOptions(@"C:\Exports"));

        IRenderedComponent<Settings> cut = RenderLoaded(host);

        Assert.That(cut.FindAll("input[type=password]"), Has.Count.EqualTo(1));
    }

    [Test]
    public void SelectTheme_WhenDarkIsChosen_AppliesAndPersistsWithoutSavingSettings() {
        using ComponentTestHost host = new(ComponentTestHost.CreateOptions(@"C:\Exports"));
        IRenderedComponent<MudPopoverProvider> popover = host.Context.Render<MudPopoverProvider>();
        IRenderedComponent<Settings> cut = RenderLoaded(host);

        cut.Find(".mud-select .mud-input-control").MouseDown();
        popover.WaitForState(() => FindThemeOption(popover, "深色") is not null);
        FindThemeOption(popover, "深色")!.TriggerEvent("onclick", new MouseEventArgs());
        string preferencePath = host.ThemePreferenceFilePath;
        cut.WaitForState(() => File.Exists(preferencePath));

        using (Assert.EnterMultipleScope()) {
            Assert.That(host.ThemePreference.Mode, Is.EqualTo(ThemeMode.Dark));
            Assert.That(File.ReadAllText(preferencePath), Does.Contain("Dark"));
            host.SettingsService.DidNotReceive().SaveAsync(Arg.Any<SchemaOptions>());
        }
    }

    [Test]
    public void Render_WhenThemePreferenceCannotBeSaved_ShowsWarning() {
        using ComponentTestHost host = new(ComponentTestHost.CreateOptions(@"C:\Exports"));
        // 暫存檔路徑已被資料夾佔用時寫入會失敗，用來觸發儲存錯誤。
        Directory.CreateDirectory(host.ThemePreferenceFilePath + ".tmp");
        IRenderedComponent<Settings> cut = RenderLoaded(host);

        cut.InvokeAsync(() => host.ThemePreference.SetModeAsync(ThemeMode.Dark));
        cut.WaitForState(() => cut.Markup.Contains("無法儲存主題設定", StringComparison.Ordinal));

        Assert.That(cut.Find(".mud-alert").TextContent, Does.Contain("無法儲存主題設定"));
    }

    // bUnit 的 WaitForAssertion 會重試失敗的斷言，但 NUnit 4 會記錄每次失敗，因此改以 WaitForState 等待載入完成後再斷言。
    private static IRenderedComponent<Settings> RenderLoaded(ComponentTestHost host) {
        IRenderedComponent<Settings> cut = host.Context.Render<Settings>();
        cut.WaitForState(() => cut.Markup.Contains("加密儲存", StringComparison.Ordinal));
        return cut;
    }

    private static IElement FindEncryptionCheckbox(IRenderedComponent<Settings> cut) {
        IElement label = cut.FindAll("label").Single(x => x.TextContent.Contains("加密儲存", StringComparison.Ordinal));
        return label.QuerySelector("input[type=checkbox]")
            ?? throw new AssertionException("找不到加密儲存的核取方塊。");
    }

    private static IElement? FindThemeOption(IRenderedComponent<MudPopoverProvider> popover, string label) {
        return popover.FindAll(".mud-list-item")
            .FirstOrDefault(x => x.TextContent.Contains(label, StringComparison.Ordinal));
    }
}
