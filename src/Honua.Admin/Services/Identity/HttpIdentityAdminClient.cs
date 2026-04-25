using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.Identity;

namespace Honua.Admin.Services.Identity;

/// <summary>
/// Default <see cref="IIdentityAdminClient"/>. Calls honua-server's identity admin
/// endpoints over a typed <see cref="HttpClient"/>, wrapping every response in the
/// shared <see cref="ApiResponse{T}"/> envelope. Auth header (<c>X-API-Key</c>) is
/// preconfigured by the DI registration in <c>Program.cs</c>.
/// </summary>
public sealed class HttpIdentityAdminClient : IIdentityAdminClient
{
    private const string OidcProvidersPath = "api/v1/admin/oidc/providers";
    private const string IdentityProvidersPath = "api/v1/admin/identity/providers";

    private readonly HttpClient _http;
    private readonly IIdentityAdminTelemetry _telemetry;

    public HttpIdentityAdminClient(HttpClient http, IIdentityAdminTelemetry telemetry)
    {
        _http = http;
        _telemetry = telemetry;
    }

    public async Task<IReadOnlyList<OidcProviderResponse>> ListOidcProvidersAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var envelope = await _http.GetFromJsonAsync(
                OidcProvidersPath,
                IdentityAdminJsonContext.Default.ApiResponseListOidcProviderResponse,
                cancellationToken).ConfigureAwait(false);

            var providers = envelope?.Data ?? new List<OidcProviderResponse>();
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
            using var response = await _http
                .GetAsync($"{OidcProvidersPath}/{providerId:D}", cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                stopwatch.Stop();
                _telemetry.RecordLatency("identity_admin_get_oidc_provider", stopwatch.ElapsedMilliseconds, new Dictionary<string, object?>
                {
                    ["provider_id"] = providerId,
                    ["found"] = false
                });
                return null;
            }

            response.EnsureSuccessStatusCode();
            var envelope = await response.Content
                .ReadFromJsonAsync(IdentityAdminJsonContext.Default.ApiResponseOidcProviderResponse, cancellationToken)
                .ConfigureAwait(false);

            stopwatch.Stop();
            _telemetry.RecordLatency("identity_admin_get_oidc_provider", stopwatch.ElapsedMilliseconds, new Dictionary<string, object?>
            {
                ["provider_id"] = providerId,
                ["found"] = envelope?.Data is not null
            });
            return envelope?.Data;
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
            using var response = await _http
                .PostAsJsonAsync(OidcProvidersPath, request, IdentityAdminJsonContext.Default.CreateOidcProviderRequest, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var envelope = await response.Content
                .ReadFromJsonAsync(IdentityAdminJsonContext.Default.ApiResponseOidcProviderResponse, cancellationToken)
                .ConfigureAwait(false);
            var created = envelope?.Data
                ?? throw new InvalidOperationException("Server returned a successful response with no provider payload.");

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
            using var response = await _http
                .PutAsJsonAsync($"{OidcProvidersPath}/{providerId:D}", request, IdentityAdminJsonContext.Default.UpdateOidcProviderRequest, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var envelope = await response.Content
                .ReadFromJsonAsync(IdentityAdminJsonContext.Default.ApiResponseOidcProviderResponse, cancellationToken)
                .ConfigureAwait(false);
            var updated = envelope?.Data
                ?? throw new InvalidOperationException("Server returned a successful response with no provider payload.");

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
            using var response = await _http
                .DeleteAsync($"{OidcProvidersPath}/{providerId:D}", cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

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
            using var response = await _http
                .PostAsync($"{OidcProvidersPath}/{providerId:D}/test", content: null, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var envelope = await response.Content
                .ReadFromJsonAsync(IdentityAdminJsonContext.Default.ApiResponseOidcProviderTestResponse, cancellationToken)
                .ConfigureAwait(false);
            var result = envelope?.Data
                ?? throw new InvalidOperationException("Server returned a successful response with no test payload.");

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
            var envelope = await _http.GetFromJsonAsync(
                IdentityProvidersPath,
                IdentityAdminJsonContext.Default.ApiResponseIdentityProvidersResponse,
                cancellationToken).ConfigureAwait(false);

            var providers = envelope?.Data ?? new IdentityProvidersResponse
            {
                Enabled = false,
                Providers = Array.Empty<IdentityProviderStatus>()
            };

            stopwatch.Stop();
            _telemetry.RecordLatency("identity_admin_get_providers", stopwatch.ElapsedMilliseconds, new Dictionary<string, object?>
            {
                ["count"] = providers.Providers.Length
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
        if (string.IsNullOrWhiteSpace(providerType))
        {
            throw new ArgumentException("Provider type must be supplied.", nameof(providerType));
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var envelope = await _http.GetFromJsonAsync(
                $"{IdentityProvidersPath}/{Uri.EscapeDataString(providerType)}/test",
                IdentityAdminJsonContext.Default.ApiResponseIdentityProviderTestResult,
                cancellationToken).ConfigureAwait(false);

            var result = envelope?.Data
                ?? throw new InvalidOperationException("Server returned a successful response with no test payload.");

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
