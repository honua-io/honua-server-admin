using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.SpecWorkspace;

namespace Honua.Admin.Services.SpecWorkspace;

/// <summary>
/// Seam between the admin UI and the grounding / planning / execution back-end. S1
/// ships with <see cref="StubSpecWorkspaceClient"/>; a gRPC-backed implementation lands
/// in a follow-on admin ticket once the sibling repos publish their surfaces.
/// </summary>
public interface ISpecWorkspaceClient
{
    Task<IntentOutcome> SubmitIntentAsync(IntentRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogCandidate>> ResolveCatalogAsync(ResolveQuery query, CancellationToken cancellationToken);

    Task<PlanResult> PlanAsync(SpecDocument document, CancellationToken cancellationToken);

    IAsyncEnumerable<ApplyEvent> ApplyAsync(SpecDocument document, string jobId, CancellationToken cancellationToken);

    Task CancelAsync(string jobId, CancellationToken cancellationToken);

    Task<string> SummarizeSectionAsync(SpecDocument document, SpecSectionId section, CancellationToken cancellationToken);

    Task<SpecGrammar> LoadGrammarAsync(CancellationToken cancellationToken);

    IReadOnlyList<ValidationDiagnostic> Validate(SpecDocument document);
}
