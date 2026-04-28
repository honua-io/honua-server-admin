using System.IO;
using Xunit;

namespace Honua.Admin.Tests;

/// <summary>
/// Light text-based assertions on NavMenu.razor - confirms operator workspaces
/// live in the same shared shell rather than parallel layouts.
/// </summary>
public sealed class NavMenuTests
{
    private const string NavMenuPath = "../../../../../src/Honua.Admin/Shared/NavMenu.razor";

    [Fact]
    public void NavMenu_registers_spatial_sql_entry_under_operator_route()
    {
        var path = Path.Combine(System.AppContext.BaseDirectory, NavMenuPath);
        var contents = File.ReadAllText(path);

        Assert.Contains("/operator/sql", contents, System.StringComparison.Ordinal);
        Assert.Contains("Spatial SQL", contents, System.StringComparison.Ordinal);
    }

    [Fact]
    public void NavMenu_registers_control_center_under_operator_route()
    {
        var path = Path.Combine(System.AppContext.BaseDirectory, NavMenuPath);
        var contents = File.ReadAllText(path);

        Assert.Contains("/operator/control-center", contents, System.StringComparison.Ordinal);
        Assert.Contains("Control center", contents, System.StringComparison.Ordinal);
    }

    [Fact]
    public void NavMenu_registers_admin_readiness_under_operator_route()
    {
        var path = Path.Combine(System.AppContext.BaseDirectory, NavMenuPath);
        var contents = File.ReadAllText(path);

        Assert.Contains("/operator/admin-readiness", contents, System.StringComparison.Ordinal);
        Assert.Contains("Admin readiness", contents, System.StringComparison.Ordinal);
    }

    [Fact]
    public void NavMenu_registers_annotation_workspace_under_operator_route()
    {
        var path = Path.Combine(System.AppContext.BaseDirectory, NavMenuPath);
        var contents = File.ReadAllText(path);

        Assert.Contains("/operator/annotations", contents, System.StringComparison.Ordinal);
        Assert.Contains("Map annotations", contents, System.StringComparison.Ordinal);
    }

    [Fact]
    public void NavMenu_registers_publishing_workspace_under_operator_route()
    {
        var path = Path.Combine(System.AppContext.BaseDirectory, NavMenuPath);
        var contents = File.ReadAllText(path);

        Assert.Contains("/operator/publishing", contents, System.StringComparison.Ordinal);
        Assert.Contains("Publishing", contents, System.StringComparison.Ordinal);
    }

    [Fact]
    public void NavMenu_registers_usage_analytics_under_operator_route()
    {
        var path = Path.Combine(System.AppContext.BaseDirectory, NavMenuPath);
        var contents = File.ReadAllText(path);

        Assert.Contains("/operator/analytics", contents, System.StringComparison.Ordinal);
        Assert.Contains("Usage analytics", contents, System.StringComparison.Ordinal);
    }

    [Fact]
    public void NavMenu_registers_print_service_under_operator_route()
    {
        var path = Path.Combine(System.AppContext.BaseDirectory, NavMenuPath);
        var contents = File.ReadAllText(path);

        Assert.Contains("/operator/print", contents, System.StringComparison.Ordinal);
        Assert.Contains("Print service", contents, System.StringComparison.Ordinal);
    }

    [Fact]
    public void NavMenu_registers_app_builder_under_operator_route()
    {
        var path = Path.Combine(System.AppContext.BaseDirectory, NavMenuPath);
        var contents = File.ReadAllText(path);

        Assert.Contains("/operator/app-builder", contents, System.StringComparison.Ordinal);
        Assert.Contains("App builder", contents, System.StringComparison.Ordinal);
    }

    [Fact]
    public void NavMenu_uses_a_single_MudNavMenu_so_sql_page_lives_in_the_shared_shell()
    {
        var path = Path.Combine(System.AppContext.BaseDirectory, NavMenuPath);
        var contents = File.ReadAllText(path);

        // The contract requires the page to live in the shared spec-editor shell,
        // not a parallel layout — this asserts there's only one MudNavMenu in the
        // shared NavMenu, with both spec workspace and spatial SQL inside it.
        var open = System.Text.RegularExpressions.Regex.Matches(contents, "<MudNavMenu").Count;
        Assert.Equal(1, open);

        var operatorControlCenter = contents.IndexOf("/operator/control-center", System.StringComparison.Ordinal);
        var operatorAdminReadiness = contents.IndexOf("/operator/admin-readiness", System.StringComparison.Ordinal);
        var operatorSpec = contents.IndexOf("/operator/spec", System.StringComparison.Ordinal);
        var operatorSql = contents.IndexOf("/operator/sql", System.StringComparison.Ordinal);
        var operatorAnnotations = contents.IndexOf("/operator/annotations", System.StringComparison.Ordinal);
        var operatorPublishing = contents.IndexOf("/operator/publishing", System.StringComparison.Ordinal);
        var operatorAnalytics = contents.IndexOf("/operator/analytics", System.StringComparison.Ordinal);
        var operatorAppBuilder = contents.IndexOf("/operator/app-builder", System.StringComparison.Ordinal);
        var operatorPrint = contents.IndexOf("/operator/print", System.StringComparison.Ordinal);
        var menuClose = contents.IndexOf("</MudNavMenu>", System.StringComparison.Ordinal);

        Assert.True(operatorControlCenter > 0);
        Assert.True(operatorAdminReadiness > 0);
        Assert.True(operatorSpec > 0);
        Assert.True(operatorSql > 0);
        Assert.True(operatorAnnotations > 0);
        Assert.True(operatorPublishing > 0);
        Assert.True(operatorAnalytics > 0);
        Assert.True(operatorAppBuilder > 0);
        Assert.True(operatorPrint > 0);
        Assert.True(operatorControlCenter < menuClose);
        Assert.True(operatorAdminReadiness < menuClose);
        Assert.True(operatorSql < menuClose);
        Assert.True(operatorAnnotations < menuClose);
        Assert.True(operatorPublishing < menuClose);
        Assert.True(operatorAnalytics < menuClose);
        Assert.True(operatorAppBuilder < menuClose);
        Assert.True(operatorPrint < menuClose);
    }
}
