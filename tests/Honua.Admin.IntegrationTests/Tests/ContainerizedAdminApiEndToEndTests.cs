// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.IntegrationTests.Fixtures;
using Honua.Admin.Models.Admin;
using Xunit;

namespace Honua.Admin.IntegrationTests.Tests;

/// <summary>
/// Docker-backed E2E coverage for issue #19. The test is opt-in until the
/// repo has a stable Honua Server image configured in CI.
/// </summary>
public sealed class ContainerizedAdminApiEndToEndTests
{
    private static readonly TimeSpan ContainerRunBudget = TimeSpan.FromMinutes(4);

    [Fact]
    [Trait("Category", "ContainerE2E")]
    public async Task HonuaServerContainer_ExercisesAdminReadinessAndPostgisPublishingFlow()
    {
        if (!ContainerizedHonuaServerFixture.IsEnabled)
        {
            return;
        }

        using var timeout = new CancellationTokenSource(ContainerRunBudget);
        var cancellationToken = timeout.Token;
        await using var fixture = await ContainerizedHonuaServerFixture.StartAsync(cancellationToken);
        await fixture.SeedSpatialCatalogAsync(cancellationToken);

        var overview = await fixture.AdminClient.GetFeatureOverviewAsync(cancellationToken);
        var configuration = await fixture.AdminClient.GetConfigurationSummaryAsync(cancellationToken);
        var configurationDocument = await fixture.AdminClient.GetConfigurationDocumentAsync(cancellationToken);
        var openApi = await fixture.AdminClient.GetAdminOpenApiAsync(cancellationToken);
        var telemetry = await fixture.AdminClient.GetTelemetryStatusAsync(cancellationToken);
        var migrations = await fixture.AdminClient.GetMigrationStatusAsync(cancellationToken);
        var recentErrors = await fixture.AdminClient.GetRecentErrorsAsync(cancellationToken);
        var deployPreflight = await fixture.AdminClient.GetDeployPreflightAsync(cancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(fixture.BaseUrl));
        Assert.NotNull(overview);
        Assert.NotEmpty(overview.CurrentEdition);
        Assert.NotEmpty(overview.Features);
        Assert.False(string.IsNullOrWhiteSpace(configuration.Environment));
        Assert.True(configuration.TotalTypes >= 0);
        Assert.True(configurationDocument.ValueKind is JsonValueKind.Object or JsonValueKind.Array);
        Assert.Equal(JsonValueKind.Object, openApi.ValueKind);
        Assert.NotNull(telemetry);
        Assert.NotNull(migrations);
        Assert.NotNull(recentErrors);
        Assert.NotNull(deployPreflight);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var connection = await CreateTestConnectionAsync(fixture, suffix, cancellationToken);
        var connectionId = connection.Id;

        try
        {
            var detail = await fixture.AdminClient.GetConnectionAsync(connectionId, cancellationToken);
            var draftHealth = await fixture.AdminClient.TestDraftConnectionAsync(
                fixture.CreatePostgisConnectionRequest($"admin-e2e-draft-{suffix}"),
                cancellationToken);
            var savedHealth = await fixture.AdminClient.TestConnectionAsync(connectionId, cancellationToken);
            var encryption = await fixture.AdminClient.ValidateEncryptionAsync(cancellationToken);
            var tables = await fixture.AdminClient.DiscoverConnectionTablesAsync(connectionId, cancellationToken);

            Assert.Equal(connectionId, detail.Id);
            Assert.True(draftHealth.IsHealthy, draftHealth.Message);
            Assert.True(savedHealth.IsHealthy, savedHealth.Message);
            Assert.True(encryption.IsValid, encryption.Message);
            Assert.Contains(tables, table =>
                table.Schema == fixture.SeedSchema &&
                table.Table == fixture.SeedTable &&
                table.GeometryColumn == "geom");

            var layer = await fixture.AdminClient.PublishLayerAsync(
                connectionId,
                new PublishLayerRequest
                {
                    Schema = fixture.SeedSchema,
                    Table = fixture.SeedTable,
                    LayerName = $"Admin E2E Parcels {suffix}",
                    Description = "Layer published by the container E2E lane.",
                    GeometryColumn = "geom",
                    GeometryType = "Polygon",
                    Srid = 4326,
                    PrimaryKey = "id",
                    Fields = ["id", "name", "category"],
                    ServiceName = "default",
                    Enabled = true,
                },
                cancellationToken);

            Assert.True(layer.LayerId > 0);
            Assert.Equal(fixture.SeedTable, layer.Table);
            Assert.True(layer.Enabled);

            var listedLayers = await fixture.AdminClient.ListLayersAsync(connectionId, layer.ServiceName, cancellationToken);
            Assert.Contains(listedLayers, listed => listed.LayerId == layer.LayerId);

            var disabledLayer = await fixture.AdminClient.SetLayerEnabledAsync(connectionId, layer.LayerId, false, layer.ServiceName, cancellationToken);
            Assert.False(disabledLayer.Enabled);

            var enabledLayers = await fixture.AdminClient.SetServiceLayersEnabledAsync(connectionId, true, layer.ServiceName, cancellationToken);
            Assert.Contains(enabledLayers, enabled => enabled.LayerId == layer.LayerId && enabled.Enabled);

            var existingStyle = await fixture.AdminClient.GetLayerStyleAsync(layer.LayerId, cancellationToken);
            Assert.NotNull(existingStyle);

            var updatedStyle = await fixture.AdminClient.UpdateLayerStyleAsync(
                layer.LayerId,
                new LayerStyleUpdateRequest
                {
                    MapLibreStyle = BuildPolygonMapLibreStyle(layer.LayerId, layer.LayerName),
                },
                cancellationToken);
            Assert.NotNull(updatedStyle);

            var services = await fixture.AdminClient.ListServicesAsync(cancellationToken);
            Assert.Contains(services, service => service.ServiceName == layer.ServiceName);

            var settings = await fixture.AdminClient.GetServiceSettingsAsync(layer.ServiceName, cancellationToken);
            IReadOnlyList<string> protocols = settings.EnabledProtocols.Count == 0
                ? ["FeatureServer"]
                : settings.EnabledProtocols;
            var protocolUpdate = await fixture.AdminClient.UpdateServiceProtocolsAsync(
                layer.ServiceName,
                new UpdateProtocolsRequest { EnabledProtocols = protocols },
                cancellationToken);
            Assert.NotEmpty(protocolUpdate.EnabledProtocols);

            var mapServer = await fixture.AdminClient.UpdateServiceMapServerAsync(
                layer.ServiceName,
                settings.MapServer with
                {
                    DefaultImageWidth = settings.MapServer.DefaultImageWidth ?? 1024,
                    DefaultImageHeight = settings.MapServer.DefaultImageHeight ?? 768,
                },
                cancellationToken);
            Assert.Equal(layer.ServiceName, mapServer.ServiceName);

            var accessPolicy = await fixture.AdminClient.UpdateServiceAccessPolicyAsync(
                layer.ServiceName,
                settings.AccessPolicy ?? new AccessPolicySettings { AllowAnonymous = true, AllowAnonymousWrite = false },
                cancellationToken);
            Assert.Equal(layer.ServiceName, accessPolicy.ServiceName);

            var timeInfo = await fixture.AdminClient.UpdateServiceTimeInfoAsync(
                layer.ServiceName,
                settings.TimeInfo ?? new TimeInfoSettings(),
                cancellationToken);
            Assert.Equal(layer.ServiceName, timeInfo.ServiceName);

            var metadata = await fixture.AdminClient.UpdateServiceLayerMetadataAsync(
                layer.ServiceName,
                layer.LayerId,
                new UpdateLayerMetadataRequest
                {
                    AccessPolicy = accessPolicy.AccessPolicy,
                    TimeInfo = timeInfo.TimeInfo,
                },
                cancellationToken);
            Assert.Equal(layer.LayerId, metadata.LayerId);
        }
        finally
        {
            await TryDeleteConnectionAsync(fixture, connectionId);
        }
    }

