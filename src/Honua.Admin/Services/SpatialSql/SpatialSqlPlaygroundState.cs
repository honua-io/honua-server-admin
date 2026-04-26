using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.SpatialSql;

namespace Honua.Admin.Services.SpatialSql;

public enum SpatialSqlPaneStatus
{
    Idle,
    Loading,
    Executing,
    Explaining,
    Saving,
    Error
}

/// <summary>
/// Scoped observable store backing the SQL playground. All mutations route through
/// explicit methods so telemetry, cancellation, and persistence stay single-origin
/// — the same pattern used by <see cref="Honua.Admin.Services.SpecWorkspace.SpecWorkspaceState"/>.
/// </summary>
public sealed class SpatialSqlPlaygroundState : IDisposable
{
    private readonly ISpatialSqlClient _client;
    private readonly ISpatialSqlTelemetry _telemetry;
    private readonly object _sync = new();
    private CancellationTokenSource? _executeCts;
    private CancellationTokenSource? _explainCts;
    private bool _disposed;

    public SpatialSqlPlaygroundState(ISpatialSqlClient client, ISpatialSqlTelemetry telemetry)
    {
        _client = client;
        _telemetry = telemetry;
    }

    public string Sql { get; private set; } = string.Empty;

    public SchemaSnapshot? Schema { get; private set; }

    public SqlExecuteResult? LastResult { get; private set; }

    /// <summary>
    /// SQL text that produced <see cref="LastResult"/>. Snapshotted at the moment
    /// the result is stored so <see cref="SaveViewAsync"/> registers the view
    /// against the executed SQL (and its derived geometry metadata) instead of
    /// whatever the operator has typed since. Distinct from <see cref="Sql"/>,
    /// which tracks the live editor buffer.
    /// </summary>
    public string? LastResultSql { get; private set; }

    public ExplainPlan? LastPlan { get; private set; }

    public NamedViewRegistration? LastSavedView { get; private set; }

    public string? LastError { get; private set; }

    public SpatialSqlPaneStatus Status { get; private set; } = SpatialSqlPaneStatus.Idle;

    public bool MutationOverrideArmed { get; private set; }

    public bool ExportTruncatedConfirmed { get; private set; }

    public string ResultsTab { get; private set; } = "table";

    public event Action? OnChanged;

    public void SetSql(string? value)
    {
        Sql = value ?? string.Empty;
        // Disarm any previous mutation override the moment the operator edits the SQL —
        // a confirmation must apply only to the exact statement the operator approved.
        MutationOverrideArmed = false;
        Notify();
    }

    public void SetResultsTab(string tab)
    {
        if (tab != "table" && tab != "map")
        {
            return;
        }
        ResultsTab = tab;
        _telemetry.Record("results_tab_changed", new Dictionary<string, object?> { ["tab"] = tab });
        Notify();
    }

    public void ArmMutationOverride()
    {
        MutationOverrideArmed = true;
        _telemetry.Record("mutation_override_accepted", new Dictionary<string, object?>
        {
            ["sql_length"] = Sql.Length
        });
        Notify();
    }

    public void ConfirmTruncatedExport()
    {
        ExportTruncatedConfirmed = true;
        Notify();
    }

    /// <summary>
    /// Surfaces a client-side export failure (e.g. non-WGS84 SRID) on the toolbar
    /// banner without disturbing the underlying result. Telemetry records the
    /// rejection so the operator-visible error has a paired log entry.
    ///
    /// Deliberate exception to the LastError-implies-Error invariant: the query
    /// itself succeeded — only the export action failed — so <see cref="Status"/>
    /// stays at its prior value (typically Idle) while the banner communicates the
    /// rejection. The next state-changing call (re-Run, EXPLAIN, save) clears
    /// <see cref="LastError"/>.
    /// </summary>
    public void SetExportError(string message)
    {
        LastError = message;
        _telemetry.Record("export_rejected", new Dictionary<string, object?>
        {
            ["message"] = message
        });
        Notify();
    }

