using System;
using System.Linq;
using Honua.Admin.Models.Identity;

namespace Honua.Admin.Components.Identity;

/// <summary>
/// Edit-side projection of the OIDC provider form, reused by the create dialog and
/// the edit dialog. Plaintext <see cref="ClientSecret"/> never leaves this object —
/// the form clears it after submission so it cannot land in any persistence sink.
/// </summary>
public sealed class OidcProviderFormModel
{
    /// <summary>
    /// The catalog of supported provider type identifiers. Mirrors the server-side
    /// <c>OidcProviderConfiguration.ProviderType</c> enum-string values.
    /// </summary>
    public static readonly string[] SupportedProviderTypes =
    {
        "Generic",
        "AzureAd",
        "Okta",
        "Auth0"
    };

    public Guid? ProviderId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ProviderType { get; set; } = SupportedProviderTypes[0];

    public string Authority { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Plaintext secret. Only populated while the form is open; cleared on submit.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether the existing record had a secret configured. Drives the
    /// <c>••••• (set)</c> hint and the rotate-toggle default.
    /// </summary>
    public bool ServerHasSecret { get; set; }

    /// <summary>
    /// In edit mode, the operator opts in to rotating the secret. When false, the
    /// form omits <c>ClientSecret</c> from the update request so the server keeps
    /// the existing value.
    /// </summary>
    public bool RotateSecret { get; set; }

    public bool IsEdit => ProviderId is not null;

    public static OidcProviderFormModel ForCreate() => new();

    public static OidcProviderFormModel ForEdit(OidcProviderResponse provider) => new()
    {
        ProviderId = provider.ProviderId,
        Name = provider.Name,
        ProviderType = provider.ProviderType,
        Authority = provider.Authority,
        ClientId = provider.ClientId,
        Enabled = provider.Enabled,
        // Best-effort heuristic: configured providers nearly always have a secret;
        // honua-server returns no flag for it. Operators can rotate via the toggle.
        ServerHasSecret = true,
        RotateSecret = false,
        ClientSecret = string.Empty
    };

    /// <summary>
    /// Validate the in-memory form against the same constraints honua-server
    /// applies. Returns one error per offending field; the dialog renders these
    /// inline and disables submit while any are present.
    /// </summary>
    public string[] Validate()
    {
        var errors = new System.Collections.Generic.List<string>();
        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("Name is required.");
        }
        else if (Name.Length > 200)
        {
            errors.Add("Name must be 200 characters or fewer.");
        }
        if (string.IsNullOrWhiteSpace(ProviderType))
        {
            errors.Add("Provider type is required.");
        }
        if (string.IsNullOrWhiteSpace(Authority))
        {
            errors.Add("Authority URL is required.");
        }
        else if (!Uri.TryCreate(Authority, UriKind.Absolute, out var uri)
                 || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            errors.Add("Authority must be a valid http(s) URL.");
        }
        if (string.IsNullOrWhiteSpace(ClientId))
        {
            errors.Add("Client ID is required.");
        }
        if (!IsEdit && string.IsNullOrWhiteSpace(ClientSecret))
        {
            errors.Add("Client secret is required when creating a provider.");
        }
        if (IsEdit && RotateSecret && string.IsNullOrWhiteSpace(ClientSecret))
        {
            errors.Add("Enter the new client secret to rotate, or turn off the rotate toggle.");
        }
        return errors.ToArray();
    }

    /// <summary>
    /// Build the create payload sent to honua-server. Plaintext secret is included
    /// because the server has no other way to persist it on first creation.
    /// </summary>
    public CreateOidcProviderRequest ToCreateRequest()
    {
        return new CreateOidcProviderRequest
        {
            Name = Name.Trim(),
            ProviderType = ProviderType,
            Authority = Authority.Trim(),
            ClientId = ClientId.Trim(),
            ClientSecret = ClientSecret,
            Enabled = Enabled
        };
    }

    /// <summary>
    /// Build the update payload. <see cref="UpdateOidcProviderRequest.ClientSecret"/>
    /// is null unless the operator opted in to rotating it; that is the only path
    /// where plaintext leaves the browser during an edit.
    /// </summary>
    public UpdateOidcProviderRequest ToUpdateRequest()
    {
        return new UpdateOidcProviderRequest
        {
            Name = Name.Trim(),
            Authority = Authority.Trim(),
            ClientId = ClientId.Trim(),
            ClientSecret = RotateSecret && !string.IsNullOrEmpty(ClientSecret) ? ClientSecret : null,
            Enabled = Enabled
        };
    }

    /// <summary>
    /// Erase the plaintext secret from in-memory state. Callers must invoke this
    /// after a successful submit so no plaintext outlives the network call.
    /// </summary>
    public void ClearSecret()
    {
        ClientSecret = string.Empty;
    }

    /// <summary>
    /// Hint copy for the secret field. Edit mode renders an obscured indicator
    /// when the server reports a secret is configured; create mode shows nothing.
    /// </summary>
    public string SecretHint =>
        IsEdit && ServerHasSecret ? "••••• (set) — toggle Rotate to replace" : "Required to authenticate operators against the provider.";
}