    private static async Task<ConnectionSummary> CreateTestConnectionAsync(
        ContainerizedHonuaServerFixture fixture,
        string suffix,
        CancellationToken cancellationToken)
    {
        var createRequest = fixture.CreatePostgisConnectionRequest($"admin-e2e-{suffix}");
        ConnectionSummary? created = null;

        try
        {
            created = await fixture.AdminClient.CreateConnectionAsync(createRequest, cancellationToken);
            var updated = await fixture.AdminClient.UpdateConnectionAsync(
                created.Id,
                new UpdateConnectionRequest
                {
                    Description = "Updated by the container E2E lane.",
                    IsActive = true,
                    SslRequired = false,
                    SslMode = "Disable",
                },
                cancellationToken);
            var connections = await fixture.AdminClient.ListConnectionsAsync(cancellationToken);

            Assert.Contains(connections, candidate => candidate.Id == updated.Id);
            Assert.Equal(createRequest.Name, updated.Name);

            return updated;
        }
        catch
        {
            if (created is not null)
            {
                await TryDeleteConnectionAsync(fixture, created.Id);
            }

            throw;
        }
    }

    private static async Task TryDeleteConnectionAsync(ContainerizedHonuaServerFixture fixture, string connectionId)
    {
        try
        {
            await fixture.AdminClient.DeleteConnectionAsync(connectionId, CancellationToken.None);
        }
        catch (HttpRequestException)
        {
            // A published layer can make connection deletion fail on current
            // server images. Cleanup is best-effort so this admin-client E2E
            // lane reports the lifecycle assertion that failed, not teardown.
        }
    }

    private static JsonElement BuildPolygonMapLibreStyle(int layerId, string name)
        => Json(
            $$"""
            {
              "version": 8,
              "name": "{{name}}",
              "sources": {
                "layer-{{layerId}}": {
                  "type": "vector",
                  "tiles": ["/tiles/{{layerId}}/{z}/{x}/{y}.mvt"],
                  "minzoom": 0,
                  "maxzoom": 22
                }
              },
              "layers": [
                {
                  "id": "layer-{{layerId}}-fill",
                  "type": "fill",
                  "source": "layer-{{layerId}}",
                  "source-layer": "layer",
                  "paint": {
                    "fill-color": "#2D69A5",
                    "fill-opacity": 0.4
                  }
                },
                {
                  "id": "layer-{{layerId}}-outline",
                  "type": "line",
                  "source": "layer-{{layerId}}",
                  "source-layer": "layer",
                  "paint": {
                    "line-color": "#1A4D80",
                    "line-width": 0.75,
                    "line-opacity": 0.8
                  }
                }
              ]
            }
            """);

    private static JsonElement Json(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.Clone();
    }
}
