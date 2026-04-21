namespace Honua.Admin.Models.SpecWorkspace;

public enum ValidationSeverity
{
    Red,
    Yellow
}

/// <summary>
/// Single diagnostic surfaced next to a section or identifier in the DSL pane. Red =
/// unknown identifier / type mismatch / missing required / unknown op. Yellow = CRS unit
/// mismatch / mutable-source-no-pin / estimated-oversize / non-deterministic op.
/// </summary>
public sealed record ValidationDiagnostic(
    SpecSectionId Section,
    ValidationSeverity Severity,
    string Code,
    string Message,
    string? Identifier = null);
