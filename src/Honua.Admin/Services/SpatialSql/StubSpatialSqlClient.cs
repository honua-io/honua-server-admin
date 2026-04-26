using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.SpatialSql;

namespace Honua.Admin.Services.SpatialSql;

/// <summary>
/// In-process stub used while the server SQL playground endpoints are still in
/// flight. Honours the same safety contract the HTTP backend will enforce —
/// reads succeed, mutations need <see cref="SqlExecuteRequest.AllowMutation"/>,
/// the row cap surfaces through <see cref="SqlExecuteResult.Truncated"/>, and the
/// stub returns realistic geometry rows so the map preview path is exercisable
/// end-to-end.
/// </summary>
public sealed class StubSpatialSqlClient : ISpatialSqlClient
{
    private const int DefaultRowLimit = 1000;
    private const int DefaultTimeoutMs = 30_000;

    private static readonly Regex FromTableRegex = new(
        @"\bfrom\s+(?<table>[a-zA-Z_][\w]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly Dictionary<string, StubTable> _tables;
    private readonly HashSet<string> _registeredViewNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _viewSync = new();
    private int _auditSequence;

    public StubSpatialSqlClient()
    {
        _tables = BuildSeed();
    }

    public Task<SchemaSnapshot> GetSchemaAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = new SchemaSnapshot
        {
            Tables = _tables.Values.Select(t => new SchemaTable
            {
                Name = t.Name,
                Description = t.Description,
                GeometryColumn = t.GeometryColumn,
                Srid = t.Srid,
                Columns = t.Columns
            }).ToArray(),
            Functions = PostGisReference.Functions,
            Operators = PostGisReference.Operators,
            FetchedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        };
        return Task.FromResult(snapshot);
    }

    public async Task<SqlExecuteResult> ExecuteAsync(SqlExecuteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        var sql = request.Sql ?? string.Empty;
        var rowLimit = request.RowLimit is int rl && rl > 0 ? Math.Min(rl, DefaultRowLimit) : DefaultRowLimit;
        var timeoutMs = request.TimeoutMs is int t && t > 0 ? t : DefaultTimeoutMs;

        if (string.IsNullOrWhiteSpace(sql))
        {
            return new SqlExecuteResult
            {
                RowLimit = rowLimit,
                TimeoutMs = timeoutMs,
                Error = new SqlExecuteError("empty_sql", "SQL text is empty.")
            };
        }

        var mutating = MutationGuard.IsMutating(sql);
        if (mutating && !request.AllowMutation)
        {
            return new SqlExecuteResult
            {
                RowLimit = rowLimit,
                TimeoutMs = timeoutMs,
                Error = new SqlExecuteError(
                    "mutation_blocked",
                    "Mutating SQL is rejected by default. Re-submit with the per-query override to run it.")
            };
        }

        if (mutating && request.AllowMutation)
        {
            var auditId = $"audit-{Interlocked.Increment(ref _auditSequence):D6}";
            return new SqlExecuteResult
            {
                Columns = new[] { new SqlColumn("status", "text") },
                Rows = new[] { new SqlRow(new string?[] { "ok" }) },
                RowLimit = rowLimit,
                TimeoutMs = timeoutMs,
                ElapsedMs = 12,
                AuditEntryId = auditId
            };
        }

        var table = ResolveTable(sql);
        if (table is null)
        {
            return new SqlExecuteResult
            {
                Columns = new[] { new SqlColumn("note", "text") },
                Rows = new[] { new SqlRow(new string?[] { "Stub: query parsed but no FROM table recognised." }) },
                RowLimit = rowLimit,
                TimeoutMs = timeoutMs,
                ElapsedMs = 4
            };
        }

        IReadOnlyList<SqlRow> rows = table.Rows;
        var truncated = false;
        if (rows.Count > rowLimit)
        {
            rows = rows.Take(rowLimit).ToArray();
            truncated = true;
        }

        var columns = table.SelectSqlColumns();
        var geometryIndex = columns.FindIndex(c => c.IsGeometry);

        return new SqlExecuteResult
        {
            Columns = columns,
            Rows = rows,
            RowLimit = rowLimit,
            TimeoutMs = timeoutMs,
            Truncated = truncated,
            GeometryColumnIndex = geometryIndex >= 0 ? geometryIndex : null,
            GeometrySrid = geometryIndex >= 0 ? table.Srid : null,
            ElapsedMs = 38
        };
    }

