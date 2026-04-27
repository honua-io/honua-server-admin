# Admin UI quality gates

Issue
[#9](https://github.com/honua-io/honua-server-admin/issues/9) owns the
baseline quality gates for admin UI changes. These checks are intentionally
small enough to run on every pull request, with deeper browser and server-backed
tests added as follow-up work.

## Performance budgets

The CI publish check fails when the Blazor WebAssembly output exceeds these
budgets:

| Budget | Limit | CI source |
| --- | ---: | --- |
| Total published output | 60 MiB | `ADMIN_PUBLISH_MAX_BYTES` |
| `_framework` payload | 45 MiB | `ADMIN_FRAMEWORK_MAX_BYTES` |
| WebAssembly binaries | 24 MiB | `ADMIN_WASM_MAX_BYTES` |

The bUnit smoke suite also pins the stub-backed first render budget for each
top workflow at 10 seconds. This is not a replacement for real browser timing;
it catches obvious render stalls and runaway lifecycle loops before a PR is
merged.

## Accessibility baseline

Every PR that touches admin UI pages must keep the baseline bUnit accessibility
checks green:

- interactive elements expose an accessible name through visible text,
  `aria-label`, `aria-labelledby`, `title`, placeholder text, or an associated
  `<label>`;
- tables expose an accessible name through `aria-label`, `aria-labelledby`, or
  `<caption>`;
- icon-only controls use an explicit accessible label;
- destructive actions remain reachable by keyboard and preserve their telemetry
  guard tests.

Manual release validation should still run a browser accessibility pass on the
changed workflow with keyboard-only navigation and an axe-compatible checker.

## Regression smoke suite

The PR gate renders these top workflows with the deterministic
`StubHonuaAdminClient`:

| Workflow | Route | Baseline assertion |
| --- | --- | --- |
| Dashboard | `/` | Feature overview and quick links render |
| Connections | `/connections` | Connection rows and create action render |
| Layers | `/layers` | Layer rows and publish action render |
| Services | `/services` | Service rows and settings action render |
| Deploy | `/deploy` | Preflight entry point renders |
| Observability | `/observability` | Recent error panel renders |
| Server info | `/server-info` | Configuration metadata renders |

Add a row here when a workflow becomes release-critical, then extend
`AdminQualityGateTests.TopWorkflows` in the same PR.

## Release checklist

Before tagging or merging a release branch:

- confirm `CI Gate` and `Require CI Success` passed on the release candidate;
- review the publish-size output in `Publish Verification` and compare it with
  the budgets above;
- run the top workflow smoke suite locally if the release contains major UI or
  dependency changes;
- complete the manual accessibility pass for changed workflows;
- leave an issue comment when any quality gate is deferred, including the
  follow-up issue and release owner.
