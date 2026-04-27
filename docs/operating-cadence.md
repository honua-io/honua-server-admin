# Operating cadence

This repo uses a weekly issue-review cadence to keep the admin UI backlog
actionable while large roadmap epics are in flight.

## Weekly owner routine

Run this every Monday in the repo's primary working timezone.

1. Review newly opened issues.
2. Apply `area/*`, `priority/*`, `effort/*`, `phase/*`, assignee, and milestone
   metadata where the issue has enough information.
3. Confirm the next two weeks include enough `ready-to-start` work.
4. Add dependency notes to blocked issues.
5. Recheck MVP/Beta/GA mix against the current release goal.
6. Split oversized implementation tickets, or leave a comment that explicitly
   accepts the size and names the reason.
7. Close completed work within 24 hours of verification.
8. Comment on partially complete or stale issues with the exact remaining
   tasks, then rephase or close them.

## Scope gate

New scope is accepted only when the issue or weekly review comment captures
the tradeoff:

- what user or operator outcome is added;
- what current-phase work is deferred, removed, or intentionally left at
  experimental quality;
- what cross-repo dependency blocks the work, if any;
- which phase owns the follow-up if the work is not MVP scope.

## Ready-to-start criteria

Use `ready-to-start` only when all of these are true:

- the issue has a single clear owner repo;
- the first deliverable can be completed without waiting on another repo;
- acceptance criteria are testable from this repo;
- no unresolved design question blocks implementation;
- the expected size is `effort/XS`, `effort/S`, or `effort/M`.

Large epics can remain open without `ready-to-start`; child tickets should carry
that label once they satisfy these criteria.

## Weekly comment template

Post this as a dated comment on issue
[#11](https://github.com/honua-io/honua-server-admin/issues/11).

```markdown
## Weekly backlog review - YYYY-MM-DD

### Backlog review
- New issues triaged:
- Metadata corrections:
- Ready-to-start queue:
- Blocked/dependency notes:

### Scope gate
- MVP/Beta/GA mix:
- Oversized tickets accepted or split:
- Tradeoffs recorded:

### Done/close hygiene
- Closed as done:
- Partially complete updates:
- Stale items rephased or closed:

### Next actions
- TBD
```

## Current recurring checks

- `dotnet test tests/Honua.Admin.Tests/Honua.Admin.Tests.csproj`
- `dotnet run --project tools/audit-api-surface -- generate`
- `dotnet run --project tools/audit-api-surface -- seed-coverage`
- `dotnet run --project tools/audit-api-surface -- render`
- Release candidates must satisfy the quality-gate release checklist in
  [`docs/admin-ui-quality-gates.md`](admin-ui-quality-gates.md).

Run the audit commands when server API coverage changed or when a sibling
`honua-server` checkout is available for drift comparison.
