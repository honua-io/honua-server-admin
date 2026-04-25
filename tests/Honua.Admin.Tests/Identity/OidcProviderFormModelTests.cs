using System;
using Honua.Admin.Components.Identity;
using Honua.Admin.Models.Identity;
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
    public void SecretHint_in_edit_mode_shows_masked_indicator()
    {
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

        // Hint must indicate the secret is set without exposing it.
        Assert.Contains("set", model.SecretHint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Rotate", model.SecretHint, StringComparison.OrdinalIgnoreCase);
    }
}
