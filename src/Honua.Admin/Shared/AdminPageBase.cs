// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Honua.Admin.Services.Admin;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Honua.Admin.Shared;

/// <summary>
/// Base for restored admin pages. Centralises loading + error surfacing so
/// each page just calls <see cref="ExecuteAsync"/> with its operation and the
/// banner / overlay states stay consistent. Cherry-picked from PR #17, adapted
/// to the post-#27 shell (no SDK dependency; uses the in-repo
/// <see cref="IHonuaAdminClient"/>).
/// </summary>
public abstract class AdminPageBase : ComponentBase
{
    [Inject] protected IHonuaAdminClient AdminClient { get; set; } = null!;
    [Inject] protected ISnackbar Snackbar { get; set; } = null!;
    [Inject] protected NavigationManager Navigation { get; set; } = null!;
    [Inject] protected IAdminTelemetry Telemetry { get; set; } = null!;

    protected bool IsLoading { get; set; }
    protected string? ErrorMessage { get; set; }

    protected async Task ExecuteAsync(Func<Task> action)
    {
        IsLoading = true;
        ErrorMessage = null;
        StateHasChanged();

        try
        {
            await action().ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"Connection failed: {ex.Message}";
            Snackbar.Add(ErrorMessage, Severity.Error);
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "Request timed out.";
            Snackbar.Add(ErrorMessage, Severity.Warning);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Snackbar.Add(ErrorMessage, Severity.Error);
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    protected async Task<T?> ExecuteAsync<T>(Func<Task<T>> action)
    {
        T? result = default;
        await ExecuteAsync(async () => { result = await action().ConfigureAwait(false); }).ConfigureAwait(false);
        return result;
    }
}
