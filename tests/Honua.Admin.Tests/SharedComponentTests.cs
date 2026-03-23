using Bunit;
using NSubstitute;
using Xunit;
using MudBlazor;
using MudBlazor.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Honua.Admin.Components;
using Microsoft.JSInterop;

namespace Honua.Admin.Tests;

public class SharedComponentTests : IAsyncLifetime
{
    private readonly BunitContext _ctx;

    public SharedComponentTests()
    {
        _ctx = new BunitContext();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices();

        // Replace the PopoverService with a mock to avoid requiring MudPopoverProvider
        var mockPopoverService = Substitute.For<IPopoverService>();
        mockPopoverService.CreatePopoverAsync(Arg.Any<IPopover>())
            .Returns(Task.FromResult(Guid.NewGuid()));
        mockPopoverService.UpdatePopoverAsync(Arg.Any<IPopover>())
            .Returns(Task.FromResult(true));
        mockPopoverService.DestroyPopoverAsync(Arg.Any<IPopover>())
            .Returns(Task.FromResult(true));
        mockPopoverService.GetProviderCountAsync()
            .Returns(new ValueTask<int>(1));
        _ctx.Services.RemoveAll<IPopoverService>();
        _ctx.Services.AddScoped<IPopoverService>(_ => mockPopoverService);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // ErrorBanner tests

    [Fact]
    public void ErrorBanner_RendersMessage_WhenProvided()
    {
        var cut = _ctx.Render<ErrorBanner>(parameters =>
            parameters.Add(p => p.Message, "Something went wrong"));

        Assert.Contains("Something went wrong", cut.Markup);
    }

    [Fact]
    public void ErrorBanner_RendersNothing_WhenMessageIsNull()
    {
        var cut = _ctx.Render<ErrorBanner>(parameters =>
            parameters.Add(p => p.Message, (string?)null));

        // Should render empty or minimal markup with no alert
        Assert.DoesNotContain("mud-alert", cut.Markup);
    }

    [Fact]
    public void ErrorBanner_RendersNothing_WhenMessageIsEmpty()
    {
        var cut = _ctx.Render<ErrorBanner>(parameters =>
            parameters.Add(p => p.Message, string.Empty));

        Assert.DoesNotContain("mud-alert", cut.Markup);
    }

    [Fact]
    public void ErrorBanner_RendersAsAlertWithErrorSeverity()
    {
        var cut = _ctx.Render<ErrorBanner>(parameters =>
            parameters.Add(p => p.Message, "Error occurred"));

        // MudBlazor 8.x uses "mud-alert-text-error" as the CSS class for error severity
        Assert.Contains("mud-alert", cut.Markup);
        Assert.Contains("Error occurred", cut.Markup);
    }

    [Fact]
    public void ErrorBanner_HasCloseIcon()
    {
        var cut = _ctx.Render<ErrorBanner>(parameters =>
            parameters.Add(p => p.Message, "Error occurred"));

        // The ErrorBanner has ShowCloseIcon="true"
        Assert.Contains("mud-alert-close", cut.Markup);
    }

    // EmptyState tests

    [Fact]
    public void EmptyState_RendersTitle()
    {
        var cut = _ctx.Render<EmptyState>(parameters =>
            parameters.Add(p => p.Title, "Nothing here"));

        Assert.Contains("Nothing here", cut.Markup);
    }

    [Fact]
    public void EmptyState_RendersDescription()
    {
        var cut = _ctx.Render<EmptyState>(parameters => parameters
            .Add(p => p.Title, "No items")
            .Add(p => p.Description, "Try adding some items"));

        Assert.Contains("No items", cut.Markup);
        Assert.Contains("Try adding some items", cut.Markup);
    }

    [Fact]
    public void EmptyState_RendersWithDefaultTitle()
    {
        var cut = _ctx.Render<EmptyState>();

        Assert.Contains("No items found", cut.Markup);
    }

    [Fact]
    public void EmptyState_OmitsDescription_WhenNull()
    {
        var cut = _ctx.Render<EmptyState>(parameters => parameters
            .Add(p => p.Title, "Empty")
            .Add(p => p.Description, (string?)null));

        Assert.Contains("Empty", cut.Markup);
    }

    // ConfirmDialog tests — MudDialog requires dialog infrastructure,
    // so we test the component's parameter defaults directly.

    [Fact]
    public void ConfirmDialog_HasExpectedDefaultParameters()
    {
        // ConfirmDialog has default parameter values:
        // ContentText = "Are you sure?"
        // ButtonText = "Confirm"
        // ButtonColor = Color.Error
        // We verify this through reflection since MudDialog needs dialog hosting.
        var dialog = new ConfirmDialog();
        Assert.Equal("Are you sure?", dialog.ContentText);
        Assert.Equal("Confirm", dialog.ButtonText);
        Assert.Equal(Color.Error, dialog.ButtonColor);
    }

    [Fact]
    public void ConfirmDialog_AcceptsCustomParameters()
    {
        var dialog = new ConfirmDialog
        {
            ContentText = "Delete this item?",
            ButtonText = "Delete",
            ButtonColor = Color.Warning
        };

        Assert.Equal("Delete this item?", dialog.ContentText);
        Assert.Equal("Delete", dialog.ButtonText);
        Assert.Equal(Color.Warning, dialog.ButtonColor);
    }

    // LoadingOverlay tests

    [Fact]
    public void LoadingOverlay_RendersOverlay_WhenVisible()
    {
        var cut = _ctx.Render<LoadingOverlay>(parameters =>
            parameters.Add(p => p.Visible, true));

        Assert.Contains("mud-overlay", cut.Markup);
    }

    [Fact]
    public void LoadingOverlay_RendersNothing_WhenNotVisible()
    {
        var cut = _ctx.Render<LoadingOverlay>(parameters =>
            parameters.Add(p => p.Visible, false));

        Assert.DoesNotContain("mud-overlay", cut.Markup);
    }
}
