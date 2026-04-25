using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.Identity;

namespace Honua.Admin.Services.Identity;

/// <summary>
/// Seam between the admin UI and honua-server's identity admin endpoints. Mirrors
/// the <see cref="Honua.Admin.Services.SpecWorkspace.ISpecWorkspaceClient"/> seam
/// so a different transport (mocked, gRPC, contract-test) can be DI-swapped without
/// touching the page layer.
/// </summary>
public interface IIdentityAdminClient
{
    /// <summary>
    /// List all configured OIDC providers.
    /// </summary>
    Task<IReadOnlyList<OidcProviderResponse>> ListOidcProvidersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Fetch a single OIDC provider by id.
    /// </summary>
    Task<OidcProviderResponse?> GetOidcProviderAsync(Guid providerId, CancellationToken cancellationToken);

    /// <summary>
    /// Create a new OIDC provider. The plaintext <see cref="CreateOidcProviderRequest.ClientSecret"/>
    /// is sent on the wire and never round-tripped back.
    /// </summary>
    Task<OidcProviderResponse> CreateOidcProviderAsync(CreateOidcProviderRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Update an existing OIDC provider. Callers omit fields they don't want to
    /// change; <see cref="UpdateOidcProviderRequest.ClientSecret"/> stays write-only.
    /// </summary>
    Task<OidcProviderResponse> UpdateOidcProviderAsync(Guid providerId, UpdateOidcProviderRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Delete an OIDC provider.
    /// </summary>
    Task DeleteOidcProviderAsync(Guid providerId, CancellationToken cancellationToken);

    /// <summary>
    /// Run a one-click reachability test against a configured OIDC provider.
    /// </summary>
    Task<OidcProviderTestResponse> TestOidcProviderAsync(Guid providerId, CancellationToken cancellationToken);

    /// <summary>
    /// List the configured OIDC catalog provider statuses (Generic, Google, AzureAd).
    /// </summary>
    Task<IdentityProvidersResponse> GetIdentityProvidersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Run a one-click reachability test against a catalog provider type. Surfaces
    /// the discovery URL plus a structured error mode.
    /// </summary>
    Task<IdentityProviderTestResult> TestIdentityProviderAsync(string providerType, CancellationToken cancellationToken);
}
