using System.Net.Http.Json;
using System.Text.Json;
using Honua.Admin.Configuration;
using Honua.Admin.Models;
using Microsoft.Extensions.Options;

namespace Honua.Admin.Services;

/// <summary>
/// HTTP-based deployment service.
/// </summary>
public sealed class FormDeploymentService : IFormDeploymentService
{
    private readonly HttpClient _httpClient;
    private readonly HonuaAdminOptions _options;
    private readonly ILogger<FormDeploymentService> _logger;

    /// <summary>
    /// Constructor.
    /// </summary>
    public FormDeploymentService(
        HttpClient httpClient,
        IOptions<HonuaAdminOptions> options,
        ILogger<FormDeploymentService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FormDeploymentResult> DeployAsync(XlsForm form, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(form);
        var settings = form.Settings;

        if (string.IsNullOrWhiteSpace(_options.DeployEndpoint))
        {
            return FormDeploymentResult.Failure("Deployment endpoint is not configured.");
        }

        if (settings is null || string.IsNullOrWhiteSpace(settings.FormId))
        {
            return FormDeploymentResult.Failure("Form settings are incomplete. Form ID is required.");
        }

        var payload = new DeployFormRequest
        {
            Name = form.Name,
            Description = form.Description,
            Version = form.Version,
            FormId = settings.FormId,
            XFormsXml = form.XFormsXml
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                _options.DeployEndpoint,
                payload,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Deployment endpoint rejected request with status {StatusCode}",
                    (int)response.StatusCode);
                return FormDeploymentResult.Failure("Deployment failed. Please verify server status and try again.");
            }

            DeployFormResponse? deployResponse = null;
            try
            {
                deployResponse = await response.Content.ReadFromJsonAsync<DeployFormResponse>(
                    cancellationToken: cancellationToken);
            }
            catch (JsonException jsonException)
            {
                _logger.LogWarning(jsonException, "Deployment succeeded but response payload was not JSON.");
            }

            var successMessage = string.IsNullOrWhiteSpace(deployResponse?.Message)
                ? "Form deployed successfully."
                : deployResponse.Message;

            return FormDeploymentResult.Success(successMessage!, deployResponse?.DeploymentId);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Deployment request failed due to network or server error.");
            return FormDeploymentResult.Failure("Unable to reach deployment service. Please try again.");
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Deployment request timed out.");
            return FormDeploymentResult.Failure("Deployment timed out. Please try again.");
        }
    }

    private sealed class DeployFormRequest
    {
        public string Name { get; init; } = string.Empty;

        public string? Description { get; init; }

        public string Version { get; init; } = string.Empty;

        public string FormId { get; init; } = string.Empty;

        public string? XFormsXml { get; init; }
    }

    private sealed class DeployFormResponse
    {
        public string? DeploymentId { get; init; }

        public string? Message { get; init; }
    }
}