    public Task<ExplainPlan> ExplainAsync(SqlExplainRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Sql))
        {
            return Task.FromResult(new ExplainPlan
            {
                Root = new ExplainNode { NodeType = "Empty" },
                Error = new SqlExecuteError("empty_sql", "SQL text is empty.")
            });
        }

        var table = ResolveTable(request.Sql);
        var relationName = table?.Name ?? "unknown";
        var rowEstimate = table?.Rows.Count ?? 100;

        var scan = new ExplainNode
        {
            NodeType = "Seq Scan",
            Relation = relationName,
            ActualRows = rowEstimate,
            PlanRows = rowEstimate,
            ActualTotalMs = 12.4,
            TotalCost = 32.5
        };

        var aggregate = new ExplainNode
        {
            NodeType = "Aggregate",
            ActualRows = 1,
            PlanRows = 1,
            ActualTotalMs = 14.1,
            TotalCost = 38.7,
            Children = new[] { scan }
        };

        return Task.FromResult(new ExplainPlan
        {
            Root = aggregate,
            TotalElapsedMs = 18.3,
            PlanningMs = 0.7
        });
    }

    public Task<NamedViewRegistration> SaveViewAsync(SaveViewRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Task.FromResult(new NamedViewRegistration
            {
                Name = string.Empty,
                Error = new SqlExecuteError("invalid_name", "Named views require a non-empty name.")
            });
        }

        if (string.IsNullOrWhiteSpace(request.Sql))
        {
            return Task.FromResult(new NamedViewRegistration
            {
                Name = request.Name,
                Error = new SqlExecuteError("empty_sql", "Cannot save an empty query.")
            });
        }

        if (MutationGuard.IsMutating(request.Sql))
        {
            return Task.FromResult(new NamedViewRegistration
            {
                Name = request.Name,
                Error = new SqlExecuteError("mutation_blocked", "Named views must be read-only queries.")
            });
        }

        lock (_viewSync)
        {
            if (!_registeredViewNames.Add(request.Name))
            {
                return Task.FromResult(new NamedViewRegistration
                {
                    Name = request.Name,
                    Error = new SqlExecuteError("duplicate_name", $"A named view called '{request.Name}' already exists.")
                });
            }
        }

        return Task.FromResult(new NamedViewRegistration
        {
            Name = request.Name,
            FeatureServerUrl = $"/services/featureserver/views/{request.Name}/FeatureServer/0",
            OgcFeaturesUrl = $"/ogc/features/v1/collections/{request.Name}",
            ODataUrl = $"/odata/views/{request.Name}"
        });
    }

    private StubTable? ResolveTable(string sql)
    {
        var stripped = MutationGuard.StripCommentsAndLiterals(sql);
        var match = FromTableRegex.Match(stripped);
        if (!match.Success)
        {
            return null;
        }
        return _tables.TryGetValue(match.Groups["table"].Value, out var table) ? table : null;
    }

    private static Dictionary<string, StubTable> BuildSeed()
    {
        var parcels = new StubTable(
            Name: "parcels",
            Description: "Statewide parcel polygons.",
            GeometryColumn: "geom",
            Srid: 4326,
            Columns: new[]
            {
                new SchemaColumn("id", "uuid", "primary key"),
                new SchemaColumn("county", "text", "county name"),
                new SchemaColumn("acreage", "double precision", "parcel acreage"),
                new SchemaColumn("geom", "geometry(Polygon,4326)", "parcel polygon")
            },
            Rows: new[]
            {
                new SqlRow(new string?[]
                {
                    "11111111-1111-1111-1111-111111111111",
                    "Alpha",
                    "12.4",
                    "{\"type\":\"Polygon\",\"coordinates\":[[[-122.42,37.77],[-122.42,37.78],[-122.41,37.78],[-122.41,37.77],[-122.42,37.77]]]}"
                }),
                new SqlRow(new string?[]
                {
                    "22222222-2222-2222-2222-222222222222",
                    "Beta",
                    "5.2",
                    "{\"type\":\"Polygon\",\"coordinates\":[[[-122.40,37.79],[-122.40,37.80],[-122.39,37.80],[-122.39,37.79],[-122.40,37.79]]]}"
                })
            });

        var wells = new StubTable(
            Name: "wells",
            Description: "Licensed water wells with annual production.",
            GeometryColumn: "geom",
            Srid: 4326,
            Columns: new[]
            {
                new SchemaColumn("id", "text", "well id"),
                new SchemaColumn("depth_m", "double precision", "depth in metres"),
                new SchemaColumn("basin", "text", "basin name"),
                new SchemaColumn("geom", "geometry(Point,4326)", "well point")
            },
            Rows: new[]
            {
                new SqlRow(new string?[]
                {
                    "W-12",
                    "30",
                    "North",
                    "{\"type\":\"Point\",\"coordinates\":[-122.40,37.76]}"
                }),
                new SqlRow(new string?[]
                {
                    "W-33",
                    "90",
                    "South",
                    "{\"type\":\"Point\",\"coordinates\":[-122.45,37.80]}"
                })
            });

        return new Dictionary<string, StubTable>(StringComparer.OrdinalIgnoreCase)
        {
            [parcels.Name] = parcels,
            [wells.Name] = wells
        };
    }

    private sealed record StubTable(
        string Name,
        string Description,
        string GeometryColumn,
        int Srid,
        IReadOnlyList<SchemaColumn> Columns,
        IReadOnlyList<SqlRow> Rows)
    {
        public List<SqlColumn> SelectSqlColumns() => Columns
            .Select(c => new SqlColumn(c.Name, c.Type, IsGeometry: c.Name == GeometryColumn))
            .ToList();
    }

    private static class PostGisReference
    {
        public static IReadOnlyList<PostGisFunction> Functions { get; } = new[]
        {
            new PostGisFunction("ST_Area", "ST_Area(geometry)", "measurement", "Returns the area of a polygon."),
            new PostGisFunction("ST_AsGeoJSON", "ST_AsGeoJSON(geometry)", "format", "Returns GeoJSON for a geometry."),
            new PostGisFunction("ST_Buffer", "ST_Buffer(geometry, distance)", "geometry", "Returns a geometry buffered by the given distance."),
            new PostGisFunction("ST_Centroid", "ST_Centroid(geometry)", "geometry", "Returns the geometric centre of a geometry."),
            new PostGisFunction("ST_Contains", "ST_Contains(a, b)", "predicate", "True when a fully contains b."),
            new PostGisFunction("ST_DWithin", "ST_DWithin(a, b, distance)", "predicate", "True when a is within distance of b."),
            new PostGisFunction("ST_GeomFromText", "ST_GeomFromText(wkt, srid)", "constructor", "Parses WKT into a geometry."),
            new PostGisFunction("ST_Intersects", "ST_Intersects(a, b)", "predicate", "True when a and b share any point."),
            new PostGisFunction("ST_Transform", "ST_Transform(geometry, srid)", "geometry", "Reprojects geometry to a target SRID."),
            new PostGisFunction("ST_Within", "ST_Within(a, b)", "predicate", "True when a is fully within b.")
        };

        public static IReadOnlyList<PostGisOperator> Operators { get; } = new[]
        {
            new PostGisOperator("&&", "Bounding boxes intersect."),
            new PostGisOperator("@>", "Geometry A contains geometry B."),
            new PostGisOperator("<@", "Geometry A is contained by geometry B."),
            new PostGisOperator("~=", "Geometries are equal.")
        };
    }
}
