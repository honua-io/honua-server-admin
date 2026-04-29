using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.LicenseWorkspace;
using Honua.Sdk.Admin.Models;

namespace Honua.Admin.Services.LicenseWorkspace;

/// <summary>
/// Seam between the admin license workspace and honua-server's licensing
/// surface. All operations return discriminated results; callers should never
/// need to catch transport exceptions to render diagnostics.
/// </summary>
public interface ILicenseWorkspaceClient
{
    Task<LicenseClientResult<LicenseStatusResponse>> GetStatusAsync(CancellationToken cancellationToken);

    Task<LicenseClientResult<EntitlementListBox>> GetEntitlementsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replace the active license. <paramref name="bytes"/> are sent
    /// <c>application/octet-stream</c>; the implementation does not retain
    /// them after the call. The successful result contains the metadata-only
    /// status returned by the server, never the uploaded bytes.
    /// </summary>
    Task<LicenseClientResult<LicenseStatusResponse>> UploadLicenseAsync(byte[] bytes, CancellationToken cancellationToken);
}

/// <summary>
/// Boxing wrapper so <see cref="LicenseClientResult{T}"/> (which constrains
/// <c>T : class</c>) can carry the entitlement collection. A bare
/// <c>IReadOnlyList&lt;LicenseEntitlement&gt;</c> is structurally a class but the
/// generic constraint chokes on the interface.
/// </summary>
public sealed class EntitlementListBox
{
    public required IReadOnlyList<LicenseEntitlement> Items { get; init; }
}
