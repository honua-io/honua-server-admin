using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.SpatialSql;

namespace Honua.Admin.Services.SpatialSql;

/// <summary>
/// Seam between the admin SQL playground and the server-side SQL endpoints.
/// S1 ships <see cref="StubSpatialSqlClient"/>; the HTTP-backed implementation lands
/// once the server child tickets (<c>POST /api/v1/admin/sql/execute</c>,
/// <c>/explain</c>, <c>/schema</c>, <c>/views</c>) are merged.
/// </summary>
public interface ISpatialSqlClient
{
    Task<SchemaSnapshot> GetSchemaAsync(CancellationToken cancellationToken);

    Task<SqlExecuteResult> ExecuteAsync(SqlExecuteRequest request, CancellationToken cancellationToken);

    Task<ExplainPlan> ExplainAsync(SqlExplainRequest request, CancellationToken cancellationToken);

    Task<NamedViewRegistration> SaveViewAsync(SaveViewRequest request, CancellationToken cancellationToken);
}
