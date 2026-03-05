using Honua.Admin.Models;

namespace Honua.Admin.Services;

/// <summary>
/// Service for deploying forms to backend.
/// </summary>
public interface IFormDeploymentService
{
    /// <summary>
    /// Deploys a form and returns backend status.
    /// </summary>
    Task<FormDeploymentResult> DeployAsync(XlsForm form, CancellationToken cancellationToken = default);
}
