// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Honua.Sdk.Admin.Models;
using SdkAdminClient = Honua.Sdk.Admin.IHonuaAdminClient;

namespace Honua.Admin.Services.Identity;

/// <summary>
/// UI adapter over the reusable Honua .NET SDK admin identity client.
/// </summary>
public sealed class SdkIdentityAdminClient : IIdentityAdminClient
{
    private readonly SdkAdminClient _client;
    private readonly IIdentityAdminTelemetry _telemetry;

    public SdkIdentityAdminClient(SdkAdminClient client, IIdentityAdminTelemetry telemetry)
    {
        _client = client;
        _telemetry = telemetry;
    }

    public async Task<IReadOnlyList<OidcProviderResponse>> ListOidcProvidersAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var providers = await _client.ListOidcProvidersAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            _telemetry.RecordLatency("identity_admin_list_oidc_providers", stopwatch.ElapsedMilliseconds, new Dictionary<string, object?>
            {
                ["count"] = providers.Count
            });
            return providers;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _telemetry.RecordError("identity_admin_list_oidc_providers", ex, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<OidcProviderResponse?> GetOidcProviderAsync(Guid providerId, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var provider = await _client.GetOidcProviderAsync(providerId, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            _telemetry.RecordLatency("identity_admin_get_oidc_provider", stopwatch.ElapsedMilliseconds, new Dictionary<string, object?>
            {
                ["provider_id"] = providerId,
                ["found"] = provider is not null
            });
            return provider;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _telemetry.RecordError("identity_admin_get_oidc_provider", ex, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<OidcProviderResponse> CreateOidcProviderAsync(CreateOidcProviderRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var created = await _client.CreateOidcProviderAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            _telemetry.Record("identity_admin_create_oidc_provider", new Dictionary<string, object?>
            {
                ["provider_id"] = created.ProviderId,
                ["provider_type"] = created.ProviderType,
                ["elapsed_ms"] = stopwatch.ElapsedMilliseconds
            });
            return created;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _telemetry.RecordError("identity_admin_create_oidc_provider", ex, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<OidcProviderResponse> UpdateOidcProviderAsync(Guid providerId, UpdateOidcProviderRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var updated = await _client.UpdateOidcProviderAsync(providerId, request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            _telemetry.Record("identity_admin_update_oidc_provider", new Dictionary<string, object?>
            {
                ["provider_id"] = providerId,
                ["secret_rotated"] = !string.IsNullOrEmpty(request.ClientSecret),
                ["elapsed_ms"] = stopwatch.ElapsedMilliseconds
            });
            return updated;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _telemetry.RecordError("identity_admin_update_oidc_provider", ex, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task DeleteOidcProviderAsync(Guid providerId, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _client.DeleteOidcProviderAsync(providerId, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            _telemetry.Record("identity_admin_delete_oidc_provider", new Dictionary<string, object?>
            {
                ["provider_id"] = providerId,
                ["elapsed_ms"] = stopwatch.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _telemetry.RecordError("identity_admin_delete_oidc_provider", ex, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<OidcProviderTestResponse> TestOidcProviderAsync(Guid providerId, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await _client.TestOidcProviderAsync(providerId, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            _telemetry.RecordLatency("identity_admin_test_oidc_provider", stopwatch.ElapsedMilliseconds, new Dictionary<string, object?>
            {
                ["provider_id"] = providerId,
                ["is_reachable"] = result.IsReachable
            });
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _telemetry.RecordError("identity_admin_test_oidc_provider", ex, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<IdentityProvidersResponse> GetIdentityProvidersAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var providers = await _client.GetIdentityProvidersAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            _telemetry.RecordLatency("identity_admin_get_providers", stopwatch.ElapsedMilliseconds, new Dictionary<string, object?>
            {
                ["count"] = providers.Providers.Count
            });
            return providers;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _telemetry.RecordError("identity_admin_get_providers", ex, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<IdentityProviderTestResult> TestIdentityProviderAsync(string providerType, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await _client.TestIdentityProviderAsync(providerType, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            _telemetry.RecordLatency("identity_admin_test_provider", stopwatch.ElapsedMilliseconds, new Dictionary<string, object?>
            {
                ["provider_type"] = providerType,
                ["is_reachable"] = result.IsReachable
            });
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _telemetry.RecordError("identity_admin_test_provider", ex, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
