namespace Honua.Admin.Models.SpecWorkspace;

public sealed record SpecSectionChange
{
    public required SpecSectionId Section { get; init; }
    public required SpecChangeStatus Status { get; init; }
    public required string CurrentSummary { get; init; }
    public required string BaselineSummary { get; init; }
}

public enum SpecChangeStatus
{
    Empty,
    Added,
    Modified,
    Removed,
    Unchanged
}