    /// <summary>
    /// Reason the map preview cannot be rendered for the current result, or
    /// <c>null</c> when the preview is renderable. Mirrors the SRID guard the
    /// GeoJSON exporter enforces so both surfaces refuse the same inputs —
    /// MapLibre expects WGS84 longitude/latitude, so a non-4326 result would
    /// silently put markers off-map.
    /// </summary>
    public string? MapPreviewBlockedReason
    {
        get
        {
            if (LastResult is not { HasGeometry: true } result)
            {
                return null;
            }
            if (result.GeometrySrid is int srid && srid != SqlResultExporter.Wgs84Srid)
            {
                return string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"Map preview requires WGS84 (SRID 4326); result is SRID {srid}. Reproject server-side to preview.");
            }
            return null;
        }
    }

    public async Task LoadSchemaAsync(CancellationToken cancellationToken = default)
    {
        Status = SpatialSqlPaneStatus.Loading;
        LastError = null;
        Notify();

        try
        {
            Schema = await _client.GetSchemaAsync(cancellationToken).ConfigureAwait(false);
            Status = SpatialSqlPaneStatus.Idle;
            _telemetry.Record("schema_loaded", new Dictionary<string, object?>
            {
                ["tables"] = Schema?.Tables.Count ?? 0,
                ["functions"] = Schema?.Functions.Count ?? 0
            });
        }
        catch (OperationCanceledException)
        {
            // Notify before re-throwing so subscribers observing the Loading state
            // see the Idle terminal state — RunQueryAsync / RunExplainAsync make
            // the same guarantee via their trailing Notify().
            Status = SpatialSqlPaneStatus.Idle;
            Notify();
            throw;
        }
        catch (Exception ex)
        {
            Status = SpatialSqlPaneStatus.Error;
            LastError = ex.Message;
            _telemetry.Record("schema_load_failed", new Dictionary<string, object?>
            {
                ["message"] = ex.Message
            });
        }

        Notify();
    }

    public async Task RunQueryAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Sql))
        {
            SupersedeActiveExecution();
            LastError = "Enter a SQL statement to run.";
            Status = SpatialSqlPaneStatus.Error;
            Notify();
            return;
        }

        var allowMutation = MutationOverrideArmed;
        // Consume the override the moment we snapshot it so a single armed
        // confirmation is single-shot regardless of how this attempt terminates
        // (success, server error, transport error, cancellation). Without this
        // consumption, a transport failure would leak the armed state into the
        // next click of Run and silently re-submit with allowMutation=true.
        MutationOverrideArmed = false;

        if (!allowMutation && MutationGuard.IsMutating(Sql))
        {
            SupersedeActiveExecution();
            LastError = "Mutating SQL is rejected by default. Use the override dialog to confirm and resubmit.";
            Status = SpatialSqlPaneStatus.Error;
            LastResult = new SqlExecuteResult
            {
                RowLimit = 0,
                TimeoutMs = 0,
                Error = new SqlExecuteError("mutation_blocked", LastError)
            };
            LastResultSql = Sql;
            _telemetry.Record("query_rejected", new Dictionary<string, object?>
            {
                ["reason"] = "mutation_blocked"
            });
            Notify();
            return;
        }

        CancellationTokenSource cts;
        lock (_sync)
        {
            _executeCts?.Cancel();
            _executeCts?.Dispose();
            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _executeCts = cts;
        }

        Status = SpatialSqlPaneStatus.Executing;
        LastError = null;
        ExportTruncatedConfirmed = false;
        Notify();

        // Snapshot the SQL that this execution submitted so the success path can
        // pair LastResult with the exact text that produced it — even if the
        // operator edits the editor while the query is in flight.
        var submittedSql = Sql;
        var watch = Stopwatch.StartNew();
        var notify = false;
        try
        {
            _telemetry.Record("query_submitted", new Dictionary<string, object?>
            {
                ["sql_length"] = submittedSql.Length,
                ["allow_mutation"] = allowMutation
            });

            var request = new SqlExecuteRequest
            {
                Sql = submittedSql,
                AllowMutation = allowMutation
            };

            var result = await _client.ExecuteAsync(request, cts.Token).ConfigureAwait(false);
            watch.Stop();

            // Ownership gate: a newer RunQueryAsync may have superseded this one
            // (cancelled our cts, replaced _executeCts) while the await was in
            // flight. The newer call already wrote its own Status/LastError/Notify;
            // overwriting state here would race with it and undo those writes.
            if (!IsActiveExecution(cts))
            {
                return;
            }

            LastResult = result;
            LastResultSql = submittedSql;
            if (result.IsError)
            {
                Status = SpatialSqlPaneStatus.Error;
                LastError = result.Error?.Message;
                _telemetry.Record("query_rejected", new Dictionary<string, object?>
                {
                    ["reason"] = result.Error?.Code,
                    ["message"] = result.Error?.Message
                });
            }
            else
            {
                Status = SpatialSqlPaneStatus.Idle;
                // Auto-switch to the map tab only when the geometry can actually be
                // previewed — non-WGS84 results would mis-render on MapLibre, so leave
                // the operator on the table view with the blocked-reason banner.
                ResultsTab = result.HasGeometry && IsRenderableSrid(result.GeometrySrid) ? "map" : "table";
                _telemetry.RecordLatency("query_completed", watch.ElapsedMilliseconds, new Dictionary<string, object?>
                {
                    ["rows"] = result.Rows.Count,
                    ["truncated"] = result.Truncated,
                    ["has_geometry"] = result.HasGeometry,
                    ["audit_entry_id"] = result.AuditEntryId
                });

                if (result.Truncated)
                {
                    _telemetry.Record("cap_reached", new Dictionary<string, object?>
                    {
                        ["row_limit"] = result.RowLimit
                    });
                }
            }

            // Override consumption already happened up front (before the request was
            // submitted) so cancellation / transport failures cannot leak the armed
            // state into a follow-up click of Run.
            notify = true;
        }
        catch (OperationCanceledException)
        {
            // Same ownership gate as the success path: if we were superseded, the
            // newer query already owns Status — do not stomp it back to Idle while
            // the newer query is mid-flight.
            if (!IsActiveExecution(cts))
            {
                return;
            }
            Status = SpatialSqlPaneStatus.Idle;
            notify = true;
        }
        catch (Exception ex)
        {
            if (!IsActiveExecution(cts))
            {
                return;
            }
            Status = SpatialSqlPaneStatus.Error;
            LastError = ex.Message;
            _telemetry.Record("query_rejected", new Dictionary<string, object?>
            {
                ["reason"] = "transport_error",
                ["message"] = ex.Message
            });
            notify = true;
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_executeCts, cts))
                {
                    _executeCts.Dispose();
                    _executeCts = null;
                }
            }
        }

        if (notify)
        {
            Notify();
        }
    }

    private void SupersedeActiveExecution()
    {
        lock (_sync)
        {
            _executeCts?.Cancel();
            _executeCts?.Dispose();
            _executeCts = null;
        }
    }

    private bool IsActiveExecution(CancellationTokenSource cts)
    {
        lock (_sync)
        {
            return ReferenceEquals(_executeCts, cts);
        }
    }

    private bool IsActiveExplain(CancellationTokenSource cts)
    {
        lock (_sync)
        {
            return ReferenceEquals(_explainCts, cts);
        }
    }

    private void SupersedeActiveExplain()
    {
        lock (_sync)
        {
            _explainCts?.Cancel();
            _explainCts?.Dispose();
            _explainCts = null;
        }
    }

    private CancellationTokenSource StartExplainExecution(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _explainCts?.Cancel();
            _explainCts?.Dispose();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _explainCts = cts;
            return cts;
        }
    }

    public async Task RunExplainAsync(CancellationToken cancellationToken = default)
    {
        var submittedSql = Sql;

        if (string.IsNullOrWhiteSpace(submittedSql))
        {
            SupersedeActiveExplain();
            LastPlan = null;
            LastError = null;
            Status = SpatialSqlPaneStatus.Idle;
            Notify();
            return;
        }

        // EXPLAIN ANALYZE actually executes the statement on the server, and the
        // server-side EXPLAIN endpoint has no AllowMutation contract or audit
        // hook. Reject mutating SQL outright — the per-query override on the Run
        // path is the only audited mutation channel, EXPLAIN is not.
        if (MutationGuard.IsMutating(submittedSql))
        {
            SupersedeActiveExplain();
            LastPlan = null;
            LastError = "EXPLAIN is rejected for mutating SQL — EXPLAIN ANALYZE would execute the statement outside the audited mutation-override flow.";
            Status = SpatialSqlPaneStatus.Error;
            _telemetry.Record("explain_rejected", new Dictionary<string, object?>
            {
                ["reason"] = "mutation_blocked"
            });
            Notify();
            return;
        }

        var cts = StartExplainExecution(cancellationToken);

        LastPlan = null;
        Status = SpatialSqlPaneStatus.Explaining;
        LastError = null;
        Notify();

        var watch = Stopwatch.StartNew();
        var notify = false;
        try
        {
            var plan = await _client.ExplainAsync(new SqlExplainRequest { Sql = submittedSql }, cts.Token).ConfigureAwait(false);
            watch.Stop();

            // Same ownership invariant as query execution: a later EXPLAIN attempt
            // owns the plan pane, even if this client call completes afterwards.
            if (!IsActiveExplain(cts))
            {
                return;
            }

            LastPlan = plan;
            Status = plan.IsError ? SpatialSqlPaneStatus.Error : SpatialSqlPaneStatus.Idle;
            if (plan.IsError)
            {
                LastError = plan.Error?.Message;
                _telemetry.Record("explain_rejected", new Dictionary<string, object?>
                {
                    ["reason"] = plan.Error?.Code,
                    ["message"] = plan.Error?.Message
                });
            }
            else
            {
                _telemetry.RecordLatency("explain_completed", watch.ElapsedMilliseconds, new Dictionary<string, object?>
                {
                    ["total_elapsed_ms"] = plan.TotalElapsedMs,
                    ["root_node"] = plan.Root.NodeType
                });
            }
            notify = true;
        }
        catch (OperationCanceledException)
        {
            if (!IsActiveExplain(cts))
            {
                return;
            }
            Status = SpatialSqlPaneStatus.Idle;
            notify = true;
        }
        catch (Exception ex)
        {
            if (!IsActiveExplain(cts))
            {
                return;
            }
            Status = SpatialSqlPaneStatus.Error;
            LastError = ex.Message;
            _telemetry.Record("explain_rejected", new Dictionary<string, object?>
            {
                ["reason"] = "transport_error",
                ["message"] = ex.Message
            });
            notify = true;
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_explainCts, cts))
                {
                    _explainCts.Dispose();
                    _explainCts = null;
                }
            }
        }

        if (notify)
        {
            Notify();
        }
    }

    /// <summary>
    /// True when the editor's current SQL still matches the SQL that produced
    /// <see cref="LastResult"/> and the result has rows the operator could turn
    /// into a named view. The Save toolbar binds to this so an edited buffer
    /// cannot register a view against stale geometry metadata pulled from
    /// <see cref="LastResult"/>.
    /// </summary>
    public bool CanSaveCurrentResult()
    {
        if (LastResult is null || LastResult.IsError || LastResult.Rows.Count == 0)
        {
            return false;
        }
        return string.Equals(Sql, LastResultSql, StringComparison.Ordinal);
    }

    public async Task<NamedViewRegistration> SaveViewAsync(string name, string? description, CancellationToken cancellationToken = default)
    {
        Status = SpatialSqlPaneStatus.Saving;
        LastError = null;
        Notify();

        try
        {
            // Build the request from the SQL that produced LastResult, not the live
            // editor buffer. The geometry column / SRID we pass through were derived
            // from that execution; pairing them with whatever the operator typed
            // since would register a view against mismatched SQL+metadata.
            var request = new SaveViewRequest
            {
                Name = name,
                Description = description,
                Sql = LastResultSql ?? Sql,
                GeometryColumn = LastResult?.HasGeometry == true && LastResult.GeometryColumnIndex is int idx
                    ? LastResult.Columns[idx].Name
                    : null,
                Srid = LastResult?.GeometrySrid
            };

            var registration = await _client.SaveViewAsync(request, cancellationToken).ConfigureAwait(false);
            LastSavedView = registration;
            Status = registration.IsError ? SpatialSqlPaneStatus.Error : SpatialSqlPaneStatus.Idle;
            if (registration.IsError)
            {
                LastError = registration.Error?.Message;
                _telemetry.Record("view_save_rejected", new Dictionary<string, object?>
                {
                    ["reason"] = registration.Error?.Code,
                    ["message"] = registration.Error?.Message
                });
            }
            else
            {
                _telemetry.Record("view_saved", new Dictionary<string, object?>
                {
                    ["name"] = registration.Name
                });
            }
            Notify();
            return registration;
        }
        catch (OperationCanceledException)
        {
            // Match the cancellation contract used by LoadSchemaAsync /
            // RunQueryAsync / RunExplainAsync: cancellation lands in Idle, not in
            // the Error/transport_error bucket, and subscribers are notified before
            // the exception propagates so any 'Saving' spinner can clear.
            Status = SpatialSqlPaneStatus.Idle;
            Notify();
            throw;
        }
        catch (Exception ex)
        {
            Status = SpatialSqlPaneStatus.Error;
            LastError = ex.Message;
            _telemetry.Record("view_save_rejected", new Dictionary<string, object?>
            {
                ["reason"] = "transport_error",
                ["message"] = ex.Message
            });
            Notify();
            throw;
        }
    }

    public bool CanExportRows()
    {
        if (LastResult is null || LastResult.IsError || LastResult.Rows.Count == 0)
        {
            return false;
        }
        return !LastResult.Truncated || ExportTruncatedConfirmed;
    }

    public string ExportCsv()
    {
        var result = RequireExportableResult();
        _telemetry.Record("export_triggered", new Dictionary<string, object?>
        {
            ["format"] = "csv",
            ["rows"] = result.Rows.Count
        });
        return SqlResultExporter.ToCsv(result);
    }

    public string ExportGeoJson()
    {
        var result = RequireExportableResult();
        if (!result.HasGeometry)
        {
            throw new InvalidOperationException("Result has no geometry column to export.");
        }
        _telemetry.Record("export_triggered", new Dictionary<string, object?>
        {
            ["format"] = "geojson",
            ["rows"] = result.Rows.Count
        });
        return SqlResultExporter.ToGeoJson(result);
    }

    public string ExportClipboard()
    {
        var result = RequireExportableResult();
        _telemetry.Record("export_triggered", new Dictionary<string, object?>
        {
            ["format"] = "clipboard",
            ["rows"] = result.Rows.Count
        });
        return SqlResultExporter.ToClipboardText(result);
    }

    public IReadOnlyList<MapPreviewFeature> BuildMapFeatures()
    {
        if (LastResult is null || !LastResult.HasGeometry)
        {
            return Array.Empty<MapPreviewFeature>();
        }
        // Mirror the GeoJSON exporter's WGS84 guard so non-4326 SRIDs never reach
        // MapLibre — see <see cref="MapPreviewBlockedReason"/> for the surfaced text.
        if (!IsRenderableSrid(LastResult.GeometrySrid))
        {
            return Array.Empty<MapPreviewFeature>();
        }
        var geometryIndex = LastResult.GeometryColumnIndex!.Value;
        var idIndex = FindIdColumn(LastResult.Columns);
        var labelIndex = FindLabelColumn(LastResult.Columns, idIndex);

        var features = new List<MapPreviewFeature>(LastResult.Rows.Count);
        var fallback = 0;
        foreach (var row in LastResult.Rows)
        {
            if (geometryIndex >= row.Cells.Count)
            {
                continue;
            }

            var geometry = row.Cells[geometryIndex];
            if (string.IsNullOrWhiteSpace(geometry))
            {
                continue;
            }

            var id = idIndex >= 0 && idIndex < row.Cells.Count ? row.Cells[idIndex] : null;
            var label = labelIndex >= 0 && labelIndex < row.Cells.Count ? row.Cells[labelIndex] : null;
            features.Add(new MapPreviewFeature(
                id ?? $"row-{fallback}",
                label ?? id ?? $"row-{fallback}",
                geometry!));
            fallback++;
        }
        return features;
    }

    private SqlExecuteResult RequireExportableResult()
    {
        if (!CanExportRows())
        {
            throw new InvalidOperationException("Result is not exportable in its current state.");
        }
        return LastResult!;
    }

    private static int FindIdColumn(IReadOnlyList<SqlColumn> columns)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            if (columns[i].Name.Equals("id", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    private static bool IsRenderableSrid(int? srid) => srid is null || srid == SqlResultExporter.Wgs84Srid;

    private static int FindLabelColumn(IReadOnlyList<SqlColumn> columns, int idIndex)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            if (i == idIndex)
            {
                continue;
            }
            if (columns[i].IsGeometry)
            {
                continue;
            }
            return i;
        }
        return -1;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        try
        {
            _executeCts?.Cancel();
            _explainCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        _executeCts?.Dispose();
        _explainCts?.Dispose();
    }

    private void Notify() => OnChanged?.Invoke();
}

public sealed record MapPreviewFeature(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] string Id,
    [property: System.Text.Json.Serialization.JsonPropertyName("label")] string Label,
    [property: System.Text.Json.Serialization.JsonPropertyName("geoJson")] string GeoJson);
