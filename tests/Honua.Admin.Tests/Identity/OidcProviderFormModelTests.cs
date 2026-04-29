using System;
using Honua.Admin.Components.Identity;
using Honua.Sdk.Admin.Models;
using Xunit;

namespace Honua.Admin.Tests.Identity;

/// <summary>
/// Pin the secret-handling contract for the shared create/edit form. The risk
/// these tests prevent is plaintext secret material leaking out of the form
/// model into network or persistence sinks when it shouldn't.
/// </summary>
public sealed class OidcProviderFormModelTests
{
    [Fact]
    public void Create_request_includes_plaintext_secret_exactly_once()
    {
        var model = OidcProviderFormModel.ForCreate();
        model.Name = "Acme IdP";
        model.ProviderType = "Generic";
        model.Authority = "https://idp.example";
        model.ClientId = "honua-admin";
        model.ClientSecret = "s3cret";

        Assert.Empty(model.Validate());
        var request = model.ToCreateRequest();

        Assert.Equal("s3cret", request.ClientSecret);
        Assert.Equal("honua-admin", request.ClientId);
        Assert.True(request.Enabled);
    }

    [Fact]
    public void Create_without_secret_validates_and_sends_null_to_server()
    {
        // honua-server's CreateOidcProviderRequest.ClientSecret is nullable to
        // allow public / PKCE-style providers. The admin form must not be
        // stricter than the API it mirrors.
        var model = OidcProviderFormModel.ForCreate();
        model.Name = "Public PKCE IdP";
        model.ProviderType = "Okta";
        model.Authority = "https://dev-12345.okta.com";
        model.ClientId = "public-spa";
        // ClientSecret deliberately left blank.

        Assert.Empty(model.Validate());
        var request = model.ToCreateRequest();

        Assert.Null(request.ClientSecret);
        Assert.Equal("public-spa", request.ClientId);
        Assert.Equal("Okta", request.ProviderType);
    }

    [Fact]
    public void Create_with_whitespace_secret_sends_null_not_whitespace()
    {
        var model = OidcProviderFormModel.ForCreate();
        model.Name = "Acme IdP";
        model.ProviderType = "Generic";
        model.Authority = "https://idp.example";
        model.ClientId = "honua-admin";
        model.ClientSecret = "   ";

        Assert.Empty(model.Validate());
        var request = model.ToCreateRequest();

        Assert.Null(request.ClientSecret);
    }

    [Fact]
    public void Edit_without_rotate_omits_secret_from_request()
    {
        var existing = new OidcProviderResponse
        {
            ProviderId = Guid.NewGuid(),
            Name = "Acme IdP",
            ProviderType = "Generic",
            Authority = "https://idp.example",
            ClientId = "honua-admin",
            Enabled = true,
            IsHealthy = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var model = OidcProviderFormModel.ForEdit(existing);
        model.Name = "Acme IdP (renamed)";
        // RotateSecret stays false; ClientSecret stays empty.

        Assert.Empty(model.Validate());
        var request = model.ToUpdateRequest();

        Assert.Null(request.ClientSecret);
        Assert.Equal("Acme IdP (renamed)", request.Name);
    }

    [Fact]
    public void Edit_with_rotate_toggle_sends_new_secret()
    {
        var existing = new OidcProviderResponse
        {
            ProviderId = Guid.NewGuid(),
            Name = "Acme IdP",
            ProviderType = "Generic",
            Authority = "https://idp.example",
            ClientId = "honua-admin",
            Enabled = true,
            IsHealthy = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var model = OidcProviderFormModel.ForEdit(existing);
        model.RotateSecret = true;
        model.ClientSecret = "rotated-secret";

        Assert.Empty(model.Validate());
        var request = model.ToUpdateRequest();

        Assert.Equal("rotated-secret", request.ClientSecret);
    }

    [Fact]
    public void Edit_with_rotate_toggle_but_empty_secret_fails_validation()
    {
        var existing = new OidcProviderResponse
        {
            ProviderId = Guid.NewGuid(),
            Name = "Acme IdP",
            ProviderType = "Generic",
            Authority = "https://idp.example",
            ClientId = "honua-admin",
            Enabled = true,
            IsHealthy = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var model = OidcProviderFormModel.ForEdit(existing);
        model.RotateSecret = true;
        model.ClientSecret = string.Empty;

        var errors = model.Validate();

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Enter the new client secret", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClearSecret_zeroes_out_plaintext_secret()
    {
        var model = OidcProviderFormModel.ForCreate();
        model.ClientSecret = "s3cret";

        model.ClearSecret();

        Assert.Equal(string.Empty, model.ClientSecret);
    }

    [Fact]
    public void Validate_requires_https_or_http_authority()
    {
        var model = OidcProviderFormModel.ForCreate();
        model.Name = "Acme";
        model.ProviderType = "Generic";
        model.Authority = "ftp://idp.example";
        model.ClientId = "honua-admin";
        model.ClientSecret = "s3cret";

        var errors = model.Validate();

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SecretHint_in_edit_mode_describes_secret_as_write_only_without_asserting_it_is_set()
    {
        // honua-server's OidcProviderResponse does not expose whether a secret is
        // configured. The hint must therefore describe the value as write-only
        // rather than asserting "(set)" — public / PKCE providers stored with a
        // null secret would otherwise be misrepresented when reopened for edit.
        var existing = new OidcProviderResponse
        {
            ProviderId = Guid.NewGuid(),
            Name = "Acme IdP",
            ProviderType = "Generic",
            Authority = "https://idp.example",
            ClientId = "honua-admin",
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var model = OidcProviderFormModel.ForEdit(existing);

        Assert.Contains("Write-only", model.SecretHint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Rotate", model.SecretHint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("(set)", model.SecretHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecretHint_in_edit_rotate_mode_shows_optional_create_hint()
    {
        // Once the operator opts in to rotating, the editable field is shown,
        // and the same "optional / required for confidential" hint applies as
        // in create mode.
        var existing = new OidcProviderResponse
        {
            ProviderId = Guid.NewGuid(),
            Name = "Acme IdP",
            ProviderType = "Generic",
            Authority = "https://idp.example",
            ClientId = "honua-admin",
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var model = OidcProviderFormModel.ForEdit(existing);
        model.RotateSecret = true;

        Assert.Contains("Optional", model.SecretHint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PKCE", model.SecretHint, StringComparison.OrdinalIgnoreCase);
    }
}
