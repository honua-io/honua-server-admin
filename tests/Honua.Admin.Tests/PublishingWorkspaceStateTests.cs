using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.Admin;
using Honua.Admin.Services.Admin;
using Honua.Admin.Services.Publishing;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class PublishingWorkspaceStateTests
{
    [Fact]
    public async Task InitializeAsync_loads_connection_service_layers_tables_and_preflight()
    {
        var state = new PublishingWorkspaceState(new StubHonuaAdminClient());

        await state.InitializeAsync();

        Assert.Equal(PublishingWorkspaceStatus.Idle, state.Status);
        Assert.NotEmpty(state.Connections);
        Assert.NotEmpty(state.Services);
        Assert.NotEmpty(state.Tables);
        Assert.NotEmpty(state.Layers);
        Assert.NotNull(state.DeployPreflight);
        Assert.All(state.ValidationChecks, check => Assert.True(check.Passed, check.Message));
    }

    [Fact]
    public async Task ToggleProtocol_and_SaveProtocolsAsync_persist_desired_protocols()
    {
        var client = new RecordingPublishingClient();
        var state = new PublishingWorkspaceState(client);
        await state.InitializeAsync();

        state.ToggleProtocol("OData", enabled: true);
        await state.SaveProtocolsAsync();

        Assert.Contains("OData", client.LastProtocols);
        Assert.Equal("In sync", state.ProtocolDriftLabel);
    }

    [Fact]
    public async Task PublishAsync_maps_draft_to_publish_request()
    {
        var client = new RecordingPublishingClient();
        var state = new PublishingWorkspaceState(client);
        await state.InitializeAsync();
        state.UseTable(new DiscoveredTable
        {
            Schema = "gis",
            Table = "trails",
            GeometryColumn = "shape",
            GeometryType = "LineString",
            Srid = 4326
        });
        state.SetDraftLayerName("Trails");
        state.SetDraftDescription("Trail network");

        var layer = await state.PublishAsync();

        Assert.NotNull(layer);
        Assert.NotNull(client.LastPublishRequest);
        Assert.Equal("gis", client.LastPublishRequest!.Schema);
        Assert.Equal("trails", client.LastPublishRequest.Table);
        Assert.Equal("Trails", client.LastPublishRequest.LayerName);
        Assert.Equal("shape", client.LastPublishRequest.GeometryColumn);
        Assert.Equal("default", client.LastPublishRequest.ServiceName);
    }

    [Fact]
    public async Task PublishAsync_without_table_surfaces_validation_error()
    {
        var state = new PublishingWorkspaceState(new EmptyTableClient());
        await state.InitializeAsync();

        var layer = await state.PublishAsync();

        Assert.Null(layer);
        Assert.Contains("Select a table", state.LastError, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingPublishingClient : StubHonuaAdminClient
    {
        public PublishLayerRequest? LastPublishRequest { get; private set; }
        public IReadOnlyList<string> LastProtocols { get; private set; } = Array.Empty<string>();

        public override Task<LayerSummary> PublishLayerAsync(string connectionId, PublishLayerRequest request, CancellationToken cancellationToken)
        {
            LastPublishRequest = request;
            return Task.FromResult(new LayerSummary
            {
                LayerId = 202,
                LayerName = request.LayerName,
                Schema = request.Schema,
                Table = request.Table,
                GeometryType = request.GeometryType ?? string.Empty,
                Srid = request.Srid ?? 4326,
                Enabled = request.Enabled,
                ServiceName = request.ServiceName ?? "default"
            });
        }

        public override Task<ServiceSettings> UpdateServiceProtocolsAsync(string serviceName, UpdateProtocolsRequest request, CancellationToken cancellationToken)
        {
            LastProtocols = request.EnabledProtocols;
            return Task.FromResult(ServiceSettings with { EnabledProtocols = request.EnabledProtocols });
        }
    }

    private sealed class EmptyTableClient : StubHonuaAdminClient
    {
        public override Task<IReadOnlyList<DiscoveredTable>> DiscoverConnectionTablesAsync(string connectionId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DiscoveredTable>>(Array.Empty<DiscoveredTable>());
    }
}
