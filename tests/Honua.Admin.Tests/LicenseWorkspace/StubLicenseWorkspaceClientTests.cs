using Honua.Admin.Models.LicenseWorkspace;
using Honua.Admin.Services.LicenseWorkspace;
using Xunit;

namespace Honua.Admin.Tests.LicenseWorkspace;

public sealed class StubLicenseWorkspaceClientTests
{
    [Fact]
    public async Task GetStatusAsync_returns_seeded_status()
    {
        var seed = StubLicenseWorkspaceClient.BuildHealthyEnterprise(DateTimeOffset.UtcNow.AddDays(45));
        var stub = new StubLicenseWorkspaceClient(seed);

        var result = await stub.GetStatusAsync(default);

        Assert.True(result.IsSuccess);
        Assert.Equal(seed.Edition, result.Value!.Edition);
    }

    [Fact]
    public async Task GetStatusAsync_propagates_seeded_error()
    {
        var stub = new StubLicenseWorkspaceClient(StubLicenseWorkspaceClient.BuildHealthyEnterprise(DateTimeOffset.UtcNow.AddDays(45)));
        var error = new LicenseClientError(LicenseClientErrorKind.Transport, "boom");
        stub.SetStatusError(error);

        var result = await stub.GetStatusAsync(default);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public async Task UploadLicenseAsync_records_call_count_and_length_and_replaces_status()
    {
        var stub = new StubLicenseWorkspaceClient(StubLicenseWorkspaceClient.BuildExpired());

        var bytes = new byte[] { 1, 2, 3, 4 };
        var upload = await stub.UploadLicenseAsync(bytes, default);

        Assert.True(upload.IsSuccess);
        Assert.Equal(1, stub.UploadCallCount);
        Assert.Equal(bytes.Length, stub.LastUploadLength);

        var refreshed = await stub.GetStatusAsync(default);
        Assert.True(refreshed.IsSuccess);
        Assert.True(refreshed.Value!.IsValid);
    }

    [Fact]
    public async Task GetEntitlementsAsync_returns_status_entitlements()
    {
        var seed = StubLicenseWorkspaceClient.BuildPerpetualCommunity();
        var stub = new StubLicenseWorkspaceClient(seed);

        var result = await stub.GetEntitlementsAsync(default);

        Assert.True(result.IsSuccess);
        Assert.Equal(seed.Entitlements.Count, result.Value!.Items.Count);
    }
}
